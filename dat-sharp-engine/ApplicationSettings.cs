using Version = Silk.NET.SDL.Version;

namespace dat_sharp_engine; 

/// <summary>
/// A set of required values that defines the game for the engine
/// </summary>
public record ApplicationSettings {
    /// <summary>The name of the game</summary>
    public required string name { get; init; }
    /// <summary>The version of the game</summary>
    public required Version version { get; init; }
}