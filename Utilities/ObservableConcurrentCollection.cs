using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;

namespace heliomaster {
    public abstract class ObservableConcurrentCollection<T> : INotifyCollectionChanged {
        protected Dispatcher dispatcher;

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        protected void OnCollectionChanged(NotifyCollectionChangedEventArgs args) {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render,
                                                       (Action) (() => CollectionChanged?.Invoke(this, args)));
        }

        protected ObservableConcurrentCollection(Dispatcher d=null) {
            dispatcher = d ?? Application.Current.Dispatcher;
        }
    }

    public class ObservableConcurrentList<T> : ObservableConcurrentCollection<T>, IList<T> {
        private readonly IList<T> _listImplementation = new List<T>();

        public IEnumerator<T> GetEnumerator() => _listImplementation.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>  ((IEnumerable) _listImplementation).GetEnumerator();

        public void Add(T item) {
            _listImplementation.Add(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
        }

        public void Clear() {
            _listImplementation.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(T item) =>  _listImplementation.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => _listImplementation.CopyTo(array, arrayIndex);

        public bool Remove(T item) {
            var ret = _listImplementation.Remove(item);
            if (ret)
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            return ret;
        }

        public int Count => _listImplementation.Count;

        public bool IsReadOnly => _listImplementation.IsReadOnly;

        public int IndexOf(T item) =>  _listImplementation.IndexOf(item);

        public void Insert(int index, T item) {
            _listImplementation.Insert(index, item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public void RemoveAt(int index) {
            _listImplementation.RemoveAt(index);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, index));
        }

        public T this[int index] {
            get => _listImplementation[index];
            set {
                _listImplementation[index] = value;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,value, index));
            }
        }
    }
}
