using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace heliomaster
{
    /// <summary>
    /// Interaction logic for CamerasWindow.xaml
    /// </summary>
    public partial class CamerasWindow : HMWindow {
        public CamerasWindow() {
            InitializeComponent();
            O.Default.ConnectCameras();
        }

        protected override async void OnClosing(CancelEventArgs e) {
            foreach (var model in O.CamModels) {
                O.Timelapse.Stop();
                await model.Cam.Stop();
                model.Images.Clear();
            }
            O.CamModels.Clear();
            base.OnClosing(e);
        }

        #region UI

        private void MainRadio_Checked(object sender, RoutedEventArgs e) {
            var dc = ((RadioButton)sender).DataContext;
            if (dc != null)
                O.Timelapse.i0 = ((CameraModel) dc).Index;
        }

        private void TiedRadio_Checked(object sender, RoutedEventArgs e) {
            var cb = (CheckBox) sender;
            var dc = cb.DataContext;
            if (dc != null && cb.IsChecked != null) {
                if (((CameraModel) dc).Index != O.Timelapse.i0)
                    O.Timelapse.SetTied(((CameraModel) dc).Index, (bool) cb.IsChecked);
                else
                    cb.IsChecked = true;
            }
        }

        private void timelapseButton_Click(object sender, RoutedEventArgs e) {
            int i;
            if (((Button) sender).DataContext is Timelapse t
                && (i = O.Timelapse.IndexOf(t)) > -1) {
                if (!t.Running) {
                    if (O.Timelapse.Tied[i])
                        O.Timelapse.Start(CameraModel.TimelapseAction, O.CamModels);
                    else
                        t.Start(CameraModel.TimelapseAction, O.CamModels[i]);
                } else {
                    if (O.Timelapse.Tied[i])
                        O.Timelapse.Stop();
                    else
                        t.Stop();
                }
            }
        }

        private void captureButton_Click(object sender, RoutedEventArgs e) {
            int i;
            if (((Button) sender).DataContext is Timelapse t && (i = O.Timelapse.IndexOf(t)) > -1)
                Task.Factory.StartNew(CameraModel.TimelapseAction, O.CamModels[i]);
        }

        private void focuserButton_Click(object sender, RoutedEventArgs e) {
            var foc = (Focuser) ((Button) sender).DataContext;
            foc.Nudge(((Button) sender).Name == "FocuserUpButton");
        }

        #endregion
    }
}
