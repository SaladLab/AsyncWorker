using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncWorker
{
    internal class WorkSyncContext
    {
        public WorkSyncContext(SyncOptions options)
        {
            Options = options;
            WaitingCount = options.Workers.Length + 1;
            foreach (var worker in Options.Workers)
            {
                worker.QueueSync(this);
            }
        }

        public void OnOwnerEnter(Worker worker)
        {
            Owner = worker;
            if (Interlocked.Decrement(ref WaitingCount) == 0)
            {
                Owner.OnSyncStart(this);
            }
        }

        public void OnWaiterEnter(Worker worker)
        {
            if (Interlocked.Decrement(ref WaitingCount) == 0)
            {
                Owner.OnSyncStart(this);
            }
        }

        public void OnComplete()
        {
            foreach (var worker in Options.Workers)
            {
                worker.OnSyncComplete(this);
            }
        }

        public Worker Owner;
        public SyncOptions Options;
        public int WaitingCount;
    }
}
