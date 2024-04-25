using dat_sharp_engine.AssetManagement.Util;
using dat_sharp_engine.Threading;
using dat_sharp_vfs;
using Logger = dat_sharp_engine.Util.Logger;

namespace dat_sharp_engine.AssetManagement;

/// <summary>
/// An abstract class for objects that can be loaded from the disk
/// <para/>
/// Provides functionality for loading and unloading
/// <para/>
/// Some assets can be "virtual". This means the asset is unique and isn't backed by an asset on the disk and can be
/// freely modified.
/// </summary>
public abstract class Asset {
    /// <summary>
    /// The path to the file in the <see cref="AssetManager"/>.
    /// <para/>
    /// If this is <c>null</c> then the asset is virtual, see <see cref="isVirtual"/>.
    /// </summary>
    public readonly string? path;

    /// <summary>
    /// The file that is this asset
    /// <para/>
    /// If this is <c>null</c> then the asset is virtual, see <see cref="isVirtual"/>.
    /// <para/>
    /// If this week reference is invalid, then the file is assumed to have been reloaded, and will be re-fetched by the
    /// path. This functionality is not supported if the file has more than one reference.
    /// </summary>
    protected WeakReference<DVfsFile>? file;

    /// <summary>
    /// If <c>true</c>, this asset is unique and not backed by a file on the disk and therefore has no loading or
    /// unloading functionality.
    /// </summary>
    public bool isVirtual => path == null;

    /// <summary>
    /// The current state of the asset.
    /// <para/>
    /// Changes to this value should be locked with <see cref="_cpuLoadLock"/>.
    /// </summary>
    private volatile AssetLoadState _cpuLoadState;

    /// <summary>
    /// The current running task for loading or unloading this asset. If this is null, then the asset is not currently
    /// being loaded or unloaded.
    /// <para/>
    /// This is stored so that files requesting a load/unload when the asset is loading/unloading can then access the
    /// same load/unload task.
    /// </summary>
    private volatile Task? _cpuLoadTask;

    /// <summary>
    /// Lock for guarding access to changes to <see cref="_cpuLoadState"/>
    /// </summary>
    private SpinLock _cpuLoadLock;

    /// <summary>
    /// A count of the usages of this asset on the Cpu
    /// </summary>
    private int _cpuUsages;

    /// <summary>The load state of the asset</summary>
    public AssetLoadState CpuLoadState => _cpuLoadState;

    /// <summary>If <c>true</c>, then the file is currently loaded in Cpu memory</summary>
    public bool isCpuLoaded => _cpuLoadState == AssetLoadState.Loaded;
    /// <summary>If <c>true</c>, then the file is currently loading into Cpu memory</summary>
    public bool isCpuLoading => _cpuLoadState == AssetLoadState.Loading;

    /// <summary>
    /// Base initialiser for a virtual asset
    /// </summary>
    protected Asset() {
        path = null;
        file = null;
        _cpuLoadState = AssetLoadState.Loaded;
    }

    /// <summary>
    /// Base initialiser for file backed asset
    /// </summary>
    /// <param name="path">The path to the file in the VFS</param>
    /// <param name="file">The file entry in the VFS, pass <c>null</c> for a lazily loaded asset</param>
    protected Asset(string? path, DVfsFile? file) {
        _cpuLoadState = AssetLoadState.Unloaded;
        this.path = path;

        // File can be lazily acquired
        if (file == null) {
            this.file = null;
            return;
        }

        this.file = new WeakReference<DVfsFile>(file);

        if (file.references > 1) Logger.EngineLogger.Warn(
            "Asset at {} is stored in the VFS multiple times, this asset may not detect when it's file has been replaced in the VFS",
            path
        );
    }

    ~Asset() {
        // Clean up in case this asset does funky stuff
        if (isCpuLoaded) {
            CpuUnloadAssetImpl();
        }
    }

    /// <summary>
    /// Register a usage of this asset on the CPU. If this asset isn't already loaded, then it will kick off an
    /// asynchronous load. This can be waited on with <see cref="WaitForCpuLoad"/>.
    /// <para/>
    /// You <b>must</b> be careful not to call this erroneously, as well as to call <see cref="ReleaseCpuAsset"/>
    /// once you are done using this asset, as this being incorrectly tracked can lead to early unloading, or the asset
    /// becoming stuck loaded and leaked
    /// </summary>
    /// <returns>A task representing the Cpu load job</returns>
    /// <seealso cref="ReleaseCpuAsset"/>
    public Task AcquireCpuAsset() {
        Interlocked.Increment(ref _cpuUsages);
        if (!isCpuLoaded && !isCpuLoading) return RequestCpuLoadAsset();

        return _cpuLoadTask ?? ThreadManager.instance.completedTask;
    }

    /// <summary>
    /// Unregister a usage of this asset on the CPU. When there are no more usages of this asset, then it may be
    /// unloaded by the asset manager.
    /// <para/>
    /// You <b>must</b> be careful not to erroneously call this, as this being incorrectly tracked can lead to early
    /// unloading, or the asset becoming stuck loaded and leaked
    /// </summary>
    /// <seealso cref="AcquireCpuAsset"/>
    public void ReleaseCpuAsset() {
        Interlocked.Decrement(ref _cpuUsages);
    }

    /// <summary>
    /// Blocking wait for this asset to finish loading into Cpu memory.
    /// <para/>
    /// If this asset isn't currently loading or is unloading, then it will return immediately
    /// </summary>
    public void WaitForCpuLoad() {
        // Copy task as this can be cleared in another thread
        var task = _cpuLoadTask;
        if (!isCpuLoading || task == null) return;

        task.Wait();
    }

    /// <summary>
    /// The action performed when loading an asset into Cpu Memory
    /// <para/>
    /// This cannot be interrupted, and will lock <see cref="_cpuLoadLock"/>
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when failing to acquire the raw asset from the <see cref="AssetManager"/></exception>
    private void CpuLoadJob() {
        var rawFile = GetRawFile();
        if (rawFile == null) throw new FileNotFoundException($"Failed to get asset file: {path}");

        CpuLoadAssetImpl(rawFile.GetFileStream());

        var lockTaken = false;
        try {
            _cpuLoadLock.Enter(ref lockTaken);
            _cpuLoadState = AssetLoadState.Loaded;
            _cpuLoadTask = null;
        }
        finally {
            if (lockTaken) _cpuLoadLock.Exit(false);
        }
    }

    /// <summary>
    /// The action performed when unloading an asset from Cpu memory
    /// <para/>
    /// This cannot be interrupted, and will lock <see cref="_cpuLoadLock"/>
    /// </summary>
    private void CpuUnloadJob() {
        CpuUnloadAssetImpl();

        var lockTaken = false;
        try {
            _cpuLoadLock.Enter(ref lockTaken);
            if (_cpuLoadState != AssetLoadState.Unloading) return;

            _cpuLoadState = AssetLoadState.Unloaded;
            _cpuLoadTask = null;
        }
        finally {
            if (lockTaken) _cpuLoadLock.Exit(false);
        }
    }

    /// <summary>
    /// Request this asset to be loaded into Cpu memory.
    /// <para/>
    /// This always happens asyncronously.
    /// </summary>
    /// <returns>A task representing the asynchronous Cpu load</returns>
    protected Task RequestCpuLoadAsset() {
        if (isVirtual) return ThreadManager.instance.completedTask;

        // Lock as we are modifying _cpuLoadState and do not want multiple threads to enter this part of the function at
        // the same time
        var lockTaken = false;
        try {
            _cpuLoadLock.Enter(ref lockTaken);
            switch (_cpuLoadState) {
                case AssetLoadState.Unloaded:
                    _cpuLoadState = AssetLoadState.Loading;
                    _cpuLoadTask = ThreadManager.instance.StartLongTask(CpuLoadJob);
                    return _cpuLoadTask;
                case AssetLoadState.Loading:
                    return _cpuLoadTask!;
                case AssetLoadState.Loaded:
                    return ThreadManager.instance.completedTask;
                case AssetLoadState.Unloading:
                    _cpuLoadState = AssetLoadState.Loading;
                    _cpuLoadTask = _cpuLoadTask!.ContinueWith(_ => {
                        CpuLoadJob();
                    }, TaskContinuationOptions.LongRunning);
                    return _cpuLoadTask;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        finally {
            if (lockTaken) _cpuLoadLock.Exit(false);
        }
    }

    /// <summary>
    /// Unload the asset from Cpu memory
    /// <para/>
    /// This always happens asyncronously.
    /// </summary>
    /// <returns>A task representing the asynchronous Cpu unload</returns>
    protected Task? CpuUnloadAsset() {
        if (isVirtual) return ThreadManager.instance.completedTask;

        var lockTaken = false;
        try {
            _cpuLoadLock.Enter(ref lockTaken);
            switch (_cpuLoadState) {
                case AssetLoadState.Unloaded:
                    return ThreadManager.instance.completedTask;
                case AssetLoadState.Loading:
                    return null;
                case AssetLoadState.Loaded:
                    _cpuLoadState = AssetLoadState.Unloading;
                    _cpuLoadTask = ThreadManager.instance.StartLongTask(CpuUnloadJob);
                    return _cpuLoadTask;
                case AssetLoadState.Unloading:
                    return _cpuLoadTask!;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        finally {
            if (lockTaken) _cpuLoadLock.Exit(false);
        }
    }

    /// <summary>
    /// Get the raw file this asset represents
    /// </summary>
    /// <returns>The DVFS file represented by this asset</returns>
    protected DVfsFile? GetRawFile() {
        if (isVirtual) return null;

        if (file != null && file.TryGetTarget(out var rawFile)) return rawFile;

        rawFile = AssetManager.instance.GetRawAssetFile(path!);
        file = new WeakReference<DVfsFile>(rawFile);

        return rawFile;
    }

    /// <summary>
    /// The implementation to load the asset from a stream into Cpu memory
    /// </summary>
    /// <param name="assetData">A stream containing the contents of the asset file</param>
    /// <exception cref="DatAssetException">Thrown when an error occurs whilst loading the asset</exception>
    protected abstract void CpuLoadAssetImpl(Stream assetData);

    /// <summary>
    ///  The implementation to unload the asset from Cpu memory
    /// </summary>
    protected abstract void CpuUnloadAssetImpl();
}