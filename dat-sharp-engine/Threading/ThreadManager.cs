using dat_sharp_engine.Util;

namespace dat_sharp_engine.Threading;

public class ThreadManager {
    private static readonly CVar<uint> ThreadCountCVar = new("uThreadCount",
        "The number of extra threads to create for the engine, 0 to disable",
        0,
        CVarCategory.Core,
        CVarFlags.RequiresRestart
    );

    private static readonly CVar<float> LongRunningThreadRatioCVar = new("fLongThreadRatio",
        "The % of threads that are allowed to execute long running tasks, for example IO based Tasks",
        0.25f,
        CVarCategory.Core,
        CVarFlags.RequiresRestart,
        value => float.Clamp(value, 0, 1)
    );

    public static ThreadManager instance { get; } = new();

    private DatThreadScheduler _scheduler;

    public void Initialise() {
        _scheduler = new DatThreadScheduler(ThreadCountCVar.value, LongRunningThreadRatioCVar.value);
    }

    // Handle registering dedicated threads
    // This will need to remove a thread from the pool, and give it to the requester

    public void ScheduleTask(Task task) {
        task.Start(_scheduler);
    }

    public static uint totalThreadCount => ThreadCountCVar.value == 0 ? (uint) (Environment.ProcessorCount - 1) : ThreadCountCVar.value;
    public static uint longThreadCount => (uint) Math.Ceiling(ThreadCountCVar.value * LongRunningThreadRatioCVar.value);
}
