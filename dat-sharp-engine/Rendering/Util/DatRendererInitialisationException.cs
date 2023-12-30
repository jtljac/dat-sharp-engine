namespace dat_sharp_engine.Rendering.Util;

public class DatRendererInitialisationException : DatRendererExceptions{
    public DatRendererInitialisationException() { }
    public DatRendererInitialisationException(string? message) : base(message) { }
    public DatRendererInitialisationException(string? message, Exception? innerException) : base(message, innerException) { }
}
