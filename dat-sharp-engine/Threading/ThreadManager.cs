using System.Security.Cryptography;
using dat_sharp_engine.Util;

namespace dat_sharp_engine.Threading;

public class ThreadManager {
    public static ThreadManager instance { get; } = new();

    private DatThreadScheduler? _scheduler;

    private TaskFactory _factory = null!;

    public Task completedTask = null!;

    public int dedicatedThreads { get; private set; }

    public void Initialise() {
        _scheduler = new DatThreadScheduler(threadCount, EngineCVars.LongRunningThreadRatioCVar.value);
        _factory = new TaskFactory(_scheduler);
        completedTask = _factory.StartNew(() => { });
        completedTask.Wait();
    }

    /// <summary>
    /// Register a dedicated thread being used
    /// <para/>
    /// This will consume a thread from
    /// </summary>
    public void RegisterDedicatedThread() {
        if (_scheduler != null) 
            throw new DatEngineException("Thread registered after initialisation. Threads can only be registered " +
                                         "during pre-initialisation");
        ++dedicatedThreads;
    }

    // Handle registering dedicated threads
    // This will need to remove a thread from the pool, and give it to the requester

    public void ScheduleTask(Task task) {
        task.Start(_scheduler!);
    }

    public Task StartTask(Action action) {
        return _factory.StartNew(action);
    }
    public Task StartLongTask(Action action) {
        return _factory.StartNew(action, TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// The number of Cpus available on the system
    /// </summary>
    public static int totalCpuCount => Environment.ProcessorCount;

    /// <summary>
    /// The total threads available to the thread manager
    /// <para/>
    /// Unless the CVar <c>uThreadCount</c> is set to a value other than 0, this will be the number of processes
    /// (including hyper-threading) available on the cpu, minus 1 (for the main thread) and minus the number of
    /// registered dedicated threads (<see cref="dedicatedThreads"/>), minimum of 1 thread.
    /// </summary>
    public uint threadCount => EngineCVars.ThreadCountCVar.value == 0 ? (uint) int.Clamp(totalCpuCount - 1 - dedicatedThreads, 1, int.MaxValue) : EngineCVars.ThreadCountCVar.value;

    /// <summary>
    /// The number of threads that are able to perform long tasks, like asset loading
    /// </summary>
    public uint longThreadCount => (uint) Math.Ceiling(threadCount * EngineCVars.LongRunningThreadRatioCVar.value);

}
