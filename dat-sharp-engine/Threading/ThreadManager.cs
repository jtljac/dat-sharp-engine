using dat_sharp_engine.Util;

namespace dat_sharp_engine.Threading;

public class ThreadManager {
    public static ThreadManager instance { get; } = new();

    private DatThreadScheduler _scheduler;

    public void Initialise() {
        _scheduler = new DatThreadScheduler(EngineCVars.ThreadCountCVar.value, EngineCVars.LongRunningThreadRatioCVar.value);
    }

    // Handle registering dedicated threads
    // This will need to remove a thread from the pool, and give it to the requester

    public void ScheduleTask(Task task) {
        task.Start(_scheduler);
    }

    public static uint totalThreadCount => EngineCVars.ThreadCountCVar.value == 0 ? (uint) (Environment.ProcessorCount - 1) : EngineCVars.ThreadCountCVar.value;
    public static uint longThreadCount => (uint) Math.Ceiling(EngineCVars.ThreadCountCVar.value * EngineCVars.LongRunningThreadRatioCVar.value);
}
