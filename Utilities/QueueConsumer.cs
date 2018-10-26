using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Threading.Timer;

namespace heliomaster {
    /// <summary>
    /// An element in the <see cref="QueueConsumer{Tin, Tout}"/> to be executed.
    /// </summary>
    /// <typeparam name="Tin">The type of the parameter passed to <see cref="Func"/>.</typeparam>
    /// <typeparam name="Tout">The return type of <see cref="Func"/>.</typeparam>
    public class QueueItem<Tin,Tout> {
        /// <summary>
        /// The function to execute as <c>Func(</c><see cref="Param"/><c>)</c>.
        /// </summary>
        public Func<Tin, Tout> Func;
        /// <summary>
        /// The parameter to pass to <see cref="Func"/>.
        /// </summary>
        public Tin Param;
        /// <summary>
        /// The result of running <c>Func(</c><see cref="Param"/><c>)</c>.
        /// </summary>
        public Tout Result;

        /// <summary>
        /// Execute <c>Func(</c><see cref="Param"/><c>)</c>, save the result in <see cref="Result"/> and raise <see cref="Complete"/>.
        /// </summary>
        public void Invoke() {
            Result = Func(Param);
            Complete?.Invoke(this, Result);
        }

        /// <summary>
        /// Raised when the item's <see cref="Func"/> has finished running.
        /// </summary>
        public event EventHandler<Tout> Complete;
    }

    /// <summary>
    /// A class that handles a number of FIFO queues and executes the <see cref="QueueItem{Tin,Tout}"/> in them in
    /// sequence based on the priorities of the queues.
    /// </summary>
    /// <remarks>Queues are ordered by priority in <see cref="queues"/> with the zero-indexed one being given highest
    /// priority. The consumption process is realised in <see cref="Run"/>: it spawns a process which loops until there
    /// are no items left in any of the queues. At each iteration the highest-priority item is executed irrespective
    /// of what the priority of the last executed item was. Each enqueued item triggers <see cref="Run"/>, but if
    /// another process is already running, it returns immediately, and the item is left to be processed by the current
    /// consumer. Every 1s a <see cref="timer"/> runs the function to ensure that items enqueued during the last sweep
    /// of the consumer are not left hanging.</remarks>
    /// <typeparam name="Tin">The type of the parameter passed to <see cref="QueueItem{Tin, Tout}.Func"/>.</typeparam>
    /// <typeparam name="Tout">The return type of <see cref="QueueItem{Tin, Tout}.Func"/>.</typeparam>
    public class QueueConsumer<Tin, Tout> {
        /// <summary>
        /// The queues containing the actions to be performed.
        /// </summary>
        private readonly ConcurrentQueue<QueueItem<Tin, Tout>>[] queues;
        
        /// <summary>
        /// The <see cref="QueueItem{Tin,Tout}"/> currently being executed.
        /// </summary>
        private QueueItem<Tin, Tout> curitem;
        
        /// <summary>
        /// The <see cref="Task"/> which is executing the current <see cref="QueueItem{Tin,Tout}"/>.  
        /// </summary>
        private Task consumer;
        
        /// <summary>
        /// A timer that runs the consumption procedure every second.
        /// </summary>
        /// <remarks>This is necessary because if an item is enqueued during the last sweep of the consumer across
        /// the queues to a queue of higher priority that the one the consumer is checking at the same time, the
        /// consumer run by the enqueuing will return immediately since another one is currently running, but that
        /// one will terminate before seeing the new item.</remarks>
        private Timer timer;


        /// <summary>
        /// Create a new QueueConsumer with <paramref name="n"/> queues.
        /// </summary>
        /// <param name="n">The number of queues to create.</param>
        public QueueConsumer(int n) {
            queues = new ConcurrentQueue<QueueItem<Tin, Tout>>[n];
            for (var i=0; i<n; ++i)
                queues[i] = new ConcurrentQueue<QueueItem<Tin, Tout>>();

            var ts = TimeSpan.FromSeconds(1);
            timer = new Timer(Run, null, ts, ts);
        }
        
        /// <summary>
        /// A semaphore used to serialize the consumption process.
        /// </summary>
        private readonly SemaphoreSlim sem = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Run the consumption process.
        /// </summary>
        public void Run(object state=null) {
            if (sem.CurrentCount > 0)
                consumer = Task.Run(() => {
                    sem.Wait();
                    try {
                        while (queues.Any(q => q.TryDequeue(out curitem)))
                            curitem.Invoke();
                    } finally { sem.Release(); }
                });
        }

        /// <summary>
        /// Put the given <see cref="QueueItem{Tin,Tout}"/> on the queue of index <paramref name="i"/>.
        /// </summary>
        /// <param name="item">The <see cref="QueueItem{Tin,Tout}"/> to enqueue.</param>
        /// <param name="i">The index of the queue to put the item on.</param>
        /// <returns>The enqueued item.</returns>
        public QueueItem<Tin, Tout> Enqueue(QueueItem<Tin, Tout> item, int i=0) {
            queues[i].Enqueue(item);
            Run();
            return item;
        }
        /// <summary>
        /// Put a new <see cref="QueueItem{Tin,Tout}"/> constructed from the given <paramref name="func"/> and
        /// <paramref name="param"/> on queue <paramref name="i"/>.
        /// </summary>
        /// <param name="func">The <see cref="QueueItem{Tin,Tout}.Func"/> of the item.</param>
        /// <param name="param">The <see cref="QueueItem{Tin,Tout}.Param"/> of the item.</param>
        /// <param name="i">The index of the queue to put the item on.</param>
        /// <returns>The created and enqueued item.</returns>
        public QueueItem<Tin, Tout> Enqueue(Func<Tin, Tout> func, Tin param, int i = 0) {
            return Enqueue(new QueueItem<Tin, Tout> {Func = func, Param = param}, i);
        }

        /// <summary>
        /// Remove all the items from queue <paramref name="i"/> and return them.
        /// </summary>
        /// <param name="i">The index of the queue to remove the items from.</param>
        /// <returns>The items from queue <paramref name="i"/>.</returns>
        public List<QueueItem<Tin, Tout>> Clear(int i) {
            var items = new List<QueueItem<Tin, Tout>>();
            QueueItem<Tin, Tout> qi;
            while (queues[i].TryDequeue(out qi)) items.Add(qi);
            return items;
        }

        /// <summary>
        /// Remove all the items from all the queues and return them as a list of lists (one inner list for each queue).
        /// </summary>
        /// <returns></returns>
        public List<List<QueueItem<Tin, Tout>>> Clear() {
            var ret = new List<List<QueueItem<Tin, Tout>>>();
            for (var i = 0; i < queues.Length; ++i) ret.Add(Clear(i));
            return ret;
        }

        /// <summary>
        /// Clear all queues and wait asynchronously for the <see cref="consumer"/> to finish.
        /// </summary>
        /// <returns>A <see cref="Task"/> that returns the result of <see cref="Clear()"/></returns>
        public async Task<List<List<QueueItem<Tin, Tout>>>> ClearTask() {
            var ret = Clear();
            await consumer;
            return ret;
        }
    }
}
