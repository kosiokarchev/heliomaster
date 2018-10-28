using System;
using System.Threading.Tasks;
using heliomaster.Properties;

namespace heliomaster {
    public partial class Observatory {
        /// <summary>
        /// A class (structure) that holds and computes the parameters needed for <see cref="Observatory.Startup"/>.
        /// </summary>
        public class StartupArguments {
            /// <summary>
            /// Whether to require the object to be located in order to consider the startup successful.
            /// </summary>
            public bool RequireInView;

            /// <summary>
            /// Whether to automatically start the timelapses.
            /// </summary>
            public bool Autostart;

            /// <summary>
            /// Approximate time to schedule the shutdown (but see <see cref="CamShutdownTime"/> and
            /// <see cref="ShutdownTime"/>). A value of <c>null</c> indicates that a shutdown should not be scheduled.
            /// </summary>
            public DateTime? CloseAt;

            /// <summary>
            /// The interval between the scheduled <see cref="Timelapse.End"/> time of the timelapses and
            /// <see cref="CloseAt"/> (see <see cref="CamShutdownTime"/>.
            /// </summary>
            public TimeSpan? CamMargin;

            /// <summary>
            /// The interval between the actual scheduled shutdown time and <see cref="CloseAt"/>. See <see cref="ShutdownTime"/>.
            /// </summary>
            public TimeSpan? CloseMargin;

            /// <summary>
            /// return a time that is <paramref name="margin"/> before <see cref="CloseAt"/>
            /// </summary>
            /// <returns>
            /// <list type="bullet">
            ///     <item>If <see cref="CloseAt"/> is <c>null</c>, returns <c>null</c>.</item>
            ///     <item>Otherwise, if <paramref name="margin"/> is <c>null</c>, returns <see cref="CloseAt"/>.</item>
            ///     <item>If both <see cref="CloseAt"/> and <paramref name="margin"/> are set, returns
            ///         <c>CloseAt-margin</c>.</item>
            /// </list>
            /// </returns>
            private DateTime? Marginalize(TimeSpan? margin) =>
                CloseAt == null  ? (DateTime?) null
                : margin == null ? (DateTime) CloseAt
                                   : (DateTime) CloseAt - (TimeSpan) margin;

            /// <summary>
            /// The actual time that the <see cref="Timelapse.End"/> of the timelapses is set to. 
            /// </summary>
            /// <value>
            /// <list type="bullet">
            ///     <item>If <see cref="CloseAt"/> is <c>null</c>, this is also <c>null</c>, which indicates that the
            ///         timelapses should be left as-is.</item>
            ///     <item>Otherwise, if <see cref="CamMargin"/> is <c>null</c>, this is the same time as
            ///         <see cref="CloseAt"/>.</item>
            ///     <item>If both <see cref="CloseAt"/> and <see cref="CamMargin"/> are set, this is
            ///         <c>CloseAt-CamMargin</c>.</item>
            /// </list>
            /// </value>
            public DateTime? CamShutdownTime => Marginalize(CamMargin);

            /// <summary>
            /// The actual time that the shutdown is scheduled for. 
            /// </summary>
            /// <value>
            /// <list type="bullet">
            ///     <item>If <see cref="CloseAt"/> is <c>null</c>, this is also <c>null</c>, which indicates that a
            ///         shutdown should not be scheduled.</item>
            ///     <item>Otherwise, if <see cref="CamMargin"/> is <c>null</c>, this is the same time as
            ///         <see cref="CloseAt"/>.</item>
            ///     <item>If both <see cref="CloseAt"/> and <see cref="CloseMargin"/> are set, this is
            ///         <c>CloseAt-CloseMargin</c>.</item>
            /// </list>
            /// </value>
            public DateTime? ShutdownTime => Marginalize(CloseMargin);
        }

        /// <summary>
        /// Raised when the start of observatory automation is requested.
        /// </summary>
        /// <remarks>This events is handled by <see cref="StartingHandle"/> which in turn calls <see cref="Startup"/>.</remarks>
        public event Action<StartupArguments> Starting;
        public void                           StartingRaise(StartupArguments args = null) => Starting?.Invoke(args ?? new StartupArguments());
        /// <summary>
        /// Handle the <see cref="Starting"/> event. Call <see cref="Startup"/> with the <see cref="StartupArguments"/>
        /// argument of the event.
        /// </summary>
        /// <param name="args">The arguments to pass to <see cref="Startup"/>.</param>
        private void StartingHandle(StartupArguments args) => Startup(args);
        
        /// <summary>
        /// Raised when the observatory automation has been successfully initiated (by <see cref="Startup"/>).
        /// </summary>
        public event Action StartupSuccess;
        public void         StartupSuccessRaise() => StartupSuccess?.Invoke();
        
        /// <summary>
        /// Raised when the observatory automation has failed to initialise (by <see cref="Startup"/>).
        /// </summary>
        public event Action<ObservatoryException> StartupFailure;
        public void                               StartupFailureRaise(ObservatoryException e) => StartupFailure?.Invoke(e);
        
        /// <summary>
        /// Raised when a shutdown of observatory operations is requested. 
        /// </summary>
        /// <remarks>This event is handled by <see cref="ShuttingHandle"/> which in turn calls <see cref="Shutdown"/>.</remarks>
        public event Action Shutting;
        public  void        ShuttingRaise()  => Shutting?.Invoke();
        /// <summary>
        /// Handle the <see cref="Shutting"/> event. Call <see cref="Shutdown"/>.
        /// </summary>
        private void ShuttingHandle() => Shutdown();
        
        /// <summary>
        /// Raised when the observatory has been shut down successfully (by <see cref="Shutdown"/>).
        /// </summary>
        public event Action ShutdownSuccess;
        public void         ShutdownSuccessRaise() => ShutdownSuccess?.Invoke();
        
        /// <summary>
        /// Raised when the observatory has failed to shut down successfully (by <see cref="Shutdown"/>).
        /// </summary>
        public event Action<Exception> ShutdownFailure;
        public void                    ShutdownFailureRaise(Exception e) => ShutdownFailure?.Invoke(e);
        
        /// <summary>
        /// Raised when a fix of the observatory state has been requested.
        /// </summary>
        /// <remarks>This event is handled by <see cref="FixingHandle"/> which in turn calls <see cref="Fix"/>.</remarks>
        public event Action Fixing;
        public  void        FixingRaise()  => Fixing?.Invoke();
        /// <summary>
        /// Handle the <see cref="Fix"/> event. Call <see cref="Fix"/>.
        /// </summary>
        private void FixingHandle() => Fix();
        
        /// <summary>
        /// Raised when fixing of the observatory state has been successful (by <see cref="Fix"/>).
        /// </summary>
        public event Action FixingSuccess;
        public void         FixingSuccessRaise() => FixingSuccess?.Invoke();
        
        /// <summary>
        /// Raised when fixing of the observatory state has failed (by <see cref="Fix"/>).
        /// </summary>
        public event Action FixingFailure;
        public void         FixingFailureRaise() => FixingFailure?.Invoke();

        /// <summary>
        /// Try to ensure the hardware controller <paramref name="h"/> is connected to hardware with <paramref name="id"/>, or throw an error.
        /// </summary>
        /// <param name="h">The hardware controller to connect.</param>
        /// <param name="id">The id of the hardware to connect to.</param>
        /// <returns>Whether the connection was successful. In fact the only possible return value is <c>true</c>
        /// since in case the connection is unsuccessful, the method throws <see cref="ConnectionError"/>.</returns>
        /// <exception cref="ConnectionError">If the connection is impossible.</exception>
        private static async Task<bool> connect(BaseHardwareControl h, string id) {
            if (h.Valid || await h.Connect(id))
                return true;
            else
                throw new ConnectionError($"Could not connect hardware type {h.Type} with id \"{id}\"");
        }

        /// <summary>
        /// Connect the hardware controller <paramref name="h"/> to the default hardware of the respective type.
        /// </summary>
        /// <param name="h">The hardware controller to connect.</param>
        /// <returns>Whether the connection was successful (but see <see cref="connect(heliomaster.BaseHardwareControl,string)"/>).</returns>
        /// <exception cref="ArgumentException">If <paramref name="h"/> is not one of <see cref="Dome"/>,
        /// <see cref="Mount"/>, or <see cref="Weather"/>.</exception>
        /// <exception cref="ConnectionError">If the connection is impossible.</exception>
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

        /// <summary>
        /// An enumeration of the possible states of the observatory automation.
        /// </summary>
        public enum AutomationStates {
            /// <summary>
            /// No automation is taking place and the observatory is assumed to be in the fixed state.
            /// </summary>
            Idle,
            
            /// <summary>
            /// <see cref="Observatory.Starting"/> has been raised, and <see cref="Observatory.Startup"/> is
            /// running but has not terminated yet.
            /// </summary>
            Starting,
            
            /// <summary>
            /// <see cref="Observatory.StartupSuccess"/> has been raised and the observation is in progress.
            /// </summary>
            InOperation,
            
            /// <summary>
            /// <see cref="Observatory.Shutdown"/> has been raised, and <see cref="Observatory.Shutdown"/> is
            /// running but has not terminated yet.
            /// </summary>
            Closing,
            
            /// <summary>
            /// A startup attempt has been made but failed due to bad weather, and a startup has been scheduled for when
            /// safe weather is confirmed.
            /// </summary>
            WaitingForWeather,
            
            /// <summary>
            /// An exception has been encountered during <see cref="Observatory.Startup"/>,
            /// <see cref="Observatory.Shutdown"/>, or <see cref="Observatory.Fix"/>, and <see cref="Fix"/> has not
            /// begun yet in the former two cases.
            /// </summary>
            Faulted,
            
            /// <summary>
            /// <see cref="Observatory.Fixing"/> has been raised, and <see cref="Observatory.Fix"/> is running but has
            /// not terminated yet.
            /// </summary>
            Fixing
        }

        private AutomationStates _automationState;
        /// <summary>
        /// The current state of observatory automation. Changing this value results in a log message (info level). 
        /// </summary>
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
