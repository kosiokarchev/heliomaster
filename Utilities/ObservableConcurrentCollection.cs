using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;

namespace heliomaster {
    /// <summary>
    /// A base class for collections that can be modified from multiple threads but raise their events using a dispatcher.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <remarks>This base class defines the infrastructure to dispatch
    /// <see cref="INotifyCollectionChanged.CollectionChanged"/> events using the given <see cref="Dispatcher"/>
    /// but leaves the implementation of a specific collection modification interface to subclasses.</remarks>
    public abstract class ObservableConcurrentCollection<T> : INotifyCollectionChanged {
        /// <summary>
        /// The dispatcher to use to raise <see cref="INotifyCollectionChanged.CollectionChanged"/> events.
        /// </summary>
        /// <remarks>Set in the constructor and defaults to the UI thread <c>Application.Current.Dispatcher</c>.</remarks>
        protected Dispatcher Dispatcher;

        /// <summary>
        /// Raised when the collection has been modified. See <see cref="INotifyCollectionChanged.CollectionChanged"/>.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        protected void CollectionChangedRaise(NotifyCollectionChangedEventArgs args) {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render,
                                                       (Action) (() => CollectionChanged?.Invoke(this, args)));
        }

        /// <summary>
        /// Create a new ObservableConcurrentCollection with the given <paramref name="dispatcher"/> or
        /// <c>Application.Current.Dispatcher</c> if <c>null</c> is passed as the parameter.
        /// </summary>
        protected ObservableConcurrentCollection(Dispatcher dispatcher=null) {
            Dispatcher = dispatcher ?? Application.Current.Dispatcher;
        }
    }

    /// <summary>
    /// A <see cref="List{T}"/> that can be modified from multiple threads and raises
    /// <see cref="INotifyCollectionChanged.CollectionChanged"/> using a <see cref="Dispatcher"/>. 
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <remarks>The <see cref="IList{T}"/> interface is implemented by delegating to a <see cref="List{T}"/> instance
    /// <see cref="_listImplementation"/>. In addition, the implementations of those members of <see cref="IList{T}"/>
    /// that modify the contents of the list also raise the <see cref="INotifyCollectionChanged.CollectionChanged"/>
    /// with the appropriate <see cref="NotifyCollectionChangedAction"/>.</remarks>
    public class ObservableConcurrentList<T> : ObservableConcurrentCollection<T>, IList<T> {
        private readonly IList<T> _listImplementation = new List<T>();

        public IEnumerator<T> GetEnumerator() => _listImplementation.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>  ((IEnumerable) _listImplementation).GetEnumerator();

        public void Add(T item) {
            _listImplementation.Add(item);
            CollectionChangedRaise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
        }

        public void Clear() {
            _listImplementation.Clear();
            CollectionChangedRaise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(T item) =>  _listImplementation.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => _listImplementation.CopyTo(array, arrayIndex);

        public bool Remove(T item) {
            var ret = _listImplementation.Remove(item);
            if (ret)
                CollectionChangedRaise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            return ret;
        }

        public int Count => _listImplementation.Count;

        public bool IsReadOnly => _listImplementation.IsReadOnly;

        public int IndexOf(T item) =>  _listImplementation.IndexOf(item);

        public void Insert(int index, T item) {
            _listImplementation.Insert(index, item);
            CollectionChangedRaise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public void RemoveAt(int index) {
            _listImplementation.RemoveAt(index);
            CollectionChangedRaise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, index));
        }

        public T this[int index] {
            get => _listImplementation[index];
            set {
                _listImplementation[index] = value;
                CollectionChangedRaise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, index));
            }
        }
    }
}
