using System;
using System.Threading;

namespace AsyncWorker
{
    internal sealed class WorkerSynchronizationContext : SynchronizationContext
    {
        private readonly Worker _worker;
        private readonly Work _work;

        public WorkerSynchronizationContext(Worker worker, Work work = null)
        {
            _worker = worker;
            _work = work;
        }

        public override SynchronizationContext CreateCopy()
        {
            return new WorkerSynchronizationContext(_worker, _work);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            _worker.QueuePost(d, state, _work);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotImplementedException("Send");
        }
    }
}
