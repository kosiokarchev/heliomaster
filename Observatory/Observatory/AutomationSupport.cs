using System;
using System.Threading.Tasks;
using heliomaster.Properties;

namespace heliomaster {
    public partial class Observatory {
        public class StartupArguments {
            public bool      RequireInView;
            public bool      Autostart;
            public DateTime? CloseAt;
            public TimeSpan? CamMargin;
            public TimeSpan? CloseMargin;

            private DateTime? Marginalize(TimeSpan? margin) {
                return CloseAt == null
                           ? (DateTime?) null
                           : (margin == null
                                  ? (DateTime) CloseAt
                                  : (DateTime) CloseAt - (TimeSpan) margin);
            }

            public DateTime? CamShutdownTime => Marginalize(CloseMargin);
            public DateTime? ShutdownTime    => Marginalize(CloseMargin);
        }

        public event Action<StartupArguments> Starting;

        public void StartingRaise(StartupArguments args = null) => Starting?.Invoke(args ?? new StartupArguments());

        private void                              StartingHandle(StartupArguments args) => Startup(args);
        public event Action                       StartupSuccess;
        public void                               StartupSuccessRaise() => StartupSuccess?.Invoke();
        public event Action<ObservatoryException> StartupFailure;

        public void StartupFailureRaise(ObservatoryException e) => StartupFailure?.Invoke(e);

        public event Action            Shutting;
        public  void                   ShuttingRaise()  => Shutting?.Invoke();
        private void                   ShuttingHandle() => Shutdown();
        public event Action            ShutdownSuccess;
        public void                    ShutdownSuccessRaise() => ShutdownSuccess?.Invoke();
        public event Action<Exception> ShutdownFailure;
        public void                    ShutdownFailureRaise(Exception e) => ShutdownFailure?.Invoke(e);
        public event Action            Fixing;
        public  void                   FixingRaise()  => Fixing?.Invoke();
        private void                   FixingHandle() => Fix();
        public event Action            FixingSuccess;
        public void                    FixingSuccessRaise() => FixingSuccess?.Invoke();
        public event Action            FixingFailure;
        public void                    FixingFailureRaise() => FixingFailure?.Invoke();

        private bool perform(Task<bool> t, string msg) {
            t.Wait();
            if (t.Exception != null || t.Result) {
                Emit(new AutoOperationsException(
                         $"{msg}: {(t.Exception == null ? "no details" : t.Exception.Message)}"));
                return false;
            } else
                return true;
        }

        private async Task<bool> connect(BaseHardwareControl h, string id) {
            if (h.Valid || await h.Connect(id))
                return true;
            else
                throw new ConnectionError($"Could not connect hardware type {h.GetType().Name} with id \"{id}\"");
        }

        private Task<bool> connect(BaseHardwareControl h) {
            string id;
            if (h.Equals(Dome))
                id = S.Dome.DomeID;
            else if (h.Equals(Mount))
                id = S.Mount.MountID;
            else if (h.Equals(Weather))
                id = O.WeatherID;
            else
                throw new ArgumentException();
            return connect(h, id);
        }

        public enum AutomationStates {
            Idle,
            Starting,
            InOperation,
            Closing,
            WaitingForWeather,
            Faulted,
            Fixing
        }

        private AutomationStates _automationState;

        public AutomationStates AutomationState {
            get => _automationState;
            set {
                if (_automationState.Equals(value)) return;
                _automationState = value;

                Logger.info($"Transitioning to {_automationState}");

                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanShutdown));
            }
        }
    }
}
