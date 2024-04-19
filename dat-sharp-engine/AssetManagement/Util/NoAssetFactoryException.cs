namespace dat_sharp_engine.AssetManagement.Util;

/// <summary>
/// Represents an error that occurs when there isn't an <see cref="AssetFactory"/> registered for an <see cref="Asset"/>.
/// </summary>
public class NoAssetFactoryException : DatAssetException {
    public NoAssetFactoryException() { }
    public NoAssetFactoryException(string? message) : base(message) { }
    public NoAssetFactoryException(string? message, Exception? innerException) : base(message, innerException) { }
}