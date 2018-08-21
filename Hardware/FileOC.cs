using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using ASCOM;
using ASCOM.DriverAccess;
using heliomaster.Annotations;

namespace heliomaster {
    public abstract class FileOC : ObservingConditions, INotifyPropertyChanged {
        public new void SetupDialog() { throw new System.NotImplementedException(); }
        public new string Action(string ActionName, string ActionParameters) => throw new System.NotImplementedException();
        public new void CommandBlind(string Command, bool Raw = false) { throw new System.NotImplementedException(); }
        public new bool CommandBool(string Command, bool Raw = false) => throw new System.NotImplementedException();
        public new string CommandString(string Command, bool Raw = false) => throw new System.NotImplementedException();
        public new ArrayList SupportedActions { get; set; } = new ArrayList();

        public new string Description { get; }
        public new string DriverInfo => $"File reader Observing conditions driver v{DriverVersion}";

        public new string DriverVersion { get; } = "0.1";
        public new short  InterfaceVersion { get; } = 3;
        public new string SensorDescription(string PropertyName) => throw new System.NotImplementedException();


        private readonly Timer timer;
        private bool _connected;
        public new bool Connected {
            get => _connected;
            set {
                if (_connected.Equals(value)) return;
                _connected = value;
            }
        }
        
        
        protected DateTime lastUpdateTime;
        public new double TimeSinceLastUpdate(string PropertyName) => (DateTime.Now - lastUpdateTime).TotalSeconds;

        protected abstract void parse(string file);

        
        public new async void Refresh() {
            if (!Connected) return;

            lastUpdateTime = File.GetLastWriteTime(Description);
            try {
                using (var f = File.OpenText(Description))
                    parse(await f.ReadToEndAsync());
            } catch {}
        }


        public new void Dispose() {
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            timer.Dispose();
        }
        ~FileOC() => Dispose();


        public new string Name { get; } = "FileOC";

        
        private double _averagePeriod;
        public new double AveragePeriod {
            get => _averagePeriod;
            set {
                if (value.Equals(_averagePeriod)) return;
                _averagePeriod = value;

                timer.Change(TimeSpan.Zero, TimeSpan.FromHours(_averagePeriod));
                
                OnPropertyChanged();
            }
        }
        
        protected double? _cloudCover;
        public new double CloudCover {
            get => _cloudCover ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_cloudCover)) return;
                _cloudCover = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _dewPoint;
        public new double DewPoint {
            get => _dewPoint ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_dewPoint)) return;
                _dewPoint = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _humidity;
        public new double Humidity {
            get => _humidity ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_humidity)) return;
                _humidity = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _pressure;
        public new double Pressure {
            get => _pressure ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_pressure)) return;
                _pressure = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _rainRate;
        public new double RainRate {
            get => _rainRate ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_rainRate)) return;
                _rainRate = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _skyBrightness;
        public new double SkyBrightness {
            get => _skyBrightness ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_skyBrightness)) return;
                _skyBrightness = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _skyQuality;
        public new double SkyQuality {
            get => _skyQuality ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_skyQuality)) return;
                _skyQuality = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _starFWHM;
        public new double StarFWHM {
            get => _starFWHM ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_starFWHM)) return;
                _starFWHM = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _skyTemperature;
        public new double SkyTemperature {
            get => _skyTemperature ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_skyTemperature)) return;
                _skyTemperature = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _temperature;
        public new double Temperature {
            get => _temperature ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_temperature)) return;
                _temperature = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _windDirection;
        public new double WindDirection {
            get => _windDirection ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_windDirection)) return;
                _windDirection = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _windGust;
        public new double WindGust {
            get => _windGust ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_windGust)) return;
                _windGust = value;
                OnPropertyChanged();
            }
        }
        
        protected double? _windSpeed;
        public new double WindSpeed {
            get => _windSpeed ?? throw new PropertyNotImplementedException();
            set {
                if (value.Equals(_windSpeed)) return;
                _windSpeed = value;
                OnPropertyChanged();
            }
        }


        public FileOC(string fname, TimeSpan freq) : base(null) {
            Description = fname;
            timer = new Timer(o => Refresh(), null, TimeSpan.Zero, freq);
        }

        
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BoltwoodFileOC : FileOC {
        public BoltwoodFileOC(string fname, TimeSpan freq) : base(fname, freq) { }

        private static double temp(string name, GroupCollection g) {
            var val = double.Parse(g[name].Value);
            return g["Tunit"].Value == "C" ? val : val * 9/5 + 32;
        }
        private static double speed(string name, GroupCollection g) {
            var val = double.Parse(g[name].Value);
            return g["Vunit"].Value == "m"   ? val
                   : g["Vunit"].Value == "M" ? (1760 * 36 * 0.0254 / 3600) * val
                                               : (1000.0 / 3600.0) * val;
        }

        private static readonly Regex re = new Regex(@"(?<Date>\d\d\d\d-\d\d-\d\d)\s+(?<Time>\d\d:\d\d:\d\d.\d\d)\s+(?<Tunit>[CF])\s+(?<Vunit>[KMm])\s+(?<SkyTemperature>[-.\d]*)\s+(?<Temperature>[-.\d]*)\s+(?<SensorTemperature>[-.\d]*)\s+(?<Wind>[-.\d]*)\s+(?<Humidity>[-.\d]*)\s+(?<DewPoint>[-.\d]*)\s+(?<Heater>[-.\d]*)\s+(?<RainFlag>[012]*)\s+(?<WetFlag>[012]*)\s+(?<Since>[-.\d]*)\s+(?<Now>[-.\d]*)\s+(?<CloudCond>[0123])\s+(?<WindCond>[0123])\s+(?<RainCond>[0123])\s+(?<DayCond>[0123])\s+(?<Roof>[01])\s+(?<Alert>[01])",
                                            RegexOptions.Compiled | RegexOptions.Singleline);
        protected override void parse(string file) {
            if (re.Match(file) is Match m) {
                lastUpdateTime =
                    DateTime.ParseExact($"{(m.Groups["Date"])} {(m.Groups["Time"])}",
                                        "yyyy-MM-dd HH':'mm':'ss.ff", CultureInfo.InvariantCulture)
                    - TimeSpan.FromSeconds(int.Parse(m.Groups["Since"].Value));
                SkyTemperature = temp("SkyTemperature", m.Groups);
                Temperature    = temp("Temperature",    m.Groups);
                WindSpeed      = speed("Wind", m.Groups);
                Humidity       = double.Parse(m.Groups["Humidity"].Value);
                DewPoint       = temp("DewPoint", m.Groups);
                SkyBrightness  = m.Groups["DayCond"].Value == "1"   ? 0
                                 : m.Groups["DayCond"].Value == "2" ? 50
                                 : m.Groups["DayCond"].Value == "3" ? 100 : double.NaN;
                RainRate       = (m.Groups["RainFlag"].Value == "0"
                                  && (m.Groups["RainCond"].Value == "0" || m.Groups["RainCond"].Value == "1"))
                                     ? 0 : 10;
                SkyBrightness  = m.Groups["DayCond"].Value == "1"   ? 100
                                 : m.Groups["DayCond"].Value == "2" ? 1000
                                 : m.Groups["DayCond"].Value == "3" ? 10000 : double.NaN;
            }
        }
    }
}
