using System;

namespace AsyncWorker
{
    [Flags]
    public enum InvokeOptions
    {
        Normal = 0, //      Normal work which can be interruped by other works.
        Atomic = 1, //      Atomic work which is not interruped by other works.
    }

    [Flags]
    internal enum WorkOptions
    {
        Normal = 0, //      InvokeOptions.Normal
        Atomic = 1, //      InvokeOptions.Atomic
        Post = 256, //      Post work of ongoing Task work.
        Barrier = 512, //   Work for Barrier mark.
        Sync = 1024, //     Work for Sync mark requested from other Worker.
    }

    public class SyncOptions
    {
        internal Worker[] Workers;

        public SyncOptions(params Worker[] workers)
        {
            Workers = workers;
        }
    }
}
