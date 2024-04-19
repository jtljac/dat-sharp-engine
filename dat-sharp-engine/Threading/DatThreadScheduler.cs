using System.Collections.Concurrent;

namespace dat_sharp_engine.Threading;

/**
 * A Threaded task scheduler with it's own pool of threads
 */
public class DatThreadScheduler : TaskScheduler {
    private readonly ConcurrentQueue<Task> _tasks = new();
    private readonly ConcurrentQueue<Task> _longTasks = new();
    private readonly Thread[] _threads;
    private bool _shouldShutdown;

    /// <summary>
    ///
    /// </summary>
    /// <param name="threadCount">The number of threads to create for this scheduler</param>
    /// <param name="longRunningRatio">The ratio of threads that are allowed to run long running tasks</param>
    public DatThreadScheduler(uint threadCount, float longRunningRatio) {
        _threads = new Thread[threadCount];
        for (var i = 0; i < _threads.Length; i++) {
            var allowLong = i < threadCount * longRunningRatio;
            _threads[i] = new Thread(() => WorkLoop(allowLong)) {IsBackground = true};
        }
    }

    protected override IEnumerable<Task> GetScheduledTasks() {
        List<Task> totalTasks = new(_tasks.Count + _longTasks.Count);

        totalTasks.AddRange(_tasks);
        totalTasks.AddRange(_longTasks);

        return totalTasks;
    }

    protected override void QueueTask(Task task) {
        if ((task.CreationOptions & TaskCreationOptions.LongRunning) != 0) {
            _longTasks.Enqueue(task);
        } else {
            _tasks.Enqueue(task);
        }
    }



    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => TryExecuteTask(task);

    /// <summary>
    /// Shutdown the thread scheduler
    /// </summary>
    /// <param name="wait">Wait for the threads to finish shutting down before returning</param>
    public void Shutdown(bool wait = true) {
        _shouldShutdown = true;
        if (!wait) return;

        foreach (var thread in _threads) {
            thread.Join();
        }
    }

    private void WorkLoop(bool allowLong) {
        while (!_shouldShutdown) {
            Task? task = null;
            if (allowLong) _tasks.TryDequeue(out task);
            if (task == null || !_tasks.TryDequeue(out task)) continue;

            TryExecuteTask(task);
        }
    }
}