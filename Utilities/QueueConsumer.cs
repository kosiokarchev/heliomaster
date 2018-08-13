using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

namespace heliomaster_wpf {
    public class QueueItem<Tin,Tout> {
        public Func<Tin, Tout> Func;
        public Tin             Param;
        public Tout            Result;

        public void Invoke() {
            Result = Func(Param);
            Complete?.Invoke(this, Result);
        }

        public event EventHandler<Tout> Complete;
    }

    public class QueueItem : QueueItem<object, object> { }

    public class QueueConsumer<Tin, Tout> {
        private readonly ConcurrentQueue<QueueItem<Tin, Tout>>[] queues;
        private QueueItem<Tin, Tout> curitem;
        private Task consumer;
        private Timer timer;

        public QueueConsumer(int n) {
            queues = new ConcurrentQueue<QueueItem<Tin, Tout>>[n];
            for (var i=0; i<n; ++i)
                queues[i] = new ConcurrentQueue<QueueItem<Tin, Tout>>();

            var ts = new TimeSpan(0, 0, 1);
            timer = new Timer(Run, null, ts, ts);
        }

        public void Run(object state=null) {
            if (consumer == null || consumer.IsCompleted)
                consumer = Task.Run((Action) consume);
        }

        private void consume() {
            while (queues.Any(q => q.TryDequeue(out curitem)))
                curitem.Invoke();
        }

        public QueueItem<Tin, Tout> Enqueue(QueueItem<Tin, Tout> item, int i=0) {
            queues[i].Enqueue(item);
            Run();
            return item;
        }
        public void Enqueue(Func<Tin, Tout> action, Tin state, int i = 0) {
            Enqueue(new QueueItem<Tin, Tout> {Func = action, Param = state}, i);
        }

        public List<QueueItem<Tin, Tout>> Clear(int i) {
            var items = new List<QueueItem<Tin, Tout>>();
            QueueItem<Tin, Tout> qi;
            while (queues[i].TryDequeue(out qi)) items.Add(qi);
            return items;
        }

        public List<List<QueueItem<Tin, Tout>>> Clear() {
            var ret = new List<List<QueueItem<Tin, Tout>>>();
            for (var i = 0; i < queues.Length; ++i) ret.Add(Clear(i));
            return ret;
        }

        public async Task<List<List<QueueItem<Tin, Tout>>>> ClearTask() {
            var ret = Clear();
            await consumer;
            return ret;
        }
    }

    public class QueueConsumer : QueueConsumer<object, object> {
        public QueueConsumer(int n) : base(n) { }
    }
}
