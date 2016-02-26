using System;
using System.Threading;

namespace Basic
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var waitor = new AutoResetEvent(false);

            var worker = new AsyncWorker.Worker();
            worker.Invoke(() => { Console.WriteLine("Work1"); });
            worker.Invoke(() => { Console.WriteLine("Work2"); });
            worker.Invoke(() => { waitor.Set(); });

            waitor.WaitOne();
        }
    }
}
