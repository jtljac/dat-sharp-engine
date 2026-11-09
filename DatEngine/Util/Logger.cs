using NLog;

namespace DatEngine.Util;

public class Logger {
    public static readonly NLog.Logger EngineLogger = LogManager.GetLogger("Engine");
    public static readonly NLog.Logger GameLogger = LogManager.GetLogger("Game");
}