﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncWorker
{
    public class Worker : IDisposable
    {
        private readonly object _lock = new object();
        private readonly string _name;
        private bool _isDisposed;
        private bool _isWorkLoopSpawned;
        private bool _isInAtomic;
        private bool _isInBarrier;
        private int _runningTaskCount;
        private Queue<Work> _workQueue = new Queue<Work>();
        private Queue<Work> _pendingWorkQueue;
        private Queue<Work> _barrierWorkQueue;
        private Work _atomicWork;
        private Work _waitingBarrier;
        private WorkSync _waitingSync;
        private Work _waitingSyncedWork;
        private readonly WorkerSynchronizationContext _synchronizationContext;
        private CancellationTokenSource _cancelTokenSource;

        /// <summary>
        /// Name of worker
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// When an exception is unhandled internally, this action will be called.
        /// This call is not guaranteed to be serialized.
        /// </summary>
        public event Action<Worker, Exception> UnhandledException;

        public Worker(string name = null)
        {
            _synchronizationContext = new WorkerSynchronizationContext(this);
            _name = name;
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            lock (_lock)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                CancelAllInvoke();
            }
        }

        private CancellationToken CreateCancellationToken()
        {
            if (_cancelTokenSource == null)
            {
                lock (_lock)
                {
                    _cancelTokenSource = new CancellationTokenSource();
                }
            }

            return _cancelTokenSource.Token;
        }

        private void ValidateSyncOptions(SyncOptions syncOptions)
        {
            if (syncOptions.Workers.Length == 0)
                throw new ArgumentException(nameof(syncOptions), "The number of worker should be greater than 0");

            if (syncOptions.Workers.Any(w => w == this))
                throw new ArgumentException(nameof(syncOptions), "There is this worker in Workers");

            if (syncOptions.Workers.Length != syncOptions.Workers.Distinct().Count())
                throw new ArgumentException(nameof(syncOptions), "There is a duplicate worker in Workers");
        }

        /// <summary>
        /// Queues a method for execution. The method executes when this worker is ready.
        /// </summary>
        /// <param name="action">A method to be executed.</param>
        public void Invoke(Action action,
                           InvokeOptions options = InvokeOptions.Normal,
                           SyncOptions syncOptions = null)
        {
            if (options.HasFlag(InvokeOptions.Atomic))
                throw new ArgumentException("Atomic should be used with Task");
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkA
            {
                Action = action,
                Options = (WorkOptions)((int)options),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
        }

        /// <summary>
        /// Queues a method for execution. The method executes when this worker is ready.
        /// </summary>
        /// <param name="action">A method to be executed.</param>
        /// <param name="state">An object containing data to be used by the method.</param>
        public void Invoke(Action<object> action, object state,
                           InvokeOptions options = InvokeOptions.Normal,
                           SyncOptions syncOptions = null)
        {
            if (options.HasFlag(InvokeOptions.Atomic))
                throw new ArgumentException("Atomic should be used with Task");
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkAs
            {
                Action = action,
                State = state,
                Options = (WorkOptions)((int)options),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
        }

        /// <summary>
        /// Queues a method for execution. The method executes when this worker is ready.
        /// </summary>
        /// <param name="function">The work to execute asynchronously.</param>
        public void Invoke(Func<Task> function,
                           InvokeOptions options = InvokeOptions.Normal,
                           SyncOptions syncOptions = null)
        {
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkF
            {
                Function = function,
                Options = (WorkOptions)((int)options),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
        }

        /// <summary>
        /// Queues a method for execution. The method executes when this worker is ready.
        /// </summary>
        /// <param name="function">The work to execute asynchronously.</param>
        /// <param name="state">An object containing data to be used by the work.</param>
        public void Invoke(Func<object, Task> function, object state,
                           InvokeOptions options = InvokeOptions.Normal,
                           SyncOptions syncOptions = null)
        {
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkFs
            {
                Function = function,
                State = state,
                Options = (WorkOptions)((int)options),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
        }

        /// <summary>
        /// Queues a method for execution. The method executes when this worker is ready.
        /// </summary>
        /// <param name="function">The work to execute asynchronously. CancellationToken will be set when worker is closed.</param>
        public void Invoke(Func<CancellationToken, Task> function,
                           InvokeOptions options = InvokeOptions.Normal,
                           SyncOptions syncOptions = null)
        {
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkFt
            {
                Function = function,
                Token = CreateCancellationToken(),
                Options = (WorkOptions)((int)options),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
        }

        /// <summary>
        /// Queues a method for execution. The method executes when this worker is ready.
        /// </summary>
        /// <param name="function">The work to execute asynchronously. CancellationToken will be set when worker is closed.</param>
        /// <param name="state">An object containing data to be used by the work.</param>
        public void Invoke(Func<object, CancellationToken, Task> function, object state,
                           InvokeOptions options = InvokeOptions.Normal,
                           SyncOptions syncOptions = null)
        {
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkFst
            {
                Function = function,
                State = state,
                Token = CreateCancellationToken(),
                Options = (WorkOptions)((int)options),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
        }

        /// <summary>
        /// Queues the specified work to run when this worker is ready and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="function">The work to execute asynchronously.</param>
        public Task<Task> InvokeAsync(Func<Task> function,
                                      InvokeOptions options = InvokeOptions.Normal,
                                      SyncOptions syncOptions = null)
        {
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkF
            {
                Function = function,
                Options = (WorkOptions)((int)options),
                CompletionSource = new TaskCompletionSource<Task>(),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
            return work.CompletionSource.Task;
        }

        /// <summary>
        /// Queues the specified work to run when this worker is ready and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="function">The work to execute asynchronously.</param>
        /// <param name="state">An object containing data to be used by the work.</param>
        public Task<Task> InvokeAsync(Func<object, Task> function, object state,
                                      InvokeOptions options = InvokeOptions.Normal,
                                      SyncOptions syncOptions = null)
        {
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkFs
            {
                Function = function,
                State = state,
                Options = (WorkOptions)((int)options),
                CompletionSource = new TaskCompletionSource<Task>(),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
            return work.CompletionSource.Task;
        }

        /// <summary>
        /// Queues the specified work to run when this worker is ready and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="function">The work to execute asynchronously. CancellationToken will be set when worker is closed.</param>
        public Task<Task> InvokeAsync(Func<CancellationToken, Task> function,
                                      InvokeOptions options = InvokeOptions.Normal,
                                      SyncOptions syncOptions = null)
        {
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkFt
            {
                Function = function,
                Token = CreateCancellationToken(),
                Options = (WorkOptions)((int)options),
                CompletionSource = new TaskCompletionSource<Task>(),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
            return work.CompletionSource.Task;
        }

        /// <summary>
        /// Queues the specified work to run when this worker is ready and returns a proxy for the task returned by function.
        /// </summary>
        /// <param name="function">The work to execute asynchronously. CancellationToken will be set when worker is closed.</param>
        /// <param name="state">An object containing data to be used by the work.</param>
        public Task<Task> InvokeAsync(Func<object, CancellationToken, Task> function, object state,
                                      InvokeOptions options = InvokeOptions.Normal,
                                      SyncOptions syncOptions = null)
        {
            if (syncOptions != null)
                ValidateSyncOptions(syncOptions);

            var work = new WorkFst
            {
                Function = function,
                State = state,
                Token = CreateCancellationToken(),
                Options = (WorkOptions)((int)options),
                CompletionSource = new TaskCompletionSource<Task>(),
                Sync = syncOptions != null ? new WorkSyncContext(syncOptions) : null
            };
            QueueWork(work);
            return work.CompletionSource.Task;
        }

        /// <summary>
        /// Sets barrier.
        /// All queued works before this barrier finish working before another queued works after this call.
        /// </summary>
        public void SetBarrier()
        {
            var work = new Work
            {
                Options = WorkOptions.Barrier
            };
            QueueBarrier(work);
        }

        /// <summary>
        /// Sets barrier and returns a proxy task which will be completed when barrier is consumed.
        /// All queued works before this barrier finish working before another queued works after this call.
        /// </summary>
        public Task SetBarrierAsync()
        {
            var work = new Work
            {
                Options = WorkOptions.Barrier,
                CompletionSource = new TaskCompletionSource<Task>()
            };
            QueueBarrier(work);
            return work.CompletionSource.Task;
        }

        private void QueueWork(Work work)
        {
            lock (_lock)
            {
                if (_isDisposed)
                {
                    if (work.CompletionSource != null)
                        work.CompletionSource.SetCanceled();
                    return;
                }

                if (_isInBarrier)
                {
                    _barrierWorkQueue.Enqueue(work);
                }
                else if (_isInAtomic)
                {
                    _pendingWorkQueue.Enqueue(work);
                }
                else
                {
                    _workQueue.Enqueue(work);
                    if (_isWorkLoopSpawned == false)
                    {
                        _isWorkLoopSpawned = true;
                        ThreadPool.UnsafeQueueUserWorkItem(WorkLoop, null);
                    }
                }
            }

            if (work.Sync != null)
                work.Sync.RequestSyncToWaiters();
        }

        internal void QueuePost(SendOrPostCallback action, object state, Work ownerWork)
        {
            var work = new WorkPost
            {
                Action = action,
                State = state,
                Options = WorkOptions.Post
            };

            if (ownerWork != null)
            {
                if (ownerWork.Sync != null && ownerWork.Options.HasFlag(WorkOptions.Atomic) == false)
                {
                    work.Sync = new WorkSyncContext(ownerWork.Sync.Options);
                    work.Sync.RequestSyncToWaiters();
                }
            }

            bool willSpawn = false;
            lock (_lock)
            {
                if (_isInAtomic)
                {
                    if (_atomicWork == ownerWork)
                        _workQueue.Enqueue(work);
                    else
                        _pendingWorkQueue.Enqueue(work);
                }
                else
                {
                    _workQueue.Enqueue(work);
                }

                if (_isWorkLoopSpawned == false)
                {
                    _isWorkLoopSpawned = true;
                    willSpawn = true;
                }
            }
            if (willSpawn)
            {
                WorkLoop(null);
            }
        }

        private void QueueBarrier(Work work)
        {
            lock (_lock)
            {
                QueueWork(work);
                if (_isInBarrier == false)
                {
                    _isInBarrier = true;
                    if (_barrierWorkQueue == null)
                        _barrierWorkQueue = new Queue<Work>();
                }
            }
        }

        internal void QueueSync(WorkSyncContext source)
        {
            var work = new WorkSync
            {
                Source = source,
                Options = WorkOptions.Sync
            };

            bool willSpawn = false;
            lock (_lock)
            {
                _workQueue.Enqueue(work);
                if (_isWorkLoopSpawned == false)
                {
                    _isWorkLoopSpawned = true;
                    willSpawn = true;
                }
            }
            if (willSpawn)
            {
                WorkLoop(null);
            }
        }

        private void CancelAllInvoke()
        {
            if (_cancelTokenSource != null)
                _cancelTokenSource.Cancel();

            foreach (var w in _workQueue)
            {
                if (w.CompletionSource != null)
                    w.CompletionSource.SetCanceled();
            }
            _workQueue = new Queue<Work>(
                _workQueue.Where(w => w.Options.HasFlag(WorkOptions.Post)));

            if (_pendingWorkQueue != null)
            {
                foreach (var w in _pendingWorkQueue)
                {
                    if (w.CompletionSource != null)
                        w.CompletionSource.SetCanceled();
                }
                _pendingWorkQueue = new Queue<Work>(
                    _pendingWorkQueue.Where(w => w.Options.HasFlag(WorkOptions.Post)));
            }
        }

        private static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        private void WorkLoop(object state)
        {
            using (new SynchronizationContextSwitcher(_synchronizationContext))
            {
                while (true)
                {
                    Work work;
                    lock (_lock)
                    {
                        if (_waitingSync != null || _waitingSyncedWork != null)
                        {
                            _isWorkLoopSpawned = false;
                            break;
                        }
                        if (_workQueue.Count == 0)
                        {
                            _isWorkLoopSpawned = false;
                            break;
                        }

                        work = _workQueue.Dequeue();
                        if (work.Sync != null)
                        {
                            _waitingSyncedWork = work;
                            work.Sync.OnOwnerEnter(this);
                            continue;
                        }

                        if (work.Options.HasFlag(WorkOptions.Atomic))
                        {
                            if (_isInAtomic)
                                throw new InvalidOperationException("Already in atomic");

                            _isInAtomic = true;
                            _atomicWork = work;
                            Swap(ref _workQueue, ref _pendingWorkQueue);
                            if (_workQueue == null)
                                _workQueue = new Queue<Work>();
                        }
                        else if (work.Options.HasFlag(WorkOptions.Barrier))
                        {
                            if (_runningTaskCount > 0)
                            {
                                _waitingBarrier = work;
                            }
                            else
                            {
                                OnBarrierComplete(work);
                            }
                            continue;
                        }
                        else if (work.Options.HasFlag(WorkOptions.Sync))
                        {
                            _waitingSync = (WorkSync)work;
                            var source = _waitingSync.Source;
                            source.OnWaiterEnter(this);
                            continue;
                        }
                    }

                    ProcessWork(work);
                }
            }
        }

        private void ProcessWork(Work work)
        {
            WorkerSynchronizationContext syncCtx = null;
            if (work.Options.HasFlag(WorkOptions.Post) && _isInAtomic)
            {
                syncCtx = new WorkerSynchronizationContext(this, _atomicWork);
                SynchronizationContext.SetSynchronizationContext(syncCtx);
            }
            else if (work.Sync != null || work.Options.HasFlag(WorkOptions.Atomic))
            {
                syncCtx = new WorkerSynchronizationContext(this, work);
                SynchronizationContext.SetSynchronizationContext(syncCtx);
            }

            Task task = null;
            try
            {
                task = work.Invoke();
            }
            catch (Exception e)
            {
                if (UnhandledException != null)
                    UnhandledException(this, e);
                else
                    throw;
            }

            if (syncCtx != null)
            {
                SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
                if (work.Sync != null)
                {
                    if (task == null || work.Options.HasFlag(WorkOptions.Atomic) == false)
                        work.Sync.NotifySyncEndToWaiters();
                }
            }

            if (task != null)
            {
                Interlocked.Increment(ref _runningTaskCount);
                task.ContinueWith(OnTaskComplete, work, TaskContinuationOptions.ExecuteSynchronously);
            }
            else
            {
                if (work.CompletionSource != null)
                {
                    throw new InvalidOperationException("Function for InvokeResult should return Task");
                }
            }
        }

        private void OnTaskComplete(Task completed, object param)
        {
            var work = (Work)param;
            var runningTaskCount = Interlocked.Decrement(ref _runningTaskCount);

            switch (completed.Status)
            {
                case TaskStatus.RanToCompletion:
                    if (work.CompletionSource != null)
                        work.CompletionSource.TrySetResult(completed);
                    break;

                case TaskStatus.Canceled:
                    if (work.CompletionSource != null)
                        work.CompletionSource.TrySetCanceled();
                    break;

                case TaskStatus.Faulted:
                    if (work.CompletionSource != null)
                        work.CompletionSource.TrySetException(completed.Exception.GetBaseException());
                    lock (_lock)
                    {
                        if (UnhandledException != null)
                            UnhandledException(this, completed.Exception.GetBaseException());
                        else
                            throw completed.Exception;
                    }
                    break;
            }

            if (work.Options.HasFlag(WorkOptions.Atomic))
            {
                if (work.Sync != null)
                    work.Sync.NotifySyncEndToWaiters();

                bool willSpawn = false;
                lock (_lock)
                {
                    _isInAtomic = false;
                    _atomicWork = null;
                    Swap(ref _workQueue, ref _pendingWorkQueue);

                    if (_isWorkLoopSpawned == false)
                    {
                        _isWorkLoopSpawned = true;
                        willSpawn = true;
                    }
                }
                if (willSpawn)
                {
                    WorkLoop(null);
                    return;
                }
            }

            if (runningTaskCount == 0 && _waitingBarrier != null)
            {
                bool willSpawn = false;
                lock (_lock)
                {
                    if (_waitingBarrier != null)
                    {
                        Debug.Assert(_isInBarrier, "Should be in barrier.");
                        OnBarrierComplete(_waitingBarrier);

                        if (_isWorkLoopSpawned == false)
                        {
                            _isWorkLoopSpawned = true;
                            willSpawn = true;
                        }
                    }
                }
                if (willSpawn)
                {
                    WorkLoop(null);
                    return;
                }
            }
        }

        private void OnBarrierComplete(Work barrierWork)
        {
            Debug.Assert(Monitor.IsEntered(_lock), "This handler should be under lock.");

            _isInBarrier = false;
            _waitingBarrier = null;

            // resume all pending works
            while (_barrierWorkQueue.Count > 0)
            {
                var w = _barrierWorkQueue.Dequeue();
                _workQueue.Enqueue(w);

                // if we got barrier in pending works, trigger barrier mode again
                if (w.Options.HasFlag(WorkOptions.Barrier))
                {
                    _isInBarrier = true;
                    break;
                }
            }

            if (barrierWork.CompletionSource != null)
                barrierWork.CompletionSource.TrySetResult(null);
        }

        internal void OnSyncReady(WorkSyncContext sync)
        {
            if (_waitingSyncedWork.Options.HasFlag(WorkOptions.Atomic))
            {
                if (_isInAtomic)
                    throw new InvalidOperationException("Already in atomic");

                _isInAtomic = true;
                _atomicWork = _waitingSyncedWork;
                Swap(ref _workQueue, ref _pendingWorkQueue);
                if (_workQueue == null)
                    _workQueue = new Queue<Work>();
            }

            // all waitor workers stops for waiting this work done.
            // we can process this work safely.
            ProcessWork(_waitingSyncedWork);

            lock (_lock)
            {
                _waitingSyncedWork = null;

                if (_isWorkLoopSpawned == false)
                {
                    _isWorkLoopSpawned = true;
                    ThreadPool.UnsafeQueueUserWorkItem(WorkLoop, null);
                }
            }
        }

        internal void OnSyncEnd(WorkSyncContext sync)
        {
            // when synched work finshed, resume work loop again
            lock (_lock)
            {
                if (_waitingSync.Source == sync)
                {
                    _waitingSync = null;

                    if (_isWorkLoopSpawned == false)
                    {
                        _isWorkLoopSpawned = true;
                        ThreadPool.UnsafeQueueUserWorkItem(WorkLoop, null);
                    }
                }
                else
                {
                    throw new InvalidOperationException("Waiting state doesn't match with event source");
                }
            }
        }
    }
}
