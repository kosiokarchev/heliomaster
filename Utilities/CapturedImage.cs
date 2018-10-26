using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using heliomaster.Properties;

namespace heliomaster {
    /// <summary>
    /// Provides a high-level interface to saving images, transferring them to and processing them on a remote host.
    /// </summary>
    public class CapturedImage : BaseNotify {
        /// <summary>
        /// The object containing the actual image data.
        /// </summary>
        public CameraImage Image { get; set; }
        
        /// <summary>
        /// The path on the local machine to save the image to, and also the file to transfer to the remote host.
        /// </summary>
        public string LocalPath { get; set; }
        
        /// <summary>
        /// The path on the remote host the image has been transferred to.
        /// </summary>
        /// <remarks>Set appropriately by <see cref="Transfer"/></remarks>
        public string RemotePath { get; private set; }


        private bool? _isSaved;
        /// <summary>
        /// Whether the image has already been saved.
        /// </summary>
        /// <remarks>Initially this property is <c>null</c>. The method <see cref="Save"/> sets it to either <c>true</c>
        /// or <c>false</c> depending on the outcome of the saving process. Setting this to <c>true</c> results in the
        /// <see cref="Saved"/> event being raised.</remarks>
        public bool? IsSaved {
            get => _isSaved;
            private set {
                if (value == _isSaved) return;
                _isSaved = value;
                if (value == true && Saved != null)
                    Task.Factory.FromAsync((ac, o) => Saved.BeginInvoke(this, ac, o), Saved.EndInvoke, null);
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Raised when the image has been saved successfully.
        /// </summary>
        public event Action<CapturedImage> Saved;

        private bool? _isTransferred;
        /// <summary>
        /// Whether the image has already been uploaded to the remote host.
        /// </summary>
        /// <remarks>Initially this property is <c>null</c>. The method <see cref="Transfer"/> sets it to either
        /// <c>true</c> or <c>false</c> depending on the outcome of the upload. Setting this to <c>true</c> results in
        /// the <see cref="Transferred"/> event being raised.</remarks>
        public bool? IsTransferred {
            get => _isTransferred;
            private set {
                if (value == _isTransferred) return;
                _isTransferred = value;
                if (value == true && Transferred != null)
                    Task.Factory.FromAsync((ac, o) => Transferred.BeginInvoke(this, ac, o), Transferred.EndInvoke, null);
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Raised when the image has been uploaded successfully.
        /// </summary>
        public event Action<CapturedImage> Transferred;

        private bool? _isProcessed;
        /// <summary>
        /// Whether the image has already been processed by the remote host.
        /// </summary>
        /// <remarks>Initially this property is <c>null</c>. The method <see cref="Process"/> sets it to either
        /// <c>true</c> or <c>false</c> depending on the outcome of the processing. Setting this to <c>true</c>
        /// results in the <see cref="Processed"/> event being raised.</remarks>
        public bool? IsProcessed {
            get => _isProcessed;
            private set {
                if (value == _isProcessed) return;
                _isProcessed = value;
                if (value == true && Processed != null)
                    Task.Factory.FromAsync((ac, o) => Processed.BeginInvoke(this, ac, o), Processed.EndInvoke, null);
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Raised when the image has been processed successfully.
        /// </summary>
        public event Action<CapturedImage> Processed;


        private Transform _transform;
        /// <summary>
        /// A transformation from the raw image to its correct orientation.
        /// </summary>
        /// <remarks>This is set to the <see cref="CameraModel.FinalTransform"/> when an image is taken by
        /// <see cref="CameraModel.TakeImage"/>.</remarks>
        public Transform Transform {
            get => _transform;
            set {
                if (Equals(value, _transform)) return;
                _transform = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Dispose of the <see cref="Image"/>.
        /// </summary>
        public void Dispose() {
            Image.Dispose();
        }

        /// <summary>
        /// Save the image to the file referred to by <see cref="LocalPath"/>.
        /// </summary>
        /// <remarks>
        /// <para>Does nothing if <see cref="IsSaved"/> is <c>true</c>.</para>
        /// <para>This method checks the <see cref="CameraSettings.SaveTransformed"/> setting in <see cref="S.Cameras"/>
        /// to determine whether to pass <see cref="Transform"/> to <see cref="CameraImage.Save"/>. It also creates
        /// all necessary directories through <see cref="Directory.CreateDirectory(string)"/> and catches all exceptions
        /// except those thrown by this function.</para>
        /// </remarks>
        public async void Save() {
            if (IsSaved != true && Path.GetDirectoryName(LocalPath) is string dirname) {
                Directory.CreateDirectory(dirname);
                try { IsSaved = await Image.Save(LocalPath, t:S.Cameras.SaveTransformed ? Transform : null);}
                catch { IsSaved = false; }
            }
        }

        /// <summary>
        /// Transfer the image to the remote host configured on <paramref name="uploader"/> under the filename
        /// <paramref name="path"/>.
        /// </summary>
        /// <param name="uploader">The uploader to use. The remote host should be configured in this parameter.</param>
        /// <param name="path">The path on the remote host to upload to.</param>
        /// <remarks>
        /// <para>Does nothing if the image has not been saved yet (<see cref="IsSaved"/> is not <c>true</c>).</para>
        /// <para>Calls <paramref name="uploader"/><c>.Upload(</c><see cref="LocalPath"/><c>, </c>
        /// <paramref name="path"/><c>)</c>, and if successful saves <paramref name="path"/> into
        /// <see cref="RemotePath"/>.</para>
        /// </remarks>
        public async void Transfer(IUploader uploader, string path) {
            if (IsSaved==true)
                try {
                    await uploader.Upload(LocalPath, path);
                    IsTransferred = true;
                    RemotePath = path;
                } catch { IsTransferred = false; }
        }

        /// <summary>
        /// Runs the command <paramref name="cmd"/> on the <paramref name="remote"/>.
        /// </summary>
        /// <param name="remote">The remote host controller to use to execute <paramref name="cmd"/>.</param>
        /// <param name="cmd">the command to execute on the remote host.</param>
        /// <remarks>Calls <paramref name="remote"/><c>.Execute(</c><paramref name="cmd"/><c>)</c>, and sets
        /// <see cref="IsProcessed"/> to <c>true</c> if the command has an <see cref="Command.ExitCode"/> of 0
        /// and to <c>false</c> otherwise.</remarks>
        public async void Process(Remote remote, string cmd) {
            var command = await remote.Execute(cmd);
            IsProcessed = command.ExitCode != null && (int) command.ExitCode == 0;
        }
    }
}
