using NLog;

namespace dat_sharp_engine.Util; 

public static class Logger {
    public static readonly NLog.Logger EngineLogger = LogManager.GetLogger("Engine");
    public static readonly NLog.Logger GameLogger = LogManager.GetLogger("Game");
}