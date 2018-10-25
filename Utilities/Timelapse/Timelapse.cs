using System;
using heliomaster.Properties;

namespace heliomaster {
    /// <summary>
    /// A class handling a timelapse, i.e. executing a certain action periodically until a certain condition.
    /// </summary>
    /// <remarks>
    /// <para>A timelapse has four parameters: an <see cref="Interval"/>, a total number of "frames" to execute
    /// (<see cref="Nshots"/>), a total <see cref="Duration"/>, and an <see cref="End"/> time. These are periodically
    /// coerced by the <see cref="Fix"/> function. The exact procedure depends on which of the three latter parameters
    /// is chosen as "guiding", i.e. used in the stopping condition:
    /// <list type="bullet">
    ///     <item>
    ///         <term><c>StopMethod == 0</c> (<see cref="Nshots"/>)</term>
    ///         <description>
    ///             The <see cref="Duration"/> is calculated as <c>(Nshots-1)*Interval</c> since the first "frame"
    ///             is taken at the start of the timelapse, and the <see cref="End"/> time is calculated as
    ///             <see cref="Duration"/> after <see cref="DateTime.Now"/>;
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><c>StopMethod == 1</c> (<see cref="Duration"/>)</term>
    ///         <description>
    ///             <see cref="Nshots"/> is <c>Duration // Interval + 1</c>, i.e. 1 more than the number of
    ///             <see cref="Interval"/>s that fit in <see cref="Duration"/>, and the <see cref="End"/> time is
    ///             calculated as <see cref="Duration"/> after <see cref="DateTime.Now"/>;
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><c>StopMethod == 2</c> (<see cref="End"/>)</term>
    ///         <description>
    ///             <see cref="Duration"/> is <c>End - DateTime.Now</c>, and <see cref="Nshots"/> is <c>Duration //
    ///             Interval + 1</c>.
    ///         </description>
    ///     </item>
    /// </list>
    /// The <see cref="Interval"/> can only be adjusted by the user.
    /// </para>
    /// <para> This means that while not running, if the chosen method is <see cref="Nshots"/> or <see cref="Duration"/>
    /// they will remain constant while <see cref="End"/> will update to reflect the proper expected end time, and
    /// conversely if <see cref="End"/> is guiding. However, <see cref="End"/> will always be non-past, i.e. if it is
    /// guiding, and the time it indicates has passed, it will be set to the current time. This way always a started
    /// timelapse will execute at least one "frame". </para>
    /// <para> While the timelapse is running, its parameters are adjusted so that if stopped and resumed, it will have
    /// (almost) the same effect as if it were not stopped in the first place. This means that it will execute the
    /// same number of frames if <see cref="Nshots"/> is guiding, will run for the same total <see cref="Duration"/> if
    /// it is guiding, or will end and the same <see cref="End"/> time otherwise.
    /// </para>
    /// </remarks>
    public class Timelapse : BaseNotify {
        private bool _free = true;
        /// <summary>
        /// Whether the timelapse can be edited or is controlled by a <see cref="CommonTimelapse"/>
        /// </summary>
        public bool Free {
            get => _free;
            set { _free = value; OnPropertyChanged(); OnPropertyChanged(nameof(Editable)); }
        }

        private bool _running;
        /// <summary>
        /// Whether the timelapse is currently running.
        /// </summary>
        public bool Running {
            get => _running;
            set { _running = value; OnPropertyChanged(); OnPropertyChanged(nameof(Editable)); }
        }

        /// <summary>
        /// Whether the timelapse is currently editable, i.e. it is <see cref="Free"/> and not <see cref="Running"/>.
        /// </summary>
        public bool Editable => !Running && Free;


        #region PROGRESS

        private double _progressNext;
        /// <summary>
        /// Indicates the fraction of the timelapse period that has elapsed since the last "frame".
        /// </summary>
        public double ProgressNext {
            get => _progressNext;
            private set {
                _progressNext = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressNextLabel));
            }
        }
        /// <summary>
        /// Representing the time remaining until the next "frame".
        /// </summary>
        public string ProgressNextLabel => Running ? $"frame in {(1-ProgressNext) * Interval.TotalSeconds:F0}s" : null;

        /// <summary>
        /// Indicates the fraction of the whole duration of the timelapse that has elapsed.
        /// </summary>
        public double ProgressTotal => Running ? (double) ntaken / ntaking : 0;
        /// <summary>
        /// Represents the time remaining until the timelapse terminates.
        /// </summary>
        public string ProgressTotalLabel => Running ? $"{ntaken} / {ntaking}" : Resources.notrunning;

        /// <summary>
        /// Raise <see cref="BaseNotify.OnPropertyChanged"/> for <see cref="ProgressTotal"/> and <see cref="ProgressTotalLabel"/>.
        /// </summary>
        public void UpdateTotalProgress() {
            OnPropertyChanged(nameof(ProgressTotal));
            OnPropertyChanged(nameof(ProgressTotalLabel));
        }

        #endregion

        
        /// <summary>
        /// Used internally to stop circular updates when changing one property recalculates the others.
        /// </summary>
        private bool fixing = false;

        // 0 - # shots
        // 1 - # duration
        // 2 - # end time
        private int _stopMethod = -1;
        /// <summary>
        /// The type of condition to use for determining when the timelapse should terminate
        /// (0 - # shots, 1 - duration, 2 - end time)
        /// </summary>
        public int StopMethod {
            get => _stopMethod;
            set {
                _stopMethod = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _interval;
        /// <summary>
        /// The interval between "frames".
        /// </summary>
        public TimeSpan Interval {
            get => _interval;
            set {
                _interval = value;
                OnPropertyChanged();
                if (!fixing) Fix();
            }
        }

        private int _nshots;
        /// <summary>
        /// The number of (remaining) "frames" to take before terminating.
        /// </summary>
        public int Nshots {
            get => _nshots;
            set {
                _nshots = value;
                OnPropertyChanged();
                if (!fixing) Fix();
            }
        }

        private TimeSpan _duration;
        /// <summary>
        /// The total (remaining) duration of the timelapse.
        /// </summary>
        public TimeSpan Duration {
            get => _duration;
            set {
                _duration = value;
                OnPropertyChanged();
                if (!fixing) Fix();
            }
        }

        private DateTime _end;
        /// <summary>
        /// The expected end time of the timelapse.
        /// </summary>
        public DateTime End {
            get => _end;
            set {
                _end = value > DateTime.Now ? value : DateTime.Now;
                OnPropertyChanged();
                if (!fixing) Fix();
            }
        }


        /// <summary>
        /// Coerce the values of <see cref="Nshots"/>, <see cref="Duration"/> and <see cref="End"/> based on the
        /// selected <see cref="StopMethod"/>.
        /// </summary>
        public void Fix() {
            if (StopMethod < 0) return;

            fixing = true;

            var ts = (StopMethod == 2 || Running)      ? End - DateTime.Now
                     : (StopMethod == 1)               ? Duration
                     : (StopMethod == 0 && Nshots > 0) ? new TimeSpan((Nshots - 1) * Interval.Ticks)
                                                         : TimeSpan.Zero;
            ts = ts > TimeSpan.Zero ? ts : TimeSpan.Zero;
            
            if (StopMethod != 0)
                Nshots = (int) (ts.Ticks / Interval.Ticks) + 1;
            if (StopMethod != 1 || Running)
                Duration = ts;
            if ((StopMethod != 2 || End < DateTime.Now) && !Running)
                End = DateTime.Now + ts;

            if (Running) {
                ProgressNext = ((double) (DateTime.Now - starttime).Ticks / Interval.Ticks) % 1;
            }

            fixing = false;
        }


        /// <summary>
        /// A timer that handles updating the values as time inevitably progresses.
        /// </summary>
        private readonly System.Timers.Timer updateTimer = new System.Timers.Timer(1000);
        
        /// <summary>
        /// The timer that executes the "frames".
        /// </summary>
        private System.Threading.Timer timer;

        /// <summary>
        /// Create a new timelapse and start the <see cref="updateTimer"/>.
        /// </summary>
        public Timelapse() {
            updateTimer.Elapsed += (sender, e) => Fix();
            updateTimer.Enabled = true;
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <param name="t">Timelapse to copy.</param>
        public Timelapse(Timelapse t) : this() {
            if (t != null) Take(t);
        }

        /// <summary>
        /// The time when the timelapse started.
        /// </summary>
        private DateTime starttime;
        /// <summary>
        /// The number of "frames" already executed since the start of the timelapse.
        /// </summary>
        private int ntaken;
        /// <summary>
        /// The total number of "frames" to be executed.
        /// </summary>
        /// <remarks>This is a copy of the value of <see cref="Nshots"/> from when the timelapse was started.</remarks>
        private int ntaking;
        /// <summary>
        /// The action to execute for each "frame".
        /// </summary>
        private Action<object> action;

        /// <summary>
        /// Execute a frame.
        /// </summary>
        /// <remarks>
        /// <para>This method is repetitively executed by <see cref="timer"/>. As such it is responsible for executing
        /// the <see cref="action"/> and for terminating the timelapse using <see cref="Stop"/> when appropriate.</para>
        /// <para>
        /// The action is executed so long as
        /// <list type="bullet">
        ///     <item>
        ///         <term><c>StopMethod == 0</c> (<see cref="Nshots"/>)</term>
        ///         <description>
        ///             <see cref="ntaken"/> is smaller than <see cref="ntaking"/>;
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>otherwise</term>
        ///         <description>
        ///             <see cref="End"/> has not passed.
        ///         </description>
        ///     </item>
        /// </list>
        /// When this condition is not met, <see cref="Stop"/> is called.
        /// </para>
        /// <para>When a "frame" is executed, <see cref="Nshots"/> is diminished by one, and when it reaches zero,
        /// <see cref="Stop"/> is automatically called.</para>
        /// </remarks>
        /// <param name="state">The state object passed to <see cref="Start"/>.</param>
        private void exec(object state) {
            if (Running && ((StopMethod == 0) ? (ntaken < ntaking) : (DateTime.Now <= End))){
                action(state);
                ++ntaken;
                --Nshots;
                UpdateTotalProgress();
            } else{
                Stop();
            }

            if (Nshots == 0) Stop();
        }

        /// <summary>
        /// Start the timelapse, i.e. set the timer to execute <see cref="exec"/> every <see cref="Interval"/> until stopped.
        /// </summary>
        /// <param name="__action">The action to execute. It is saved in <see cref="action"/> and called as
        /// <c>action(state)</c>.</param>
        /// <param name="state">A state object to pass to the action.</param>
        public void Start(Action<object> __action, object state=null) {
            if (!Running){
                action = __action;
                ntaken = 0;
                ntaking = Nshots;
                starttime = DateTime.Now;
                timer = new System.Threading.Timer(exec, state, TimeSpan.Zero, Interval);
                Running = true;
            }
        }

        /// <summary>
        /// Stop the timelapse. Automatically called at the end of the timelapse, but can also be called prematurely.
        /// </summary>
        public void Stop() {
            if (Running)
                timer?.Dispose();
            timer = null;
            Running = false;
            UpdateTotalProgress();
        }

        /// <summary>
        /// Copy <see cref="t"/> into the current Timelapse.
        /// </summary>
        /// <remarks>Simply transfers over <see cref="StopMethod"/>, <see cref="Interval"/>, <see cref="Nshots"/>,
        /// <see cref="Duration"/>, and <see cref="End"/>, but does not reflect <paramref name="t"/>'s
        /// <see cref="Running"/> status.</remarks>
        /// <param name="t">Timelapse to copy.</param>
        public void Take(Timelapse t) {
            fixing = true;
            StopMethod = t.StopMethod;
            Interval = t.Interval;
            Nshots = t.Nshots;
            Duration = t.Duration;
            End = t.End;
            fixing = false;
        }
    }
}
