using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using heliomaster_wpf.Properties;

namespace heliomaster_wpf
{
    /// <summary>
    /// Interaction logic for CamerasWindow.xaml
    /// </summary>
    public partial class CamerasWindow : HMWindow {
        private CommonTimelapse _timelapse;
        public CommonTimelapse Timelapse {
            get => _timelapse;
            set {
                if (Equals(value, _timelapse)) return;
                _timelapse = value;
                OnPropertyChanged();
            }
        }

        public CamerasWindow() {
            CreateCameras(true);
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e) {
            foreach (var model in Models) {
                model.Cam.Disconnect();
                model.Images.Clear();
            }
            O.CamModels.Clear();
            base.OnClosing(e);
        }

        public ObservableCollection<CameraModel> Models { get; } = O.CamModels;

        private async void CreateCameras(bool _) {
            foreach (var model in S.Cameras.CameraModels) {
                var cam = BaseCamera.Create(model.CameraType);
                if (await cam.Connect(model.CameraID)) {
//                    O.CamModels.Add(cam);
                    model.Cam = cam;

                    O.Refresh += cam.RefreshRaise;

                    Models.Add(model);

                    if (await cam.Focuser.Connect(model.FocuserID)
                        && cam.Focuser.Absolute != null
                        && (bool) cam.Focuser.Absolute)
                        O.Refresh += cam.Focuser.RefreshRaise;
                } else
                    MessageBox.Show($"Connecting to camera {model.CameraID} failed.");
            }

            if (Models.Count < 1) return;

            Timelapse = new CommonTimelapse(new Timelapse {
                StopMethod = S.Settings.timelapseStopMethod,
                Interval   = S.Settings.timelapseInterval,
                Nshots     = S.Settings.timelapseNshots,
            }, Models.Count);
            for (var i = 0; i < Models.Count; ++i) {
                Models[i].Timelapse = Timelapse[i];
            }
        }


        #region UI

        private void MainRadio_Checked(object sender, RoutedEventArgs e) {
            var dc = ((RadioButton)sender).DataContext;
            if (dc != null)
                Timelapse.i0 = ((CameraModel) dc).Index;
        }

        private void TiedRadio_Checked(object sender, RoutedEventArgs e) {
            var cb = (CheckBox) sender;
            var dc = cb.DataContext;
            if (dc != null && cb.IsChecked != null) {
                if (((CameraModel) dc).Index != Timelapse.i0)
                    Timelapse.SetTied(((CameraModel) dc).Index, (bool) cb.IsChecked);
                else
                    cb.IsChecked = true;
            }
        }

        private void timelapseButton_Click(object sender, RoutedEventArgs e) {
            int i;
            if (((Button) sender).DataContext is Timelapse t
                && (i = Timelapse.IndexOf(t)) > -1) {
                if (!t.Running) {
                    if (Timelapse.Tied[i])
                        Timelapse.Start(CameraModel.TimelapseAction, Models);
                    else
                        t.Start(CameraModel.TimelapseAction, Models[i]);
                } else {
                    if (Timelapse.Tied[i])
                        Timelapse.Stop();
                    else
                        t.Stop();
                }
            }
        }

        private void captureButton_Click(object sender, RoutedEventArgs e) {
            int i;
            if (((Button) sender).DataContext is Timelapse t && (i = Timelapse.IndexOf(t)) > -1)
                Task.Factory.StartNew(CameraModel.TimelapseAction, Models[i]);
        }

        private void focuserButton_Click(object sender, RoutedEventArgs e) {
            var foc = (Focuser) ((Button) sender).DataContext;
            foc.Nudge(((Button) sender).Name == "FocuserUpButton");
        }

        #endregion
    }
}
