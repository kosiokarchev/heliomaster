using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using ASCOM;
using ASCOM.DeviceInterface;
using ASCOM.DriverAccess;
using heliomaster.Annotations;

namespace heliomaster {
    public class FileParsingException : Exception {
        public FileParsingException() { }
        public FileParsingException(string message) : base(message) { }
    }

    public abstract class FileOC : AscomDriver, IObservingConditions, INotifyPropertyChanged {
        public void SetupDialog() { throw new System.NotImplementedException(); }
        public string Action(string ActionName, string ActionParameters) => throw new System.NotImplementedException();
        public void CommandBlind(string Command, bool Raw = false) { throw new System.NotImplementedException(); }
        public bool CommandBool(string Command, bool Raw = false) => throw new System.NotImplementedException();
        public string CommandString(string Command, bool Raw = false) => throw new System.NotImplementedException();
        public ArrayList SupportedActions { get; set; } = new ArrayList();

        public string Description { get; }
        public string DriverInfo => $"File reader Observing conditions driver v{DriverVersion}";

        public string DriverVersion { get; } = "0.1";
        public short  InterfaceVersion { get; } = 3;
        public string SensorDescription(string PropertyName) => throw new System.NotImplementedException();


        private Timer timer;
        private bool _connected;
        public new bool Connected {
            get => _connected;
            set {
                var val = false;
                if (value)
                    try {
                        parse(File.ReadAllText(Description));
                        val = true;
                        timer = new Timer(o => Refresh(), null, TimeSpan.Zero, TimeSpan.FromSeconds(AveragePeriod));
                    } catch {
                        throw new DriverException();
                    }
                else Dispose();

                if (_connected.Equals(val)) return;
                _connected = val;
                OnPropertyChanged();
            }
        }


        protected DateTime lastUpdateTime;
        public double TimeSinceLastUpdate(string PropertyName) => (DateTime.Now - lastUpdateTime).TotalSeconds;

        protected abstract bool parse(string file);
        public async void Refresh() {
            if (!Connected) return;

            var lastUpdateTimeBackup = lastUpdateTime;
            lastUpdateTime = File.GetLastWriteTime(Description);
            try {
                using (var f = File.OpenText(Description))
                    if (!parse(await f.ReadToEndAsync()))
                        throw new FileParsingException();
            } catch {
                lastUpdateTime = lastUpdateTimeBackup;
            }
        }


        public new void Dispose() {
            _cloudCover = _dewPoint = _humidity = _pressure = _rainRate = _skyBrightness = _skyQuality
                = _skyTemperature = _starFWHM = _temperature = _windDirection = _windGust = _windSpeed
                    = null;

            timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            timer?.Dispose();
            timer = null;
        }
        ~FileOC() => Dispose();


        public new string Name { get; } = "FileOC";


        private double _averagePeriod = 10; // TODO: Unhardcode
        public double AveragePeriod {
            get => _averagePeriod;
            set {
                if (value.Equals(_averagePeriod)) return;
                _averagePeriod = value;

                timer?.Change(TimeSpan.Zero, TimeSpan.FromHours(_averagePeriod));

                OnPropertyChanged();
            }
        }

        protected double? _cloudCover;
        public double CloudCover {
            get => _cloudCover ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_cloudCover)) return;
                _cloudCover = value;
                OnPropertyChanged();
            }
        }

        protected double? _dewPoint;
        public double DewPoint {
            get => _dewPoint ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_dewPoint)) return;
                _dewPoint = value;
                OnPropertyChanged();
            }
        }

        protected double? _humidity;
        public double Humidity {
            get => _humidity ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_humidity)) return;
                _humidity = value;
                OnPropertyChanged();
            }
        }

        protected double? _pressure;
        public double Pressure {
            get => _pressure ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_pressure)) return;
                _pressure = value;
                OnPropertyChanged();
            }
        }

        protected double? _rainRate;
        public double RainRate {
            get => _rainRate ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_rainRate)) return;
                _rainRate = value;
                OnPropertyChanged();
            }
        }

        protected double? _skyBrightness;
        public double SkyBrightness {
            get => _skyBrightness ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_skyBrightness)) return;
                _skyBrightness = value;
                OnPropertyChanged();
            }
        }

        protected double? _skyQuality;
        public double SkyQuality {
            get => _skyQuality ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_skyQuality)) return;
                _skyQuality = value;
                OnPropertyChanged();
            }
        }

        protected double? _starFWHM;
        public double StarFWHM {
            get => _starFWHM ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_starFWHM)) return;
                _starFWHM = value;
                OnPropertyChanged();
            }
        }

        protected double? _skyTemperature;
        public double SkyTemperature {
            get => _skyTemperature ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_skyTemperature)) return;
                _skyTemperature = value;
                OnPropertyChanged();
            }
        }

        protected double? _temperature;
        public double Temperature {
            get => _temperature ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_temperature)) return;
                _temperature = value;
                OnPropertyChanged();
            }
        }

        protected double? _windDirection;
        public double WindDirection {
            get => _windDirection ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_windDirection)) return;
                _windDirection = value;
                OnPropertyChanged();
            }
        }

        protected double? _windGust;
        public double WindGust {
            get => _windGust ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_windGust)) return;
                _windGust = value;
                OnPropertyChanged();
            }
        }

        protected double? _windSpeed;
        public double WindSpeed {
            get => _windSpeed ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_windSpeed)) return;
                _windSpeed = value;
                OnPropertyChanged();
            }
        }


        public FileOC(string fname) {
            Description = fname;
        }


        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BoltwoodFileOC : FileOC {
        public BoltwoodFileOC(string fname) : base(fname) { }

        private static double ToDouble(string s) {
            return double.Parse(s, CultureInfo.InvariantCulture);
        }
        private static double temp(string name, GroupCollection g) {
            var val = ToDouble(g[name].Value);
            return g["Tunit"].Value == "C" ? val : val * 9.0/5.0 + 32;
        }
        private static double speed(string name, GroupCollection g) {
            var val = ToDouble(g[name].Value);
            return g["Vunit"].Value == "m"   ? val
                   : g["Vunit"].Value == "M" ? (1760 * 36 * 0.0254 / 3600) * val
                                               : (1000.0 / 3600.0) * val;
        }

        private static readonly Regex re = new Regex(
            @"(?<Date>\d\d\d\d-\d\d-\d\d)\s+(?<Time>\d\d:\d\d:\d\d.\d\d)\s+(?<Tunit>[CF])\s+(?<Vunit>[KMm])\s+(?<SkyTemperature>[-.\d]*)\s+(?<Temperature>[-.\d]*)\s+(?<SensorTemperature>[-.\d]*)\s+(?<Wind>[-.\d]*)\s+(?<Humidity>[-.\d]*)\s+(?<DewPoint>[-.\d]*)\s+(?<Heater>[-.\d]*)\s+(?<RainFlag>[012]*)\s+(?<WetFlag>[012]*)\s+(?<Since>[-.\d]*)\s+(?<Now>[-.\d]*)\s+(?<CloudCond>[0123])\s+(?<WindCond>[0123])\s+(?<RainCond>[0123])\s+(?<DayCond>[0123])\s*(?<Roof>[01]*)\s*(?<Alert>[01]*)",
            RegexOptions.Compiled | RegexOptions.Singleline);
        protected override bool parse(string file) {
            if (re.Match(file) is Match m && m.Success) {
                var dts = $"{(m.Groups["Date"])} {(m.Groups["Time"])}";
                lastUpdateTime =
                    DateTime.ParseExact(dts, "yyyy-MM-dd HH':'mm':'ss.ff", CultureInfo.InvariantCulture)
                    - TimeSpan.FromSeconds(int.Parse(m.Groups["Since"].Value));
                SkyTemperature = temp("SkyTemperature", m.Groups);
                Temperature    = temp("Temperature",    m.Groups);
                WindSpeed      = speed("Wind", m.Groups);
                Humidity       = ToDouble(m.Groups["Humidity"].Value);
                DewPoint       = temp("DewPoint", m.Groups);
                CloudCover     = m.Groups["CloudCond"].Value == "1"   ? 0
                                 : m.Groups["CloudCond"].Value == "2" ? 50
                                 : m.Groups["CloudCond"].Value == "3" ? 100 : double.NaN;
                RainRate       = (m.Groups["RainFlag"].Value == "0"
                                  && (m.Groups["RainCond"].Value == "0" || m.Groups["RainCond"].Value == "1"))
                                     ? 0 : 10;
                SkyBrightness  = m.Groups["DayCond"].Value == "1"   ? 100
                                 : m.Groups["DayCond"].Value == "2" ? 1000
                                 : m.Groups["DayCond"].Value == "3" ? 10000 : double.NaN;
                return true;
            } else {
                return false;
            }
        }
    }
}
