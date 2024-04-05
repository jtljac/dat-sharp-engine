using dat_sharp_engine.Util;

namespace dat_sharp_engine.Rendering.Util;

/// <summary>
/// Represents errors that occur related to the Rendering Subsystem.
/// </summary>
public class DatRendererException : DatEngineException {
    public DatRendererException() { }
    public DatRendererException(string? message) : base(message) { }
    public DatRendererException(string? message, Exception? innerException) : base(message, innerException) { }
}
