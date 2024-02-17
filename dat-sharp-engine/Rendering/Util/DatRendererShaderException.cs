namespace dat_sharp_engine.Rendering.Util;

public class DatRendererShaderException : DatRendererException {
    public DatRendererShaderException() { }
    public DatRendererShaderException(string? message) : base(message) { }
    public DatRendererShaderException(string? message, Exception? innerException) : base(message, innerException) { }
}
