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
using heliomaster_wpf.Netio;
using Drivers = ASCOM.DriverAccess;
using heliomaster_wpf.Properties;
using Microsoft.Win32;

namespace heliomaster_wpf {
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

        #region MOUNT

        private async void Button_Click_2(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(S.Mount.MountID)) return;
            if (!await O.Mount.Connect(S.Mount.MountID))
                MessageBox.Show($"Connecting to camera {S.Mount .MountID} failed.");
        }

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
            e.Handled = true;
            TryCommand(() => {
                var isChecked = ((CheckBox) sender).IsChecked;
                O.Mount?.Track(isChecked.HasValue && isChecked.Value);
            });
        }

        #endregion

        #region DOME

        private async void Button_Click_3(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(S.Dome.DomeID)) return;
            if (!await O.Dome.Connect(S.Dome.DomeID))
                MessageBox.Show($"Connecting to dome {S.Dome.DomeID} failed.");
        }

        private async void domeSlewButton_Click(object sender, RoutedEventArgs e) {
            if ((sender as Button)?.Tag is GuideDirections d) {
                await O.Dome.Slew((d == GuideDirections.guideEast ? -1 : 1) * DomePulseSlider.CustomValue);
            } else {
                O.Default.UnSlaveDomeFromMount();
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

        #region WEATHER

        private async void Button_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(S.Weather.WeatherID)) return;
            if (!await O.Weather.Connect(S.Weather.WeatherID))
                MessageBox.Show($"Connecting to weather {S.Weather.WeatherID} failed.");
        }

        #endregion


        private void SettingsMenu_Click(object sender, RoutedEventArgs e) {
            SettingsWindow.Show();
        }
        private void CamerasMenu_Click(object sender, RoutedEventArgs e) {
            CamerasWindow.Show();
            CamerasWindow.Closed += (o, args) => {
                _camerasWindow = null;
            };
        }


//        private async void Button_Click_5(object sender, RoutedEventArgs e) {
////            var fdialog = new OpenFileDialog();
////            if (fdialog.ShowDialog() == true) {
////                var res = await O.Uploader.Upload(fdialog.FileName, "/sun_monitor/"+Path.GetFileName(fdialog.FileName));
////                Console.WriteLine(res.Success);
////                Console.WriteLine(res.StatusDescription);
////                if (!res.Success && res.Error != null)
////                    throw res.Error;
////            }
//            var fdialog = new OpenFileDialog();
//            if (fdialog.ShowDialog() == true)
//                await O.Remote.Upload(fdialog.FileName, "/home/sun_monitor/ftp_publiic/"+Path.GetFileName(fdialog.FileName));
//
////            var cmd = await O.Remote.Execute("uptime");
////            Console.Write(cmd.cmd.Result);
////            Console.Write(cmd.cmd.ExitStatus);
//        }



        private async void Button_Click_4(object sender, RoutedEventArgs e) {
            var n = new Netio.Power {
                UseHttps = true,
                Host = "10.66.180.60"
            };

            var a = await n.Get();
            var b = 1;
        }

        private async void Button_Click_5(object sender, RoutedEventArgs e) {
            var p = new Netio.Power {
                UseHttps = true,
                Host = "10.66.180.60",
                User = "ceso",
                PassString = "ESACvilspa01"
            };

            S.Power.Netio = p;
            S.Save();

            var names = p.Names;

            p.Register(O.Dome, "dome");
            var t = p.Toggle(O.Dome);
            t.Wait();
            Console.WriteLine(t.Result.On);
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
