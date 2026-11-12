namespace DatEngine.Util;

public class DatEngineException : Exception {
    public DatEngineException(string message) : base(message) {}
    public DatEngineException(string message, Exception inner) : base(message, inner) {}
}