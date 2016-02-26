# AsyncWorker

[![Build status](https://ci.appveyor.com/api/projects/status/klxyrlxal7yy0f26?svg=true)](https://ci.appveyor.com/project/veblush/asyncworker)

With .NET asynchronous worker, various "async" works can be scheduled and executed
in an easy way. AsyncWorker can be thought as a dedicated work queue which executes
work items in a serialized way.

For example, async functions can be queued to be executed as following:
```csharp
var w = new AsyncWorker.Worker();
w.Invoke(      () => { Print("A"); });
w.Invoke(async () => { Print("B1"); await Yield(); Print("B2"); });
w.Invoke(async () => { Print("C1"); await Yield(); Print("C2"); });
```

Output:
```
A B1 C1 B2 C2
```

## Where can I get it?

```
PM> Install-Package AsyncWorker
```

## Features

AsyncWorker is a queue scheduler for Task. It will run queued works in
the ordered and serialized way. And also it provides a few options controlling
how works run.

#### Queue actions

Work item which will be queued can be a plain action:

```csharp
var w = new AsyncWorker.Worker();
w.Invoke(() => { Print("A"); });
w.Invoke(() => { Print("B"); });
```

Output:
```
A B
```

It runs in order.

#### Queue task functions

In addition to action, task can be queued. And task may be executed in the interleaved way.

```csharp
var w = new AsyncWorker.Worker();
w.Invoke(async () => { Print("A1"); await Yield(); Print("A2"); });
w.Invoke(async () => { Print("B1"); await Yield(); Print("B2"); });
```

Output:
```
A1 B1 A2 B2
```

To maximze throuput of processing works, worker continues executing task B in waiting for
finishing Yield of task A. But async worker executes A1, A2, B1 and B2 at a time.

#### Queue atomic task functions

Task can be executed in an atomic way, which keep task from being interupted by
other works:

```csharp
var w = new AsyncWorker.Worker();
w.Invoke(async () => { Print("A1"); await Yield(); Print("A2"); });
w.Invoke(async () => { Print("B1"); await Yield(); Print("B2"); }, InvokeOptions.Atomic);
```

Output:
```
A1 B1 B2 A2
```

Task B is done alone without any interruption. But A is splited by B because it was not
queued as atomic work.

#### Barrier

Barrier can be set for separating works before it and works after it.
Any works after barrier won't start working before all works before barrier are finished.

```csharp
var w = new AsyncWorker.Worker();
w.Invoke(async () => { Print("A1"); await Yield(); Print("A2"); });
w.SetBarrier();
w.Invoke(async () => { Print("B1"); await Yield(); Print("B2"); });
```

Output:
```
A1 A2 B1 B2
```

You can use barrier for waiting until worker finish all enqueued works.

```csharp
var w = new AsyncWorker.Worker();
w.Invoke(...);
w.Invoke(...);
await w.SetBarrierAsync();
```

#### Queue func synced with other workers

AsyncWorker works alone and different workers may execute concurrently.
But workers can be synced with each others. For example worker1 enqueues task C and
wants worker2 synced with worker1 in executing task C.

```csharp
var w1 = new AsyncWorker.Worker();
var w2 = new AsyncWorker.Worker();
w1.Invoke(async () => { Print("A1"); await Yield(); Print("A2"); });
w2.Invoke(async () => { Print("B1"); await Yield(); Print("B2"); });
w1.Invoke(async () => { Print("C1"); await Yield(); Print("C2"); }, syncOptions: new SyncOptions(w2)));
```

Output:
```
w1: A1 C1 A2 C2
w2: B1    B2
```

Task A and B are executed concurrently because each has a different worker.
But task C is processed by worker1 while worker2 waits until task C finished.
