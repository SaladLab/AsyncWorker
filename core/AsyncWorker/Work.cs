using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncWorker
{
    internal class Work
    {
        public int Options;
        public TaskCompletionSource<Task> CompletionSource;
        public WorkSyncContext Sync;

        public virtual Task Invoke()
        {
            return null;
        }
    }

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

    // Work with Action
    internal class WorkA : Work
    {
        public Action Action;

        public override Task Invoke()
        {
            Action();
            return null;
        }
    }

    // Work with Action and State
    internal class WorkAs : Work
    {
        public Action<object> Action;
        public object State;

        public override Task Invoke()
        {
            Action(State);
            return null;
        }
    }

    // Work with Func
    internal class WorkF : Work
    {
        public Func<Task> Function;

        public override Task Invoke()
        {
            return Function();
        }
    }

    // Work with Func and State
    internal class WorkFs : Work
    {
        public Func<object, Task> Function;
        public object State;

        public override Task Invoke()
        {
            return Function(State);
        }
    }

    // Work with Func and Token
    internal class WorkFt : Work
    {
        public Func<CancellationToken, Task> Function;
        public CancellationToken Token;

        public override Task Invoke()
        {
            return Function(Token);
        }
    }

    // Work with Func, State and Token
    internal class WorkFst : Work
    {
        public Func<object, CancellationToken, Task> Function;
        public object State;
        public CancellationToken Token;

        public override Task Invoke()
        {
            return Function(State, Token);
        }
    }

    // Work for Post
    internal class WorkPost : Work
    {
        public SendOrPostCallback Action;
        public object State;

        public override Task Invoke()
        {
            Action(State);
            return null;
        }
    }

    // Work for Sync
    internal class WorkSync : Work
    {
        public WorkSyncContext Source;
    }
}
