using NLog;

namespace dat_asset_processor;

public class Logger {
    public static NLog.Logger ProcessorLogger = LogManager.GetLogger("Processor");
}