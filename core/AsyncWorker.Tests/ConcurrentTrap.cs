using System;
using System.Threading;

namespace AsyncWorker.Tests
{
    public class ConcurrentTrap
    {
        private int _counter;
        private bool _trapped;

        public bool Trapped
        {
            get { return _trapped; }
        }

        public void Acquire()
        {
            if (Interlocked.Increment(ref _counter) != 1)
                _trapped = true;
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _counter) != 0)
                _trapped = true;
        }
    }

    public class ConcurrentTrapBlock : IDisposable
    {
        private ConcurrentTrap _trap;

        public ConcurrentTrapBlock(ConcurrentTrap trap)
        {
            _trap = trap;
            _trap.Acquire();
        }

        public void Dispose()
        {
            _trap.Release();
        }
    }
}
