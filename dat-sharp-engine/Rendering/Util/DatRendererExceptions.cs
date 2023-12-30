using dat_sharp_engine.Util;

namespace dat_sharp_engine.Rendering.Util;

public class DatRendererExceptions : DatEngineException {
    public DatRendererExceptions() { }
    public DatRendererExceptions(string? message) : base(message) { }
    public DatRendererExceptions(string? message, Exception? innerException) : base(message, innerException) { }
}
