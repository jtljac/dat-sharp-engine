using dat_sharp_engine.AssetManagement;
using dat_sharp_engine.Threading;
using dat_sharp_vfs;

namespace dat_sharp_engine.Rendering;

/// <summary>
/// An abstract class representing assets that can be loaded onto the Gpu
/// </summary>
public abstract class GpuAsset : Asset {
    private volatile AssetLoadState _gpuLoadState = AssetLoadState.Unloaded;

    /// <summary>
    /// The current running task for loading or unloading this asset in Gpu Memory. If this is null, then the asset is
    /// not currently being loaded or unloaded.
    /// <para/>
    /// This is stored so that files requesting a load/unload when the asset is loading/unloading can then access the
    /// same load/unload task.
    /// </summary>
    private volatile Task? _gpuLoadTask;

    /// <summary>
    /// Lock for guarding access to changes to <see cref="_gpuLoadState"/>
    /// </summary>
    private SpinLock _gpuLoadLock;

    /// <summary>
    /// A count of the usages of this asset on the Gpu
    /// </summary>
    private int _gpuUsages;

    /// <summary>If <c>true</c>, then the file is currently loaded in Gpu memory</summary>
    public bool isGpuLoaded => _gpuLoadState == AssetLoadState.Loaded;
    /// <summary>If <c>true</c>, then the file is currently loading into Gpu memory</summary>
    public bool isGpuLoading => _gpuLoadState == AssetLoadState.Loading;
    public AssetLoadState GpuLoadState => _gpuLoadState;


    protected GpuAsset() {}
    protected GpuAsset(string? path, DVfsFile? file) : base(path, file) { }

    ~GpuAsset() {
        if (isGpuLoaded) GpuUnloadAssetImpl();
    }

    /// <summary>
    /// Register a usage of this asset on the Gpu. If this asset isn't already loaded, then it will kick off an
    /// asynchronous load. This can be waited on with <see cref="WaitForGpuLoad"/>.
    /// <para/>
    /// You <b>must</b> be careful not to call this erroneously, as well as to call <see cref="ReleaseGpuAsset"/>
    /// once you are done using this asset, as this being incorrectly tracked can lead to early unloading, or the asset
    /// becoming stuck loaded and leaked
    /// </summary>
    /// <seealso cref="ReleaseGpuAsset"/>
    public void AcquireGpuAsset() {
        Interlocked.Increment(ref _gpuUsages);
        RequestGpuLoadAsset();
    }

    /// <summary>
    /// Unregister a usage of this asset on the Gpu. When there are no more usages of this asset, then it may be
    /// unloaded by the asset manager.
    /// <para/>
    /// You <b>must</b> be careful not to erroneously call this, as this being incorrectly tracked can lead to early
    /// unloading, or the asset becoming stuck loaded and leaked
    /// </summary>
    /// <seealso cref="AcquireGpuAsset"/>
    public void ReleaseGpuAsset() {
        Interlocked.Decrement(ref _gpuUsages);
    }

    /// <summary>
    /// Blocking wait for this asset to finish loading into Gpu memory.
    /// <para/>
    /// If this asset isn't currently loading or is unloading, then it will return immediately
    /// </summary>
    public void WaitForGpuLoad() {
        // Copy task as this can be cleared in another thread
        var task = _gpuLoadTask;
        if (!isGpuLoading || task == null) return;

        task.Wait();
    }

    /// <summary>
    /// The action performed when loading an asset into Gpu Memory. This will assume that the asset has already been
    /// loaded into Cpu memory.
    /// <para/>
    /// This cannot be interrupted, and will lock <see cref="_gpuLoadLock"/>
    /// </summary>
    private void GpuLoadJob() {
        GpuLoadAssetImpl();

        // We registered interest whilst loading, now we need to release our interest
        ReleaseCpuAsset();

        var lockTaken = false;
        try {
            _gpuLoadLock.Enter(ref lockTaken);
            _gpuLoadState = AssetLoadState.Loaded;
            _gpuLoadTask = null;
        }
        finally {
            if (lockTaken) _gpuLoadLock.Exit(false);
        }
    }

    /// <summary>
    /// The action performed when unloading an asset from Gpu memory
    /// <para/>
    /// This cannot be interrupted, and will lock <see cref="_gpuLoadLock"/>
    /// </summary>
    private void GpuUnloadJob() {
        GpuUnloadAssetImpl();

        var lockTaken = false;
        try {
            _gpuLoadLock.Enter(ref lockTaken);
            if (_gpuLoadState != AssetLoadState.Unloading) return;

            _gpuLoadState = AssetLoadState.Unloaded;
            _gpuLoadTask = null;
        }
        finally {
            if (lockTaken) _gpuLoadLock.Exit(false);
        }
    }

    /// <summary>
    /// Request this asset to be loaded into Gpu memory.
    /// <para/>
    /// This always happens asyncronously.
    /// </summary>
    /// <returns>A task representing the asynchronous Gpu load</returns>
    protected Task? RequestGpuLoadAsset() {
        // Lock as we are modifying _gpuLoadState and do not want multiple threads to enter this part of the function at
        // the same time
        var lockTaken = false;
        try {
            _gpuLoadLock.Enter(ref lockTaken);
            switch (_gpuLoadState) {
                case AssetLoadState.Unloaded:
                    _gpuLoadState = AssetLoadState.Loading;
                    _gpuLoadTask = AcquireCpuAsset().ContinueWith(_ => GpuLoadJob(),
                        TaskContinuationOptions.LongRunning);
                    return _gpuLoadTask;
                case AssetLoadState.Loading:
                    return _gpuLoadTask!;
                case AssetLoadState.Loaded:
                    return ThreadManager.instance.completedTask;
                case AssetLoadState.Unloading:
                    _gpuLoadState = AssetLoadState.Loading;

                    // This looks weird because we have to ensure the Cpu is loaded before we can try to load the Gpu
                    // asset again. This should resolve out correctly, even if the Cpu is currently unloading the asset
                    _gpuLoadTask = _gpuLoadTask!
                        .ContinueWith(_ => AcquireCpuAsset())
                        .ContinueWith(_ => GpuLoadJob(), TaskContinuationOptions.LongRunning);
                    return _gpuLoadTask;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        finally {
            if (lockTaken) _gpuLoadLock.Exit(false);
        }
    }

    /// <summary>
    /// Unload the asset from Gpu memory
    /// <para/>
    /// This always happens asyncronously.
    /// </summary>
    /// <returns>A task representing the asynchronous Gpu unload</returns>
    protected Task? GpuUnloadAsset() {
        var lockTaken = false;
        try {
            _gpuLoadLock.Enter(ref lockTaken);
            switch (_gpuLoadState) {
                case AssetLoadState.Unloaded:
                    return ThreadManager.instance.completedTask;
                case AssetLoadState.Loading:
                    return null;
                case AssetLoadState.Loaded:
                    _gpuLoadState = AssetLoadState.Unloading;
                    _gpuLoadTask = ThreadManager.instance.StartLongTask(GpuUnloadJob);
                    return _gpuLoadTask;
                case AssetLoadState.Unloading:
                    return _gpuLoadTask!;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        finally {
            if (lockTaken) _gpuLoadLock.Exit(false);
        }
    }

    /// <summary>
    /// The implementation to load the asset from Cpu memory into Gpu memory
    /// <para/>
    /// It is guaranteed that by the time this method is called, the asset will be available in Cpu memory
    /// </summary>
    protected abstract void GpuLoadAssetImpl();

    /// <summary>
    ///  The implementation to unload the asset from Gpu memory
    /// </summary>
    protected abstract void GpuUnloadAssetImpl();
}