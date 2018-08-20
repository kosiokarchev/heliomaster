using System.Collections;
using ASCOM.DeviceInterface;
using ASCOM.DriverAccess;

namespace heliomaster {
    public class FileObservingConditions : IObservingConditions {
        public void SetupDialog() {
            throw new System.NotImplementedException();
        }

        public string Action(string ActionName, string ActionParameters) {
            throw new System.NotImplementedException();
        }

        public void CommandBlind(string Command, bool Raw = false) {
            throw new System.NotImplementedException();
        }

        public bool CommandBool(string Command, bool Raw = false) {
            throw new System.NotImplementedException();
        }

        public string CommandString(string Command, bool Raw = false) {
            throw new System.NotImplementedException();
        }

        public void Dispose() {
            throw new System.NotImplementedException();
        }

        public double TimeSinceLastUpdate(string PropertyName) {
            throw new System.NotImplementedException();
        }

        public string SensorDescription(string PropertyName) {
            throw new System.NotImplementedException();
        }

        public void Refresh() {
            throw new System.NotImplementedException();
        }

        private bool _connected;
        public bool Connected {
            get => _connected;
            set {
                _connected = value;

            }
        }

        public string Description { get; }
        public string DriverInfo { get; }
        public string DriverVersion { get; }
        public short InterfaceVersion { get; }
        public string Name { get; }
        public ArrayList SupportedActions { get; }

        private double _averagePeriod;
        public double AveragePeriod { get; set; } = 0;
        public double CloudCover { get; }
        public double DewPoint { get; }
        public double Humidity { get; }
        public double Pressure { get; }
        public double RainRate { get; }
        public double SkyBrightness { get; }
        public double SkyQuality { get; }
        public double StarFWHM { get; }
        public double SkyTemperature { get; }
        public double Temperature { get; }
        public double WindDirection { get; }
        public double WindGust { get; }
        public double WindSpeed { get; }
    }
}
