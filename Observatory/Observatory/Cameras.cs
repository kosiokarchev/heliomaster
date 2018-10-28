using System.Threading.Tasks;
using System.Windows;
using heliomaster.Properties;

namespace heliomaster {
    public partial class Observatory {
        /// <summary>
        /// Initialises <see cref="CameraModels"/> from the <see cref="CameraSettings.CameraModels"/> setting of
        /// <see cref="S.Cameras"/>.
        /// </summary>
        /// <remarks>
        /// <para>For each <see cref="CameraModel"/> uses <see cref="BaseCamera.Create"/> to create a camera of the
        /// appropriate <see cref="CameraTypes"/> given in <see cref="CameraModel.CameraType"/> and attempts to connect
        /// to it. If successful, registers the camera with the global refresh event (<see cref="O.Refresh"/>),
        /// connects to the camera's <see cref="BaseCamera.Focuser"/> if <see cref="CameraModel.FocuserID"/> is set,
        /// and adds the model to <see cref="CameraModels"/>.</para>
        /// <para>Initialises the <see cref="CommonTimelapse"/> with the appropriate number of <see cref="Timelapse"/>s
        /// with the default settings from <see cref="Settings"/>.</para>
        /// </remarks>
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

        /// <summary>
        /// Disconnect from all cameras, free the images taken with them, stop the <see cref="CommonTimelapse"/> and
        /// clear the <see cref="CameraModels"/> collection.
        /// </summary>
        public async void DisconnectCameras() {
            foreach (var model in CameraModels) {
                O.Refresh -= model.Cam.RefreshRaise;
                await model.Cam.Disconnect();
                model.Images.Clear();
            }
            CommonTimelapse.Stop();

            CameraModels.Clear();
        }
    }
}
