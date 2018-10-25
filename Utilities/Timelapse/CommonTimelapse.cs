using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace heliomaster {
    /// <summary>
    /// A class to control, synchronise and operate a number of <see cref="Timelapse"/>s.
    /// </summary>
    public class CommonTimelapse : BaseNotify {
        /// <summary>
        /// The collection of timelapses.
        /// </summary>
        private Timelapse[] _ = new Timelapse[0];
        
        private int _i0;
        /// <summary>
        /// The index of the "main" timelapse.
        /// </summary>
        /// <remarks> Setting its value unfrees the current main timelapse and frees the new one but also ties it. See
        /// <see cref="Tied"/> for the distinction between being <see cref="Tied"/> and <see cref="Timelapse.Free"/>.
        /// </remarks>
        public int i0 {
            get => _i0;
            set {
                _[_i0].Free            =  false;
                _[_i0].PropertyChanged -= Coerce;

                _i0 = value;

                _[_i0].PropertyChanged += Coerce;
                _[_i0].Free            =  true;

                OnPropertyChanged();

                Tied[_i0] = true;
                OnPropertyChanged(nameof(Tied));
            }
        }

        /// <summary>
        /// A collection that indicates which timelapses are controlled by (tied to) the main one.
        /// </summary>
        /// <remarks> All tied timelapses share the same parameters: those of the main one. This means that they are
        /// not editable by the user (they are not <see cref="Timelapse.Free"/>), with the exception of the main
        /// one.</remarks>
        public ObservableCollection<bool> Tied { get; } = new ObservableCollection<bool>();
        
        /// <summary>
        /// Implements the logic connecting <see cref="Tied"/> and <see cref="Timelapse.Free"/>, i.e. frees an untied
        /// timelapse and unfrees a timelapse being tied.
        /// </summary>
        /// <param name="i">The index into of the timelapse to tie/untie.</param>
        /// <param name="tied">Whether to tie the <paramref name="i"/>'th timelapse.</param>
        public void SetTied(int i, bool tied) {
            if (i != i0) {
                Tied[i]   = tied;
                _[i].Free = !Tied[i];
            }
        }

        /// <summary>
        /// Make a CommonTimelapse containing <paramref name="n"/> timelapses synchronised to <paramref name="t"/>.
        /// </summary>
        /// <param name="t">The Timelapse to use as a main one. Set at index <see cref="i0"/>=0.</param>
        /// <param name="n">The total number of timelapses in the CommonTiemlapse.</param>
        /// <param name="tied">Whether to automatically tie all newly created timelapses.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void Make(Timelapse t, int n, bool tied=true) {
            if (n < 1)
                throw new ArgumentOutOfRangeException($"{nameof(n)} must be at least 1");
            
            _    = new Timelapse[n];
            _[0] = t;
            Tied.Add(true);
            for (var i = 1; i < n; ++i) {
                _[i] = new Timelapse(_[0]) {Free = !tied};
                Tied.Add(tied);
            }
            i0 = 0;
        }

        
        /// <summary>
        /// Copy the main timelapse into all the other tied timelapses.
        /// </summary>
        public void Coerce() {
            for (var i=0; i<_.Length; ++i)
                if (i!=i0 && Tied[i])
                    _[i].Take(_[i0]);
        }
        /// <summary>
        /// An override that simply calls <see cref="Coerce"/> but is still acceptable as a <see cref="PropertyChangedEventHandler"/>.
        /// </summary>
        /// <param name="o">Dummy argument.</param>
        /// <param name="args">Dummy argument.</param>
        private void Coerce(object o, PropertyChangedEventArgs args) => Coerce();

        /// <summary>
        /// Start all the tied timelapses simultaneously with the same settings.
        /// </summary>
        /// <param name="a">The action to be performed by each Timelapse.</param>
        /// <param name="states">A collection of state objects, one for each Timelapse (regardless of whether it is
        /// tied or not).</param>
        public void Start(Action<object> a, IEnumerable states) {
            Coerce();

            var i = 0;
            foreach (var state in states) {
                if (Tied[i])
                    _[i].Start(a, state);
                ++i;
            }
        }
        /// <summary>
        /// Stop all tied timelapses.
        /// </summary>
        public void Stop() {
            for (var i = 0; i < _.Length; ++i)
                if (Tied[i])
                    _[i].Stop();
        }

        /// <summary>
        /// Tie all the timelapses.
        /// </summary>
        public void TieAll() {
            for (var i = 0; i < Tied.Count; i++)
                SetTied(i, true);
        }

        /// <summary>
        /// Retrieve the <paramref name="i"/>'th timelapse from the collection. 
        /// </summary>
        public Timelapse this[int i] => _[i];
        
        /// <summary>
        /// Get the number of tiemlapses in the collection.
        /// </summary>
        public int Length => _.Length;
        
        /// <summary>
        /// Return the index of the given timelapse in the collection, or -1 if it is not found.
        /// </summary>
        /// <param name="t">The timelapse to search for.</param>
        public int IndexOf(Timelapse t) {
            for (var i=0; i<_.Length; ++i)
                if (t.Equals(_[i]))
                    return i;
            return -1;
        }
        
        /// <summary>
        /// Get the main timelapse if one is set.
        /// </summary>
        public Timelapse Main => i0 < _.Length ? _[i0] : null;
    }
}
