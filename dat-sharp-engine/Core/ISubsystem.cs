namespace dat_sharp_engine.Core;

/// <summary>
/// Interface for subsystems that have initialisation phases
/// </summary>
public interface ISubsystem {
    /// <summary>
    /// Initialisation phase for early module setup
    /// </summary>
    public void PreInit() { }

    /// <summary>
    /// Initialisation phase
    /// </summary>
    public void Init();
    public void PostInit() { }

    public void Shutdown() { }
}