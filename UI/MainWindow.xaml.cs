using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ASCOM.DeviceInterface;
using heliomaster.Properties;
using heliomaster.Netio;
using Drivers = ASCOM.DriverAccess;
using Microsoft.Win32;
using Python.Runtime;

namespace heliomaster {
    public enum HardwareControlButtons {
        On, Off, Reset, Connect, Disconnect
    }

    public enum AutomationControlButtons {
        Run, RunUntil, Stop, WeatherToggle
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : HMWindow {
        private CamerasWindow _camerasWindow;
        public CamerasWindow CamerasWindow => _camerasWindow == null || _camerasWindow.IsClosed ? (_camerasWindow = new CamerasWindow()) : _camerasWindow;

        public readonly SettingsWindow SettingsWindow = new SettingsWindow();

        public MainWindow() {
            Application.Current.MainWindow = this;

            InitializeComponent();
        }

        private void OnExit(object sender, CancelEventArgs e) {
            S.Save();
        }

        private static async Task Connect(BaseHardwareControl h, string id = null) {
            id = id ?? (h is Dome   ? S.Dome.DomeID :
                     h is Telescope ? S.Mount.MountID :
                     h is Weather   ? O.WeatherID : null);
            if (!string.IsNullOrWhiteSpace(id) && !await h.Connect(id)) {
                MessageBox.Show($"Connecting to {h.Type.ToLower()} {id} failed.");
            }
        }

        #region MOUNT

        private void mountStop_Click(object sender, RoutedEventArgs e) {
            O.Mount.StopAllMotion();
        }

        private void mountNav_Interact(object sender, MouseButtonEventArgs e) {
            if ((sender as FrameworkElement)?.Tag is GuideDirections dir)
                O.Mount.ControlMotion(dir, e.LeftButton == MouseButtonState.Pressed);
        }

        private void MountGoTo(object sender, RoutedEventArgs e) {
            TryCommand(() => { O.Mount?.GoTo(); });
        }

        private void MountParkButton_Click(object sender, RoutedEventArgs e) {
            if (O.Mount.AtPark==true) O.Mount.Unpark();
            else if (O.Mount.AtPark==false) O.Mount.Park();
        }

        #endregion

        #region DOME

        private async void domeSlewButton_Click(object sender, RoutedEventArgs e) {
            if ((sender as Button)?.Tag is GuideDirections d) {
                await O.Dome.Slew((d == GuideDirections.guideEast ? -1 : 1) * DomePulseSlider.CustomValue);
            } else {
                await O.Dome.StopAllMotion(); // TODO: Is this really necessary?
                await O.Default.UnSlaveDomeFromMount();
                await O.Dome.StopAllMotion();
            }
        }

        private void domeControlButton_Click(object sender, RoutedEventArgs e) {
            TryCommand(() => {
                if (sender.Equals(DomeOpenButton))
                    O.Dome.Shutter(true);
                else if (sender.Equals(DomeCloseButton))
                    O.Dome.Shutter(false);
                else if (sender.Equals(DomeSlaveButton))
                    O.Slave();
                else if (sender.Equals(DomeParkButton))
                    O.Dome.HomeOrPark(home: false);
                else if (sender.Equals(DomeHomeButton))
                    O.Dome.HomeOrPark(home: true);
            });
        }

        #endregion

        #region CAMERAS

        private void Button_Click_1(object sender, RoutedEventArgs e) {
            foreach (var m in O.CamModels) {
                m.Cam.StartLivePreview(5);
            }
        }

        #endregion

        #region POWER

        private async void HardwareControlButton_Click(object sender, RoutedEventArgs e) {
            if (sender is Button b && b.DataContext is BaseHardwareControl h && b.Tag is HardwareControlButtons tag) {
                b.IsEnabled = false;
                switch (tag) {
                    case HardwareControlButtons.On:    await h.On();    break;
                    case HardwareControlButtons.Off:   await h.Off();   break;
                    case HardwareControlButtons.Reset:
                        if (await h.Reboot(TimeSpan.FromSeconds(5))) // TODO: Unhardcode
                            await Connect(h);
                        break;

                    case HardwareControlButtons.Connect: await Connect(h); break;
                    case HardwareControlButtons.Disconnect: await h.Disconnect(); break;
                }
                b.IsEnabled = true;
            }

        }

        #endregion

        private void SettingsMenu_Click(object sender, RoutedEventArgs e) {
            SettingsWindow.Show();
            SettingsWindow.Activate();
        }
        private void CamerasMenu_Click(object sender, RoutedEventArgs e) {
            CamerasWindow.Show();
            CamerasWindow.Closed += (o, args) => {
                _camerasWindow = null;
            };
            SettingsWindow.Activate();
        }

        private void RunUntilButton_Click(object sender, RoutedEventArgs e) {
            CamerasWindow.Show();
            O.Default.StartingRaise(new Observatory.StartupArguments {
                CloseAt = ShutdownTime.Value
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            O.Default.Interrupt();
            O.Default.ShuttingRaise();
        }

        private void AutomationControlButton_Click(object sender, RoutedEventArgs e) {
            if (sender is Button b && b.Tag is AutomationControlButtons tag) {
                switch (tag) {
                    case AutomationControlButtons.Run:
                        CamerasWindow.Show();
                        O.Default.StartingRaise();
                        break;
                    case AutomationControlButtons.RunUntil
                        when ShutdownTime.Value is DateTime st:
                        CamerasWindow.Show();
                        if (O.Default.AutomationState == Observatory.AutomationStates.InOperation)
                            O.Default.SetShutdown(st);
                        else O.Default.StartingRaise(new Observatory.StartupArguments {
                            CloseAt = st
                        });
                        break;
                    case AutomationControlButtons.Stop:
                        O.Default.Interrupt();
                        O.Default.ShuttingRaise();
                        break;
                }
            }
        }

        private async void Autoexposure_Click(object sender, RoutedEventArgs e) {
            if (O.CamModels.Count > 0 &&
                await O.CamModels[0].Cam.Capture(BaseCamera.Priority.Tracking, copy: true)
                    is CameraImage img) {
                var npimg = img.to_numpy();
                var level = O.CamModels[0].AutoLevel;
                if (npimg != null) {
                    dynamic ret = null;
                    Py.Run(() => {
                        switch (O.CamModels[0].AutoMode) {
                            case AutoExposureModes.max:
                                ret = Py.lib.expcorr_max(npimg, level);
                                break;
                            case AutoExposureModes.mean:
                                ret = Py.lib.expcorr_mean(npimg, level);
                                break;
                            case AutoExposureModes.percentile:
                                ret = Py.lib.expcorr_level(npimg, level, 95);
                                break;
                        }
                    });
                }
            } else {
                MessageBox.Show("Could not capture image");
            }
        }
    }
}
