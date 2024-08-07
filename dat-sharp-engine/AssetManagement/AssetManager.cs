using dat_sharp_engine.Util;
using dat_sharp_vfs;
using dat_sharp_vfs.FileInserter;

namespace dat_sharp_engine.AssetManagement;

/// <summary>
/// A singleton for managing assets for the game engine
/// <para/>
/// The Asset Manager provides an interface for accessing assets, through a file tree abstraction, as well as
/// functionality for dynamically loading and unloading assets
/// </summary>
public class AssetManager {
    /// <summary>The VFS containing the assets</summary>
    private readonly DatSharpVfs _vfs = new();

    /// <summary>
    /// A cache mapping assets and their type to assets
    /// This is used to ensure that assets are only created once as not to waste space
    /// </summary>
    private readonly Dictionary<AssetCacheKey, WeakReference<Asset>> _assetCache = new();

    /// <summary>The instance of the asset manager</summary>
    public static AssetManager instance { get; } = new();

    /// <summary>
    /// Initialise the asset manager
    /// </summary>
    public void Initialise() {
        _vfs.MountFiles("", new LooseFileDVfsFileInserter(FileUtil.GetAssetDirectory(), true));
    }

    /// <summary>
    /// Check if a file exists in the VFS
    /// </summary>
    /// <param name="path">The path to the file in the VFS</param>
    /// <returns>True if the file exists</returns>
    public bool GetFileExists(string path) {
        return _vfs.FileExists(path);
    }

    /// <summary>
    /// Get an asset from the asset manager
    /// </summary>
    /// <param name="path">The path to the asset in the VFS</param>
    /// <param name="lazyLoad">
    ///     If true, the asset should wait till it is first used before fetching it's file reference
    ///     <para/>
    ///     This would be used if the file at the <paramref name="path"/> would not have been mounted yet at the creation
    ///     time of this asset
    /// </param>
    /// <typeparam name="T">The type of the asset</typeparam>
    /// <returns>The asset at the path</returns>
    /// <exception cref="FileNotFoundException">
    ///     Thrown if lazy load is false and <paramref name="path"/> doesn't point to a file.
    /// </exception>
    public T GetAsset<T>(string path, AssetLoadMode loadMode = AssetLoadMode.None) where T : Asset {
        var assetCacheKey = new AssetCacheKey(path, typeof(T));

        // Check cache for asset
        if (_assetCache.TryGetValue(assetCacheKey, out var cachedAssetRef) &&
            cachedAssetRef.TryGetTarget(out var cachedAsset))
            return (T) cachedAsset;

        // Create new asset
        DVfsFile? file = null;
        if (loadMode != AssetLoadMode.Lazy) {
             file = _vfs.GetFile(path);
            if (file == null)
                throw new FileNotFoundException($"Failed to find file at: {path}");
        }

        // Hack to call constructor with arguments
        var asset = (T) Activator.CreateInstance(typeof(T), path, file)!;
        _assetCache[assetCacheKey] = new WeakReference<Asset>(asset);

        if (loadMode == AssetLoadMode.Eager) asset.AcquireCpuAsset();

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

/// <summary>
/// A record containing a path and an asset type.
/// <para/>
/// This is used to create a key used to cache assets.
/// </summary>
/// <param name="path">A path to an Asset</param>
/// <param name="type">An asset type</param>
internal record AssetCacheKey(string path, Type type);

public enum AssetLoadMode : byte {
    /// <summary>Simply fetch the asset without loading</summary>
    None,
    /// <summary>
    /// Fetch the asset without loading, but don't verify if the file exists in the VFS
    /// <para/>
    /// This would be used if the file at the path will not have been mounted yet at the creation time of the asset
    /// </summary>
    Lazy,
    /// <summary>
    /// Fetch the asset and kick of loading the contents of the asset
    /// <para/>
    /// This will perform <see cref="Asset.AcquireCpuAsset"/> for you.
    /// <para/>
    /// The asset is <b>not</b> guaranteed to be loaded before this function returns
    /// </summary>
    Eager
}