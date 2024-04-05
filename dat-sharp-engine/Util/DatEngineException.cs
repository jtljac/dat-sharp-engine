namespace dat_sharp_engine.Util; 

/// <summary>
/// Represents errors that occur related to the DatSharpEngine
/// </summary>
public class DatEngineException : Exception {
    public DatEngineException() { }
    public DatEngineException(string? message) : base(message) { }
    public DatEngineException(string? message, Exception? innerException) : base(message, innerException) { }
}
