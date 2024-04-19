using dat_sharp_engine.Util;

namespace dat_sharp_engine.AssetManagement.Util;

/// <summary>
/// Represents errors that occur related to the Asset Subsystem
/// </summary>
public class DatAssetException : DatEngineException {
    public DatAssetException() { }
    public DatAssetException(string? message) : base(message) { }
    public DatAssetException(string? message, Exception? innerException) : base(message, innerException) { }
}