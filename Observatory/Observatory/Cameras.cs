using System.Threading.Tasks;
using heliomaster.Properties;

namespace heliomaster {
    public partial class Observatory {
        public async Task ConnectCameras() {
            DisconnectCameras();

            foreach (var model in S.Cameras.CameraModels) {
                var cam = BaseCamera.Create(model.CameraType);
                if (await cam.Connect(model.CameraID)) {
                    model.Cam             = cam;
                    model.Cam.DisplayName = model.Name;

                    O.Refresh += cam.RefreshRaise;

                    CameraModels.Add(model);

                    if (await cam.Focuser.Connect(model.FocuserID)
                        && cam.Focuser.Absolute != null
                        && (bool) cam.Focuser.Absolute)
                        O.Refresh += cam.Focuser.RefreshRaise;
                } else
                    MessageBox.Show($"Connecting to camera {model.CameraID} failed.");
            }

            if (CameraModels.Count < 1) return;

            CommonTimelapse.Make(
                new Timelapse {
                    StopMethod = S.Settings.timelapseStopMethod,
                    Interval   = S.Settings.timelapseInterval,
                    Nshots     = S.Settings.timelapseNshots,
                }, CameraModels.Count);
            for (var i = 0; i < CameraModels.Count; ++i) {
                CameraModels[i].Timelapse = CommonTimelapse[i];
            }
        }

        public async void DisconnectCameras() {
            foreach (var exmodel in CameraModels) {
                O.Refresh -= exmodel.Cam.RefreshRaise;
                exmodel.Images.Clear();
                CommonTimelapse.Stop();
                await exmodel.Cam.Disconnect();
            }

            CameraModels.Clear();
        }
    }
}