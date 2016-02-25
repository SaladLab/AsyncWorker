using System;

namespace AsyncWorker
{
    [Flags]
    public enum InvokeOptions
    {
        Normal = 0,
        Atomic = 1,
    }

    [Flags]
    internal enum WorkOptions
    {
        Post = 256,
        Barrier = 512,
        Sync = 1024,
    }

    public class SyncOptions
    {
        public SyncOptions(params Worker[] workers)
        {
            Workers = workers;
        }

        internal Worker[] Workers;
    }
}
