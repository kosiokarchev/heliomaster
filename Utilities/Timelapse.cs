using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.RightsManagement;
using heliomaster.Properties;
using heliomaster.Annotations;

namespace heliomaster {
    public class Timelapse : BaseNotify {
        private bool _free = true;
        public bool Free {
            get => _free;
            set { _free = value; OnPropertyChanged(); OnPropertyChanged(nameof(Editable)); }
        }

        private bool _running;
        public bool Running {
            get => _running;
            set { _running = value; OnPropertyChanged(); OnPropertyChanged(nameof(Editable)); }
        }

        public bool Editable => !Running && Free;


        private double _progressNext;
        public double ProgressNext {
            get => _progressNext;
            private set {
                _progressNext = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressNextLabel));
            }
        }

        public string ProgressNextLabel => Running ? $"frame in {(1-ProgressNext) * Interval.TotalSeconds:F0}s" : null;

        public double ProgressTotal => Running ? (double) ntaken / ntaking : 0;
        public string ProgressTotalLabel => Running ? $"{ntaken} / {ntaking}" : Resources.notrunning;

        public void UpdateTotalProgress() {
            OnPropertyChanged(nameof(ProgressTotal));
            OnPropertyChanged(nameof(ProgressTotalLabel));
        }

        private bool fixing = false;

        // 0 - # shots
        // 1 - # duration
        // 2 - # end time
        private int _stopMethod = -1;
        public int StopMethod {
            get => _stopMethod;
            set {
                _stopMethod = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _interval;
        public TimeSpan Interval {
            get => _interval;
            set {
                _interval = value;
                OnPropertyChanged();
                if (!fixing) Fix();
            }
        }

        private int _nshots;
        public int Nshots {
            get => _nshots;
            set {
                _nshots = value;
                OnPropertyChanged();
                if (!fixing) Fix();
            }
        }

        private TimeSpan _duration;
        public TimeSpan Duration {
            get => _duration;
            set {
                _duration = value;
                OnPropertyChanged();
                if (!fixing) Fix();
            }
        }

        private DateTime _end;
        public DateTime End {
            get => _end;
            set {
                _end = value > DateTime.Now ? value : DateTime.Now;
                OnPropertyChanged();
                if (!fixing) Fix();
            }
        }

        public TimeSpan TimeSpan {
            get {
                var ret = (StopMethod == 2 || Running) ? End - DateTime.Now :
                          (StopMethod == 1) ? Duration :
                          (StopMethod == 0 && Nshots > 0) ? new TimeSpan((Nshots - 1) * Interval.Ticks)
                          : TimeSpan.Zero;
                return ret > TimeSpan.Zero ? ret : TimeSpan.Zero;
            }
        }



        public void Fix() {
            if (StopMethod < 0) return;

            fixing = true;

            var ts = TimeSpan;
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

        private readonly System.Timers.Timer updateTimer = new System.Timers.Timer(1000);
        private System.Threading.Timer timer;

        public Timelapse() {
            updateTimer.Elapsed += (sender, e) => Fix();
            updateTimer.Enabled = true;
        }

        public Timelapse(Timelapse t) : this() {
            if (t != null) Take(t);
        }

        private DateTime starttime;
        private int ntaken, ntaking;
        private Action<object> action;

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

        public void Stop() {
            if (Running)
                timer?.Dispose();
            timer = null;
            Running = false;
            UpdateTotalProgress();
        }

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

    public class CommonTimelapse : BaseNotify {
        private Timelapse[] _;
        private int _i0;
        public int i0 {
            get => _i0;
            set {
                _[_i0].Free = false;
                _[_i0].PropertyChanged -= Coerce;

                _i0 = value;

                _[_i0].PropertyChanged += Coerce;
                _[_i0].Free = true;

                OnPropertyChanged();

                Tied[_i0] = true;
                OnPropertyChanged(nameof(Tied));
            }
        }

        public ObservableCollection<bool> Tied { get; } = new ObservableCollection<bool>();
        public void SetTied(int i, bool val) {
            if (i != i0) {
                Tied[i] = val;
                _[i].Free = !Tied[i];
            }
        }

        public void Make(Timelapse t, int n, bool tied=true) {
            if (n < 1)
                throw new ArgumentOutOfRangeException($"{nameof(n)} must be at least 1");
            _ = new Timelapse[n];
            _[0] = t;
            Tied.Add(true);
            for (var i = 1; i < n; ++i) {
                _[i] = new Timelapse(_[0]) {Free = !tied};
                Tied.Add(tied);
            }
            i0 = 0;
        }

        private void Coerce(object o, PropertyChangedEventArgs args) {
            Coerce();
        }
        public void Coerce() {
            for (var i=0; i<_.Length; ++i)
                if (i!=i0 && Tied[i])
                    _[i].Take(_[i0]);
        }

        public void Start(Action<object> a, IEnumerable states) {
            Coerce();

            var i = 0;
            foreach (var state in states) {
                if (Tied[i])
                    _[i].Start(a, state);
                ++i;
            }
        }
        public void Stop() {
            for (var i = 0; i < _.Length; ++i)
                if (Tied[i])
                    _[i].Stop();
        }

        public void TieAll() {
            for (var i = 0; i < Tied.Count; i++)
                SetTied(i, true);
        }

        public Timelapse this[int i] => _[i];
        public int Length => _.Length;
        public int IndexOf(Timelapse t) {
            for (var i=0; i<_.Length; ++i)
                if (t.Equals(_[i]))
                    return i;
            return -1;
        }
        public Timelapse Main => _[i0];
    }
}
