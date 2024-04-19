using dat_sharp_engine.Util;
using dat_sharp_vfs;
using dat_sharp_vfs.FileInserter;

namespace dat_sharp_engine.AssetManagement;

/// <summary>
/// A class for managing assets for the game engine
/// <para/>
/// The Asset Manager provides an interface for accessing assets, through a file tree abstraction, as well as
/// functionality for dynamically loading and unloading assets
/// </summary>
public class AssetManager {
    private readonly DatSharpVfs _vfs = new();
    private readonly Dictionary<AssetCacheKey, WeakReference<Asset>> _assetCache = new();

    public static AssetManager instance { get; } = new();

    public void Initialise() {
        _vfs.MountFiles("", new LooseFileDVfsFileInserter(FileUtil.GetAssetDirectory(), true));
    }

    public T GetAsset<T>(string path, bool lazyLoad = false) where T : Asset, new() {
        var assetCacheKey = new AssetCacheKey(path, typeof(T));

        // Check cache for asset
        if (_assetCache.TryGetValue(assetCacheKey, out var cachedAssetRef) &&
            cachedAssetRef.TryGetTarget(out var cachedAsset))
            return (T) cachedAsset;

        // Create new asset
        var file = _vfs.GetFile(path);
        if (file == null) throw new FileNotFoundException($"Failed to find file at: {path}");

        // Hack to call constructor with arguments
        var asset = (T) Activator.CreateInstance(typeof(T), path, lazyLoad ? null: file)!;
        _assetCache[assetCacheKey] = new WeakReference<Asset>(asset);

        return asset;
    }

    /// <summary>
    /// Get a raw file from the VFS
    /// </summary>
    /// <param name="path">The path to the file in the VFS</param>
    /// <returns>The raw file at the given path</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist in the VFS</exception>
    public DVfsFile GetRawAssetFile(string path) {
        var rawAssetFile = _vfs.GetFile(path);

        if (rawAssetFile == null) throw new FileNotFoundException($"Failed to find file at: {path}");

        return rawAssetFile;
    }
}

internal record AssetCacheKey(string path, Type type);