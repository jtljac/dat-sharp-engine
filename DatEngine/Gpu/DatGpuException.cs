using DatEngine.Util;

namespace DatEngine.Gpu;

/// <summary>
/// Exception for throws related to the GPU
/// </summary>
public class DatGpuException : DatEngineException{
    public DatGpuException(string message) : base(message) {}
    public DatGpuException(string message, Exception inner) : base(message, inner) {}
}