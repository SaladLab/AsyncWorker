using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AsyncWorker.Tests
{
    public class WorkerTest
    {
        [Fact]
        public void InvokeAction_Run()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();
            var done = new AutoResetEvent(false);

            // Act

            worker.Invoke(() =>
            {
                log.Enqueue(1);
                done.Set();
            });
            done.WaitOne();

            // Assert

            Assert.Equal(new[] { 1 }, log);
        }

        [Fact]
        public void InvokeActionWithState_Run()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();
            var done = new AutoResetEvent(false);

            // Act

            worker.Invoke(state =>
            {
                log.Enqueue(1);
                ((AutoResetEvent)state).Set();
            }, done);
            done.WaitOne();

            // Assert

            Assert.Equal(new[] { 1 }, log);
        }

        [Fact]
        public void InvokeTask_Run()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();
            var done = new AutoResetEvent(false);

            // Act

            worker.Invoke(async () =>
            {
                log.Enqueue(1);
                await Task.Yield();
                log.Enqueue(2);
                done.Set();
            });
            done.WaitOne();

            // Assert

            Assert.Equal(new[] { 1, 2 }, log);
        }

        [Fact]
        public void InvokeTaskWithState_Run()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();
            var done = new AutoResetEvent(false);

            // Act

            worker.Invoke(async state =>
            {
                log.Enqueue(1);
                await Task.Yield();
                log.Enqueue(2);
                ((AutoResetEvent)state).Set();
            }, done);
            done.WaitOne();

            // Assert

            Assert.Equal(new[] { 1, 2 }, log);
        }

        [Fact]
        public async Task InvokeTask_WaitForReturnTask()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();

            // Act

            await worker.InvokeAsync(async () =>
            {
                log.Enqueue(1);
                await Task.Yield();
                log.Enqueue(2);
            });

            // Assert

            Assert.Equal(new[] { 1, 2 }, log);
        }

        [Fact]
        public async Task InvokeMultipleWork_RunSynchronously()
        {
            // Arrange

            var trap = new ConcurrentTrap();
            var log = new ConcurrentQueue<int>();
            var worker = new Worker();

            // Act

            for (var i = 1; i <= 10; i++)
            {
                var local_i = i;
                worker.Invoke(() =>
                {
                    using (new ConcurrentTrapBlock(trap))
                        log.Enqueue(local_i);
                });
            }

            await worker.SetBarrierAsync();

            // Assert

            Assert.Equal(false, trap.Trapped);
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, log);
        }

        [Fact]
        public async Task InvokeMultipleAsyncWork_RunSynchronously()
        {
            // Arrange

            var trap = new ConcurrentTrap();
            var log = new ConcurrentQueue<int>();
            var worker = new Worker();

            // Act

            for (var i = 1; i <= 10; i++)
            {
                var local_i = i;
                worker.Invoke(async () =>
                {
                    using (new ConcurrentTrapBlock(trap))
                        log.Enqueue(local_i);

                    await Task.Yield();

                    using (new ConcurrentTrapBlock(trap))
                        log.Enqueue(-local_i);
                });
            }

            await worker.SetBarrierAsync();

            // Assert

            Assert.Equal(false, trap.Trapped);
            Assert.Equal(20, log.Count);
            Assert.Equal(20, log.Distinct().ToList().Count);
        }

        [Fact]
        public async Task InvokeAtomicWork_RunAtomic()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();

            // Act

            for (var i = 1; i <= 10; i++)
            {
                var local_i = i;
                worker.Invoke(async () =>
                {
                    log.Enqueue(local_i);
                    await Task.Yield();
                    log.Enqueue(-local_i);
                });
            }

            worker.Invoke(async () =>
            {
                log.Enqueue(100);
                await Task.Yield();
                log.Enqueue(101);
            }, InvokeOptions.Atomic);

            await worker.SetBarrierAsync();

            // Assert

            var logItems = log.ToList();
            var atomicWorkIndex = logItems.IndexOf(100);
            Assert.Equal(101, logItems[atomicWorkIndex + 1]);
        }

        [Fact]
        public async Task Barrier_SeparateBeforeAndAfterWorks()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();

            // Act

            for (var i = 1; i <= 10; i++)
            {
                var local_i = i;
                worker.Invoke(async () =>
                {
                    log.Enqueue(local_i);
                    await Task.Yield();
                    log.Enqueue(-local_i);
                });
            }

            worker.SetBarrier();

            for (var i = 11; i <= 20; i++)
            {
                var local_i = i;
                worker.Invoke(async () =>
                {
                    log.Enqueue(local_i);
                    await Task.Yield();
                    log.Enqueue(-local_i);
                });
            }

            await worker.SetBarrierAsync();

            // Assert

            var logItems = log.ToList();
            Assert.Equal(40, logItems.Count);
            Assert.True(logItems.Take(20).All(x => Math.Abs(x) >= 1 && Math.Abs(x) <= 10));
            Assert.True(logItems.Skip(20).Take(20).All(x => Math.Abs(x) >= 11 && Math.Abs(x) <= 20));
        }

        [Fact]
        public async Task MultipleBarrier_SeparateBeforeAndAfterWorks()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();

            // Act

            for (var i = 1; i <= 10; i++)
            {
                var local_i = i;
                worker.Invoke(async () =>
                {
                    log.Enqueue(local_i);
                    await Task.Yield();
                    log.Enqueue(-local_i);
                });
            }

            worker.SetBarrier();

            for (var i = 11; i <= 20; i++)
            {
                var local_i = i;
                worker.Invoke(async () =>
                {
                    log.Enqueue(local_i);
                    await Task.Yield();
                    log.Enqueue(-local_i);
                });
            }

            worker.SetBarrier();

            for (var i = 21; i <= 30; i++)
            {
                var local_i = i;
                worker.Invoke(async () =>
                {
                    log.Enqueue(local_i);
                    await Task.Yield();
                    log.Enqueue(-local_i);
                });
            }

            await worker.SetBarrierAsync();

            // Assert

            var logItems = log.ToList();
            Assert.Equal(60, logItems.Count);
            Assert.True(logItems.Take(20).All(x => Math.Abs(x) >= 1 && Math.Abs(x) <= 10));
            Assert.True(logItems.Skip(20).Take(20).All(x => Math.Abs(x) >= 11 && Math.Abs(x) <= 20));
            Assert.True(logItems.Skip(40).Take(20).All(x => Math.Abs(x) >= 21 && Math.Abs(x) <= 30));
        }

        [Fact]
        public async Task BarrierReturn_MakeAllBeforeWorkDone()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();

            // Act

            for (var i = 1; i <= 10; i++)
            {
                var local_i = i;
                worker.Invoke(async () =>
                {
                    log.Enqueue(local_i);
                    await Task.Yield();
                    log.Enqueue(local_i);
                });
            }

            await worker.SetBarrierAsync();

            // Assert

            Assert.Equal(20, log.Count);
        }

        [Fact]
        public async Task InvokeWork_SyncWithOtherWorker()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var trap1 = new ConcurrentTrap();
            var trap2 = new ConcurrentTrap();
            var worker1 = new Worker("Worker1");
            var worker2 = new Worker("Worker2");

            // Act

            worker1.Invoke(async () =>
            {
                using (new ConcurrentTrapBlock(trap1))
                    log.Enqueue(1);

                await Task.Yield();

                using (new ConcurrentTrapBlock(trap1))
                    log.Enqueue(-1);
            });

            worker2.Invoke(async () =>
            {
                using (new ConcurrentTrapBlock(trap2))
                    log.Enqueue(2);

                await Task.Yield();

                using (new ConcurrentTrapBlock(trap2))
                    log.Enqueue(-2);
            });

            worker1.Invoke(async () =>
            {
                using (new ConcurrentTrapBlock(trap1))
                using (new ConcurrentTrapBlock(trap2))
                    log.Enqueue(100);

                await Task.Yield();

                using (new ConcurrentTrapBlock(trap1))
                using (new ConcurrentTrapBlock(trap2))
                    log.Enqueue(101);
            }, syncOptions: new SyncOptions(worker2));

            await Task.WhenAll(worker1.SetBarrierAsync(),
                               worker2.SetBarrierAsync());

            // Assert

            Assert.Equal(false, trap1.Trapped);
            Assert.Equal(false, trap2.Trapped);
            Assert.Equal(6, log.Count);
        }

        [Fact]
        public async Task InvokeAtomicWork_SyncWithOtherWorker()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var trap1 = new ConcurrentTrap();
            var trap2 = new ConcurrentTrap();
            var worker1 = new Worker("Worker1");
            var worker2 = new Worker("Worker2");

            // Act

            worker1.Invoke(async () =>
            {
                using (new ConcurrentTrapBlock(trap1))
                    log.Enqueue(1);

                await Task.Yield();

                using (new ConcurrentTrapBlock(trap1))
                    log.Enqueue(-1);
            });

            worker1.Invoke(async () =>
            {
                using (new ConcurrentTrapBlock(trap2))
                    log.Enqueue(100);

                await Task.Yield();

                using (new ConcurrentTrapBlock(trap2))
                    log.Enqueue(101);
            }, InvokeOptions.Atomic, new SyncOptions(worker2));

            worker2.Invoke(async () =>
            {
                using (new ConcurrentTrapBlock(trap1))
                using (new ConcurrentTrapBlock(trap2))
                    log.Enqueue(2);

                await Task.Yield();

                using (new ConcurrentTrapBlock(trap1))
                using (new ConcurrentTrapBlock(trap2))
                    log.Enqueue(-2);
            });

            await Task.WhenAll(worker1.SetBarrierAsync(),
                               worker2.SetBarrierAsync());

            // Assert

            Assert.Equal(false, trap1.Trapped);
            Assert.Equal(false, trap2.Trapped);
            Assert.Equal(6, log.Count);
            var idx = log.ToList().IndexOf(100);
            Assert.Equal(101, log.ToList()[idx + 1]);
        }

        [Fact]
        public async Task CloseWorkingWorker_CancelAllPendingWorks()
        {
            // Arrange

            var log = new ConcurrentQueue<int>();
            var worker = new Worker();

            // Act

            await Assert.ThrowsAnyAsync<TaskCanceledException>(async () =>
            {
                await worker.InvokeAsync(async (state, cts) =>
                {
                    log.Enqueue(1);
                    worker.Close();
                    await Task.Delay(10000, cts);
                    log.Enqueue(2);
                }, worker);
            });

            // Assert

            Assert.Equal(new[] { 1 }, log);
        }

        [Fact]
        public async Task InvokeAction_UnhandledExceptionHandling()
        {
            // Arrange

            Exception exception = null;
            var log = new ConcurrentQueue<int>();
            var worker = new Worker();
            worker.UnhandledException += (w, e) => { exception = e; };

            // Act

#pragma warning disable 0162
            worker.Invoke(() =>
            {
                log.Enqueue(1);
                throw new Exception("Test");
                log.Enqueue(-1);
            });
#pragma warning restore 0162

            worker.Invoke(() => { log.Enqueue(2); });

            await worker.SetBarrierAsync();

            // Assert

            Assert.NotNull(exception);
            Assert.Equal("Test", exception.Message);
            Assert.Equal(new[] { 1, 2 }, log);
        }

        [Fact]
        public async Task InvokeTask_UnhandledExceptionHandling()
        {
            // Arrange

            var exceptions = new List<Exception>();
            var log = new ConcurrentQueue<int>();
            var worker = new Worker();
            worker.UnhandledException += (w, e) => { exceptions.Add(e); };

            // Act

#pragma warning disable 0162
            worker.Invoke(async () =>
            {
                log.Enqueue(1);
                throw new Exception("Test1");
                await Task.Yield();
                log.Enqueue(-1);
            });

            worker.Invoke(async () =>
            {
                log.Enqueue(2);
                await Task.Yield();
                throw new Exception("Test2");
                log.Enqueue(-2);
            });
#pragma warning restore 0162

            await worker.SetBarrierAsync();

            // Assert

            Assert.Equal(2, exceptions.Count);
            Assert.Equal(new[] { "Test1", "Test2" }, exceptions.Select(e => e.Message));
            Assert.Equal(new[] { 1, 2 }, log);
        }
    }
}
