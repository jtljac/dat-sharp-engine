using dat_sharp_engine.Util;

namespace dat_sharp_engine.AssetManagement.Util;

/// <summary>
/// Represents errors that occur related to the Asset Subsystem during loading
/// </summary>
public class DatAssetLoadingException : DatEngineException {
    public DatAssetLoadingException() { }
    public DatAssetLoadingException(string? message) : base(message) { }
    public DatAssetLoadingException(string? message, Exception? innerException) : base(message, innerException) { }
}