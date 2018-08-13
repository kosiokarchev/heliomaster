using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace heliomaster_wpf {
    public class CapturedImage : BaseNotify {
        public CameraImage Image { get; set; }
        public string LocalPath { get; set; }
        public string RemotePath { get; set; }


        private bool? _isSaved;
        public bool? IsSaved {
            get => _isSaved;
            private set {
                if (value == _isSaved) return;
                _isSaved = value;
                if (value == true) Saved?.Invoke(this);
                OnPropertyChanged();
            }
        }
        public event Action<CapturedImage> Saved;

        private bool? _isTransferred;
        public bool? IsTransferred {
            get => _isTransferred;
            private set {
                if (value == _isTransferred) return;
                _isTransferred = value;
                if (value == true) Transferred?.Invoke(this);
                OnPropertyChanged();
            }
        }
        public event Action<CapturedImage> Transferred;

        private bool? _isProcessed;
        public bool? IsProcessed {
            get => _isProcessed;
            private set {
                if (value == _isProcessed) return;
                _isProcessed = value;
                if (value == true) Processed?.Invoke(this);
                OnPropertyChanged();
            }
        }
        public event Action<CapturedImage> Processed;


        private Transform _transform;
        public Transform Transform {
            get => _transform;
            set {
                if (Equals(value, _transform)) return;
                _transform = value;
                OnPropertyChanged();
            }
        }

        public async void Save() {
            if (IsSaved != true && Path.GetDirectoryName(LocalPath) is string dirname)
                try {
                    Directory.CreateDirectory(dirname);
                    IsSaved = await Image.Save(LocalPath, t:Transform);
                } catch (Exception e) {
                    if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)) throw;
                }
        }

        public async void Transfer(IUploader uploader, string path) {
            if (IsSaved==true) {
                try {
                    await uploader.Upload(LocalPath, path);
                    IsTransferred = true;
                    RemotePath = path;
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                    IsTransferred = false;
                }
            }
        }

        public async void Process(Remote remote, string cmd) {
            await Task.Delay(1000);
            var command = await remote.Execute(cmd);
            IsProcessed = command.ExitCode != null && (int) command.ExitCode == 0;
        }
    }
}
