using System.Linq;
using System.Threading;

namespace AsyncWorker
{
    internal class WorkSyncContext
    {
        public WorkSyncContext(SyncOptions options)
        {
            Options = options;
            WaitingCount = options.Workers.Length + 1;
        }

        public void RequestSyncToWaiters()
        {
            foreach (var worker in Options.Workers)
            {
                worker.QueueSync(this);
            }
        }

        public void NotifySyncEndToWaiters()
        {
            foreach (var worker in Options.Workers)
            {
                worker.OnSyncEnd(this);
            }
        }

        public void OnOwnerEnter(Worker worker)
        {
            Owner = worker;
            if (Interlocked.Decrement(ref WaitingCount) == 0)
            {
                Owner.OnSyncReady(this);
            }
        }

        public void OnWaiterEnter(Worker worker)
        {
            if (Interlocked.Decrement(ref WaitingCount) == 0)
            {
                Owner.OnSyncReady(this);
            }
        }

        public Worker Owner;
        public SyncOptions Options;
        public int WaitingCount;
    }
}
