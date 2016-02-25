using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AsyncWorker.Tests
{
    public class WorkerTest
    {
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
                    using (var block = new ConcurrentTrapBlock(trap))
                        log.Enqueue(local_i);
                });
            }

            await worker.SetBarrierReturn();

            // Assert

            Assert.Equal(false, trap.Trapped);
            Assert.Equal(new[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10}, log);
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
                    using (var block = new ConcurrentTrapBlock(trap))
                        log.Enqueue(local_i);

                    await Task.Yield();

                    using (var block = new ConcurrentTrapBlock(trap))
                        log.Enqueue(-local_i);
                });
            }

            await worker.SetBarrierReturn();

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

            await worker.SetBarrierReturn();

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
                    log.Enqueue(local_i);
                });
            }

            worker.SetBarrier();

            for (var i = 1; i <= 10; i++)
            {
                var local_i = i;
                worker.Invoke(async () =>
                {
                    log.Enqueue(-local_i);
                    await Task.Yield();
                    log.Enqueue(-local_i);
                });
            }

            await worker.SetBarrierReturn();

            // Assert

            var logItems = log.ToList();
            Assert.Equal(40, logItems.Count);
            Assert.True(logItems.Take(20).All(x => x > 0));
            Assert.True(logItems.Skip(20).Take(20).All(x => x < 0));
        }

        [Fact]
        public async Task BarrierReturn_AllWorkBeforeDone()
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

            await worker.SetBarrierReturn();

            // Assert

            Assert.Equal(20, log.Count);
        }

        [Fact]
        public async Task InvokeWork_SyncWithOtherWorker()
        {
            // Arrange

            var trap = new ConcurrentTrap();
            var log = new ConcurrentQueue<int>();
            var worker1 = new Worker();
            var worker2 = new Worker();

            // Act

            worker1.Invoke(async () =>
            {
                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(1);

                await Task.Yield();

                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(-1);
            });

            worker1.Invoke(async () =>
            {
                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(100);

                await Task.Yield();

                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(101);
            }, syncOptions: new SyncOptions(worker2));

            worker2.Invoke(async () =>
            {
                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(2);

                await Task.Yield();

                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(-2);
            });

            await Task.WhenAll(worker1.SetBarrierReturn(),
                worker2.SetBarrierReturn());

            // Assert

            Assert.Equal(false, trap.Trapped);
            Assert.Equal(6, log.Count);
        }

        // IT DOESN'T WORK NOW
        /*
        [Fact]
        public async Task InvokeAtomicWork_SyncWithOtherWorker()
        {
            // Arrange

            var trap = new ConcurrentTrap();
            var log = new ConcurrentQueue<int>();
            var worker1 = new Worker();
            var worker2 = new Worker();

            // Act

            worker1.Invoke(async () =>
            {
                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(1);

                await Task.Yield();

                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(-1);
            });

            worker1.Invoke(async () =>
            {
                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(100);

                await Task.Yield();

                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(101);
            }, InvokeOptions.Atomic, new SyncOptions(worker2));

            worker2.Invoke(async () =>
            {
                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(2);

                await Task.Yield();

                using (var block = new ConcurrentTrapBlock(trap))
                    log.Enqueue(-2);
            });

            await Task.WhenAll(worker1.SetBarrierReturn(),
                worker2.SetBarrierReturn());

            // Assert

            Assert.Equal(false, trap.Trapped);
            Assert.Equal(6, log.Count);
        }
        */

        [Fact]
        public async Task TODO_InvokeReturn()
        {
        }

        [Fact]
        public async Task TODO_CloseWorkingWorker_CancelAllPendingWorks()
        {
        }
    }
}
