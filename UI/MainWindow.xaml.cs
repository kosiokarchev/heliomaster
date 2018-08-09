using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            var dir = Telescope.dirs[(sender as FrameworkElement)?.Name];

            if (e.LeftButton == MouseButtonState.Pressed)
                O.Mount.ControlMotion(dir);
            else
                O.Mount.ControlMotion(dir, false);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e) {
            O.Mount?.GoTo();
        }

        private void mountPark_Click(object sender, RoutedEventArgs e)
        {
            TryCommand(() => { O.Mount?.HandlePark(); });
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
            if (sender.Equals(DomePlusButton))
                await O.Dome.PulseMove(DomePulseSlider.CustomValue);
            else if (sender.Equals(DomeMinusButton))
                await O.Dome.PulseMove(-DomePulseSlider.CustomValue);
            else if (sender.Equals(DomeStopButton))
                O.Dome.StopAllMotion();
        }

        private void domeControlButton_Click(object sender, RoutedEventArgs e) {
            TryCommand(() => {
                if (sender.Equals(DomeOpenButton))
                    O.Dome.Shutter(true);
                else if (sender.Equals(DomeCloseButton))
                    O.Dome.Shutter(false);
                else if (sender.Equals(DomeSlaveButton))
                    O.Dome.Slave(!O.Dome.Slaved);
                else if (sender.Equals(DomeParkButton))
                    O.Dome.Park();
                else if (sender.Equals(DomeHomeButton))
                    O.Dome.Home();
            });
        }

//        private void domeButton_Click(object sender, RoutedEventArgs e) {
//            O.Dome?.StopAllMotion();
//        }
//
//        private bool domeButton_MouseIsGoingDown;
//        private bool domeButton_MouseIsDown;
//        private void domeButton_MouseDown(object sender, MouseButtonEventArgs e) {
//            domeButton_MouseIsGoingDown = true;
//            Task.Run(async () => {
//                await Task.Delay(500);
//                if (domeButton_MouseIsGoingDown) {
//                    domeButton_MouseIsGoingDown = false;
//                    domeButton_MouseIsDown = true;
//                    O.Dome?.SetInMotion(sender.Equals(domeRight) ? Dome.MotionState.MovingRight : Dome.MotionState.Movingleft);
//                }
//            });
//        }
//
//        private void domeButton_MouseUp(object sender, MouseButtonEventArgs e) {
//            if (domeButton_MouseIsDown)
//                O.Dome?.StopAllMotion();
//            else if (domeButton_MouseIsGoingDown)
//                O.Dome?.SetInMotion(sender.Equals(domeRight) ? Dome.MotionState.MovingRight : Dome.MotionState.Movingleft);
//            domeButton_MouseIsDown = false;
//            domeButton_MouseIsGoingDown = false;
//        }
//
//        private void domePark_Click(object sender, RoutedEventArgs e) {
//            TryCommand(() => { O.Dome?.Park(); });
//        }
//
//        private void domeHome_Click(object sender, RoutedEventArgs e) {
//            TryCommand(() => { O.Dome?.Home(); });
//        }
//
//        private void domeSlave_Click(object sender, RoutedEventArgs e) {
//            TryCommand(() => { O.Dome?.Slave(!O.Dome.Slaved); });
//        }
//
//        private void domeShutter_Click(object sender, RoutedEventArgs e) {
//            TryCommand(() => { O.Dome?.Shutter(sender.Equals(DomeOpenButton)); });
//        }

        #endregion

        #region CAMERAS

        private void Button_Click_1(object sender, RoutedEventArgs e) {
            foreach (var c in O.Cams) {
                Task.Factory.StartNew(async (cam) => {
                    while (true) {
                        if ((cam as BaseCamera)?.Capture(BaseCamera.Priority.LiveView) is Task<CameraImage> t)
                            await t;
                    }
                }, c);
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

        private void SettingsMenu_Click(object sender, RoutedEventArgs e) {
            SettingsWindow.Show();
        }
        private void CamerasMenu_Click(object sender, RoutedEventArgs e) {
            CamerasWindow.Show();
        }
    }
}
