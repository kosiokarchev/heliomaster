﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Newtonsoft.Json;
using SmartFormat;

namespace heliomaster_wpf {
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class CameraModel : BaseNotify {
        private int _index;
        public int Index {
            get => _index;
            set {
                if (value == _index) return;
                _index = value;
                OnPropertyChanged();
            }
        }

        private string _cameraID;
        public string CameraID {
            get => _cameraID;
            set {
                if (value == _cameraID) return;
                _cameraID = value;
                OnPropertyChanged();
            }
        }

        private CameraTypes _cameraType;
        public CameraTypes CameraType {
            get => _cameraType;
            set {
                if (value == _cameraType) return;
                _cameraType = value;
                OnPropertyChanged();
            }
        }

        private string _name;
        public string Name {
            get => _name;
            set {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        private string _localPathFormat;
        public string LocalPathFormat {
            get => _localPathFormat;
            set {
                if (value == _localPathFormat) return;
                _localPathFormat = value;
                OnPropertyChanged();
            }
        }

        private string _remotePathFormat;
        public string RemotePathFormat {
            get => _remotePathFormat;
            set {
                if (value == _remotePathFormat) return;
                _remotePathFormat = value;
                OnPropertyChanged();
            }
        }

        private string _remoteCommandFormat;
        public string RemoteCommandFormat {
            get => _remoteCommandFormat;
            set {
                if (value == _remoteCommandFormat) return;
                _remoteCommandFormat = value;
                OnPropertyChanged();
            }
        }

        private string _focuserID;
        public string FocuserID {
            get => _focuserID;
            set {
                if (value == _focuserID) return;
                _focuserID = value;
                OnPropertyChanged();
            }
        }

        private bool _flip;
        public bool Flip {
            get => _flip;
            set {
                if (value == _flip) return;
                _flip = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Transform));
            }
        }

        private int _rotate;
        public int Rotate {
            get => _rotate;
            set {
                if (value == _rotate) return;
                _rotate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Transform));
                OnPropertyChanged(nameof(FinalTransform));
            }
        }

        private bool _isFlipped;
        public bool IsFlipped {
            set {
                if (_isFlipped == value) return;
                _isFlipped = value;
                OnPropertyChanged(nameof(FinalTransform));
            }
        }
        public Transform Transform => new TransformGroup {
            Children = new TransformCollection(new Transform[] {
                new ScaleTransform(Flip ? -1 : 1, 1),
                new RotateTransform(-Rotate * 90)
            })
        };
        public Transform FinalTransform => O.Mount.IsFlipped
            ? new TransformGroup {Children = new TransformCollection(new[] {
                Transform,
                new ScaleTransform(-1, -1)})}
            : Transform;

        private BaseCamera _cam;
        [XmlIgnore] public BaseCamera Cam {
            get => _cam;
            set {
                if (Equals(value, _cam)) return;
                _cam = value;
                OnPropertyChanged();
            }
        }

        private Timelapse _timelapse;
        [XmlIgnore] public Timelapse Timelapse {
            get => _timelapse;
            set {
                if (Equals(value, _timelapse)) return;
                _timelapse = value;
                OnPropertyChanged();
            }
        }

        [XmlIgnore] public ObservableCollection<CapturedImage> Images { get; } = new ObservableCollection<CapturedImage>();

        public CameraModel() {
            O.Refresh += () => IsFlipped = O.Mount.IsFlipped;
        }
        
        public static async void TimelapseAction(object state) {
            if (state is CameraModel m)
                Console.WriteLine(await m.TakeImage());
        }

        public async Task<CapturedImage> TakeImage() {
            var img  = await Cam.Capture();
            var cimg = new CapturedImage {
                Image = img,
                LocalPath = Smart.Format(LocalPathFormat, new {
                    Cam = Name,
                    DateTime = DateTime.UtcNow,
                })
            };

            cimg.Saved += c => {
                c.Transfer(O.Remote, Smart.Format(RemotePathFormat, new {
                    Cam           = Name,
                    DateTime      = DateTime.UtcNow,
                    LocalPath     = c.LocalPath,
                    LocalBaseName = Path.GetFileName(c.LocalPath),
                }));
            };
            cimg.Transferred += c => {
                c.Process(O.Remote, Smart.Format(RemoteCommandFormat, new {
                    Cam            = Name,
                    DateTime       = DateTime.UtcNow,
                    LocalPath      = c.LocalPath,
                    LocalBaseName  = Path.GetFileName(c.LocalPath),
                    RemoteBaseName = Path.GetFileName(c.RemotePath),
                    RemotePath     = c.RemotePath
                }));
            };

            cimg.Save();
            Application.Current.Dispatcher.InvokeAsync(() => { Images.Add(cimg); });
            return cimg;
        }
    }
}