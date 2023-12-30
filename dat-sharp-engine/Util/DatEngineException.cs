namespace dat_sharp_engine.Util; 

public class DatEngineException : Exception {
    public DatEngineException() { }
    public DatEngineException(string? message) : base(message) { }
    public DatEngineException(string? message, Exception? innerException) : base(message, innerException) { }
}
