using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AsyncWorker;

namespace Demo
{
    public class DemoWidget
    {
        private Worker _worker;
        private List<string> _commands;
        private List<string> _jobQueue;

        public DemoWidget()
        {
            _worker = new Worker();
            _commands = new List<string>();
            _jobQueue = new List<string>();
        }

        public void InvokeAction(string name, InvokeOptions options = InvokeOptions.Normal)
        {
            _commands.Add(string.Format("Invoke({0}{1})", name, options == InvokeOptions.Normal ? "" : ", Atomic"));
            _worker.Invoke(() => { OnInvoke(name); }, options);
        }

        public void InvokeTask(string name, InvokeOptions options = InvokeOptions.Normal)
        {
            _commands.Add(string.Format("Invoke(async {0}{1})", name, options == InvokeOptions.Normal ? "" : ", Atomic"));
            _worker.Invoke(async () =>
            {
                OnInvoke(name, "1");
                await Task.Yield();
                OnInvoke(name, "2");
            }, options);
        }

        public void SetBarrier()
        {
            _commands.Add("SetBarrier()");
            _worker.SetBarrierAsync()
                   .ContinueWith(_ => { OnInvoke("Barrier"); }, TaskContinuationOptions.ExecuteSynchronously);
        }

        public async Task WaitAndDump(string name)
        {
            await _worker.SetBarrierAsync();
            Console.WriteLine("========== " + name + " ==========");
            Console.WriteLine("* Command");
            foreach (var cmd in _commands)
                Console.WriteLine("  " + cmd);
            Console.WriteLine();
            Console.WriteLine("* JobQueue");
            foreach (var que in _jobQueue)
                Console.WriteLine("  " + que);
            Console.WriteLine();
        }

        private void OnInvoke(string name, string stage = null)
        {
            var step = string.Format("<{0}{1}>", name, string.IsNullOrEmpty(stage) ? "" : ":" + stage);
            _jobQueue.Add(step);
        }
    }
}
