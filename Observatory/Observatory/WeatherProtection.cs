using System;
using System.ComponentModel;

namespace heliomaster {
    public partial class Observatory {
        private bool _weatherProtectionOn;
        /// <summary>
        /// Whether the weather protection is currently enabled.
        /// </summary>
        public bool WeatherProtectionOn {
            get => _weatherProtectionOn;
            set {
                if (_weatherProtectionOn.Equals(value)) return;
                _weatherProtectionOn = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The currently known <see cref="heliomaster.Weather.Safe"/> value of <see cref="Weather"/>.
        /// </summary>
        private bool? currSafeState;
        
        /// <summary>
        /// Raised when the weather <see cref="heliomaster.Weather.Safe"/> state changes with the new known state.
        /// </summary>
        public event Action<bool?> WeatherSafeChanged;
        private void WeatherSafeChangedRaise() => WeatherSafeChanged?.Invoke(currSafeState);
        /// <summary>
        /// Handle the <see cref="WeatherSafeChanged"/> event. Stop automation if it is running, and the new state is
        /// not safe.
        /// </summary>
        /// <param name="safe">The new safe state, as provided by the <see cref="WeatherSafeChanged"/> event.</param>
        private void WeatherSafeChangedHandle(bool? safe) {
            if (AutomationState == AutomationStates.InOperation && safe != true) Shutdown();
        }

        /// <summary>
        /// Raised whenever the weather has been found to be not safe (possibly multiple times in a row).
        /// </summary>
        public event Action WeatherIsUnsafe;
        private void WeatherIsUnsafeRaise() => WeatherIsUnsafe?.Invoke();

        /// <summary>
        /// A method that realises the weather safe checks. Repeatedly called whenever a property on
        /// <see cref="Weather"/> changes (see <see cref="StartWeatherProtection"/>).
        /// </summary>
        /// <param name="o">The object (<see cref="Weather"/>) that raised the event. Not used.</param>
        /// <param name="args">The <see cref="PropertyChangedEventArgs"/> containing the
        /// <see cref="PropertyChangedEventArgs.PropertyName"/> of the property that has changed. The event is handled
        /// if this is the name of <see cref="heliomaster.Weather.Safe"/>.</param>
        /// <remarks>Checks whether the newly reported <see cref="heliomaster.Weather.Safe"/> state is different from
        /// the <see cref="currSafeState"/>, updates it and raises <see cref="WeatherSafeChanged"/> if so. If the new
        /// <see cref="currSafeState"/> is not safe (<c>!= true</c>), raises <see cref="WeatherIsUnsafe"/>.</remarks>
        private void weatherProtection(object o, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(Weather.Safe)) {
                var safe = Weather.Safe;
                if (safe != currSafeState) {
                    currSafeState = safe;
                    WeatherSafeChangedRaise();
                }

                if (currSafeState != true) WeatherIsUnsafeRaise();
            }
        }

        /// <summary>
        /// Start the weather protection service by registering <see cref="weatherProtection"/> to listen for the
        /// <see cref="INotifyPropertyChanged.PropertyChanged"/> event of <see cref="Weather"/>.
        /// </summary>
        /// <returns>Whether the weather state is currently safe.</returns>
        public bool StartWeatherProtection() {
            Weather.PropertyChanged += weatherProtection;
            WeatherProtectionOn     =  true;
            return Weather.Safe == true;
        }

        /// <summary>
        /// Stops the weather protection service.
        /// </summary>
        public void StopWeatherProtection() {
            Weather.PropertyChanged -= weatherProtection;
            WeatherProtectionOn     =  false;
        }
    }
}
