namespace dat_sharp_engine.AssetManagement;

/// <summary>
/// The state of an asset in memory
/// </summary>
public enum AssetLoadState : byte {
    /// <summary>The asset is currently unloaded</summary>
    Unloaded,
    /// <summary>The asset is queued to be loaded into memory</summary>
    Loading,
    /// <summary>The asset is currently loaded in memory and available to use</summary>
    Loaded,
    /// <summary>The asset is queued to be unloaded from memory</summary>
    Unloading
}