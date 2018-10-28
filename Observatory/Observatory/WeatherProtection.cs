using System;
using System.ComponentModel;

namespace heliomaster {
    public partial class Observatory {
        private bool _weatherProtectionOn;

        public bool WeatherProtectionOn {
            get => _weatherProtectionOn;
            set {
                if (_weatherProtectionOn.Equals(value)) return;
                _weatherProtectionOn = value;
                OnPropertyChanged();
            }
        }

        private bool?              currSafeState;
        public event Action<bool?> WeatherSafeChanged;

        private void WeatherSafeChangedHandle(bool? safe) {
            if (AutomationState == AutomationStates.InOperation && safe != true) Shutdown();
        }

        public event Action WeatherIsUnsafe;

        private void weatherProtection(object o, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(Weather.Safe)) {
                var safe = Weather.Safe;
                if (safe != currSafeState) {
                    currSafeState = safe;
                    WeatherSafeChanged?.Invoke(currSafeState);
                }

                if (currSafeState != true) WeatherIsUnsafe?.Invoke();
            }
        }

        public bool StartWeatherProtection() {
            Weather.PropertyChanged += weatherProtection;
            WeatherProtectionOn     =  true;
            return Weather.Safe == true;
        }

        public void StopWeatherProtection() {
            Weather.PropertyChanged -= weatherProtection;
            WeatherProtectionOn     =  false;
        }
    }
}