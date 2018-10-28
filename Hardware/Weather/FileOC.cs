using System;
using System.Collections;
using System.ComponentModel;
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
    public class FileParsingException : Exception {}

    public abstract class FileOC : AscomDriver, IObservingConditions, INotifyPropertyChanged {
        public void SetupDialog() { throw new System.NotImplementedException(); }
        public string Action(string ActionName, string ActionParameters) => throw new System.NotImplementedException();
        public void CommandBlind(string Command, bool Raw = false) { throw new System.NotImplementedException(); }
        public bool CommandBool(string Command, bool Raw = false) => throw new System.NotImplementedException();
        public string CommandString(string Command, bool Raw = false) => throw new System.NotImplementedException();
        public ArrayList SupportedActions { get; set; } = new ArrayList();

        /// <summary> The path to the file used to extract the weather data. </summary>
        public string Description { get; }
        public string DriverInfo => $"File reader Observing conditions driver v{DriverVersion}";

        public string DriverVersion { get; } = "1.0";
        public short  InterfaceVersion { get; } = 3;
        public string SensorDescription(string PropertyName) => throw new System.NotImplementedException();


        public new string Name { get; } = "FileOC";


        private double _averagePeriod = 10.0 / 3600.0; // TODO: Unhardcode
        /// <summary> Gets and sets the period between automatic refreshes. </summary>
        /// <value> The period (in hours) between automatic refreshes. The default is 10 seconds. </value>
        /// <remarks> This property has been re-purposed but the unit has been left unaltered. </remarks>
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
            protected set {
                if (value.Equals(_cloudCover)) return;
                _cloudCover = value;
                OnPropertyChanged();
            }
        }

        protected double? _dewPoint;
        public double DewPoint {
            get => _dewPoint ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_dewPoint)) return;
                _dewPoint = value;
                OnPropertyChanged();
            }
        }

        protected double? _humidity;
        public double Humidity {
            get => _humidity ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_humidity)) return;
                _humidity = value;
                OnPropertyChanged();
            }
        }

        protected double? _pressure;
        public double Pressure {
            get => _pressure ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_pressure)) return;
                _pressure = value;
                OnPropertyChanged();
            }
        }

        protected double? _rainRate;
        public double RainRate {
            get => _rainRate ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_rainRate)) return;
                _rainRate = value;
                OnPropertyChanged();
            }
        }

        protected double? _skyBrightness;
        public double SkyBrightness {
            get => _skyBrightness ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_skyBrightness)) return;
                _skyBrightness = value;
                OnPropertyChanged();
            }
        }

        protected double? _skyQuality;
        public double SkyQuality {
            get => _skyQuality ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_skyQuality)) return;
                _skyQuality = value;
                OnPropertyChanged();
            }
        }

        protected double? _starFWHM;
        public double StarFWHM {
            get => _starFWHM ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_starFWHM)) return;
                _starFWHM = value;
                OnPropertyChanged();
            }
        }

        protected double? _skyTemperature;
        public double SkyTemperature {
            get => _skyTemperature ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_skyTemperature)) return;
                _skyTemperature = value;
                OnPropertyChanged();
            }
        }

        protected double? _temperature;
        public double Temperature {
            get => _temperature ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_temperature)) return;
                _temperature = value;
                OnPropertyChanged();
            }
        }

        protected double? _windDirection;
        public double WindDirection {
            get => _windDirection ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_windDirection)) return;
                _windDirection = value;
                OnPropertyChanged();
            }
        }

        protected double? _windGust;
        public double WindGust {
            get => _windGust ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_windGust)) return;
                _windGust = value;
                OnPropertyChanged();
            }
        }

        protected double? _windSpeed;
        public double WindSpeed {
            get => _windSpeed ?? throw new PropertyNotImplementedException();
            protected set {
                if (value.Equals(_windSpeed)) return;
                _windSpeed = value;
                OnPropertyChanged();
            }
        }


        /// <summary> Create a new ObservingConditions driver, reading data from a file. </summary>
        /// <param name="fname">The path to the file to use as a data source. It is saved in <see cref="Description"/>.</param>
        protected FileOC(string fname) {
            Description = fname;
        }
        
        
        private Timer timer;
        private bool  _connected;
        public new bool Connected {
            get => _connected;
            set {
                var val = false;
                if (value)
                    try {
                        parse(File.ReadAllText(Description)); // Check if file is valid
                        val = true;
                        if (timer != null) timer.Change(TimeSpan.Zero, TimeSpan.FromHours(AveragePeriod));
                        else timer = new Timer(o => Refresh(), null, TimeSpan.Zero, TimeSpan.FromHours(AveragePeriod));
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
        public    double   TimeSinceLastUpdate(string PropertyName) => (DateTime.Now - lastUpdateTime).TotalSeconds;

        /// <summary> Extract the values for weather properties from the contents of a file. </summary>
        /// <remarks> The extracted values are saved in the corresponding properties. </remarks>
        /// <param name="file">The contents of the file as text.</param>
        /// <returns>Whether the parsing was successful.</returns>
        /// <exception cref="FileParsingException">If the file could not be parsed.</exception>
        protected abstract bool parse(string file);
        
        /// <inheritdoc />
        /// <summary> Refresh the properties if the datafile has been updated after the last refresh. </summary>
        /// <exception cref="FileParsingException">If the file could not be parsed.</exception>
        public async void Refresh() {
            if (!Connected) throw new NotConnectedException();

            var lastUpdateTimeBackup = lastUpdateTime;
            lastUpdateTime = File.GetLastWriteTime(Description);
            if (lastUpdateTime > lastUpdateTimeBackup) {
                try {
                    using (var f = File.OpenText(Description))
                        if (!parse(await f.ReadToEndAsync()))
                            throw new FileParsingException();
                } catch {
                    lastUpdateTime = lastUpdateTimeBackup;
                }
            }
        }


        /// <summary> Dispose of the FileOC. </summary>
        /// <remarks> This method sets all properties to <c>null</c> and stops the auto-update timer. However, even
        /// after that, refreshing is still available via <see cref="Refresh"/>. </remarks>
        public new void Dispose() {
            _cloudCover = _dewPoint = _humidity = _pressure = _rainRate = _skyBrightness = _skyQuality
                = _skyTemperature = _starFWHM = _temperature = _windDirection = _windGust = _windSpeed
                = null;

            timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            timer?.Dispose();
            timer = null;
        }
        ~FileOC() => Dispose();


        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary> A class to parse weather data saved in files using the Boltwood standard. </summary>
    public class BoltwoodFileOC : FileOC {
        public BoltwoodFileOC(string fname) : base(fname) { }

        /// <summary> Parse a double from a string using a dot as a decimal separator. </summary>
        /// <param name="s">The string to convert ot double. Must match "[-.\d]*".</param>
        /// <returns>The number as a double.</returns>
        private static double ToDouble(string s) {
            return double.Parse(s, CultureInfo.InvariantCulture);
        }
        /// <summary> Extract a temperature in degrees Celsius from a Regex match. </summary>
        /// <param name="name">The name of the match group containing the numerical value.</param>
        /// <param name="g">The Regex match groups.</param>
        /// <returns>The temperature in degrees Celsius.</returns>
        private static double temp(string name, GroupCollection g) {
            var val = ToDouble(g[name].Value);
            return g["Tunit"].Value == "C" ? val : val * 9.0/5.0 + 32;
        }
        /// <summary> Extract a speed in m/s from a Regex match. </summary>
        /// <param name="name">The name of the match group containing the numerical value.</param>
        /// <param name="g">The Regex match groups.</param>
        /// <returns>The speed in m/s.</returns>
        private static double speed(string name, GroupCollection g) {
            var val = ToDouble(g[name].Value);
            return g["Vunit"].Value == "m"   ? val
                   : g["Vunit"].Value == "M" ? (1760 * 36 * 0.0254 / 3600) * val
                                               : (1000.0 / 3600.0) * val;
        }

        private static readonly Regex re = new Regex(
            @"(?<Date>\d\d\d\d-\d\d-\d\d)\s+(?<Time>\d\d:\d\d:\d\d.\d\d)\s+(?<Tunit>[CF])\s+(?<Vunit>[KMm])\s+(?<SkyTemperature>[-.\d]*)\s+(?<Temperature>[-.\d]*)\s+(?<SensorTemperature>[-.\d]*)\s+(?<Wind>[-.\d]*)\s+(?<Humidity>[-.\d]*)\s+(?<DewPoint>[-.\d]*)\s+(?<Heater>[-.\d]*)\s+(?<RainFlag>[012]*)\s+(?<WetFlag>[012]*)\s+(?<Since>[-.\d]*)\s+(?<Now>[-.\d]*)\s+(?<CloudCond>[0123])\s+(?<WindCond>[0123])\s+(?<RainCond>[0123])\s+(?<DayCond>[0123])\s*(?<Roof>[01]*)\s*(?<Alert>[01]*)",
            RegexOptions.Compiled | RegexOptions.Singleline);
        
        /// <inheritdoc />
        /// <summary>
        /// Matches the given file contents to <see cref="F:heliomaster.BoltwoodFileOC.re" /> and extracts the
        /// parameters found.
        /// </summary>
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
