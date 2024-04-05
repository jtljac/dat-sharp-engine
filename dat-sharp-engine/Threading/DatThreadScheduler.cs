using System.Collections.Concurrent;

namespace dat_sharp_engine.Threading;

public class DatThreadScheduler : TaskScheduler, IDisposable {
    private readonly ConcurrentQueue<Task> _tasks = new();
    private readonly Thread[] _threads;
    private bool shutdown = false;

    protected override IEnumerable<Task>? GetScheduledTasks() {
        throw new NotImplementedException();
    }

    protected override void QueueTask(Task task) {
        throw new NotImplementedException();
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
        throw new NotImplementedException();
    }

    public void Dispose() {
        throw new NotImplementedException();
    }

    private void WorkLoop() {
        while (true) {

        }
    }
}