namespace dat_sharp_engine.Rendering.Util;

public class DatRendererDrawException : DatRendererException {
    public DatRendererDrawException() { }
    public DatRendererDrawException(string? message) : base(message) { }
    public DatRendererDrawException(string? message, Exception? innerException) : base(message, innerException) { }
}
