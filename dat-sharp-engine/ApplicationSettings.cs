using Version = Silk.NET.SDL.Version;

namespace dat_sharp_engine; 

/**
 * A set of
 */
public record ApplicationSettings {
    public required string name { get; init; }
    public required Version version { get; init; }
}