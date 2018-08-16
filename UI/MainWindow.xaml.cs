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

namespace heliomaster {
    public enum HardwareControlButtons {
        On, Off, Reset, Connect, Disconnect
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
            O.Weather.SaveInSettings(S.Weather);
            S.Save();
        }

        private async Task Connect(BaseHardwareControl h, string id = null) {
            id = id ?? (h is Dome   ? S.Dome.DomeID :
                     h is Telescope ? S.Mount.MountID :
                     h is Weather   ? S.Weather.WeatherID : null);
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
            if (O.Mount.AtPark) O.Mount.Unpark();
            else O.Mount.Park();
        }

        private void mountTracking_Checked(object sender, RoutedEventArgs e) {
            // TODO: Implement sophisticated tracking!
            e.Handled = true;
            TryCommand(() => {
                var isChecked = ((CheckBox) sender).IsChecked;
                O.Mount?.Track(isChecked.HasValue && isChecked.Value);
            });
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
                        if (h.Valid) {
                            await h.Disconnect();
                            await h.Reset();
                            await Connect(h);
                        } else await h.Reset();

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


        private void Button_Click_6(object sender, RoutedEventArgs e) {
            CamerasWindow.Show();
            O.Default.StartingRaise();
        }

        private void Button_Click_7(object sender, RoutedEventArgs e) {
            O.Default.Interrupt();
            O.Default.ShuttingRaise();
        }
    }
}
