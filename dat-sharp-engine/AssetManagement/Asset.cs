using dat_sharp_vfs;
using NLog;
using Logger = dat_sharp_engine.Util.Logger;

namespace dat_sharp_engine.AssetManagement;

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
    private WeakReference<DVfsFile>? _file;

    /// <summary>
    /// If <c>true</c>, this asset does not exist on the disk and therefore has no loading or unloading functionality
    /// </summary>
    public bool isVirtual => path == null;

    private volatile AssetLoadState _loadState;
    private volatile Task? _loadTask;

    /// <summary>
    /// Base initialiser for virtual file
    /// </summary>
    protected Asset() {
        path = null;
        _file = null;
        _loadState = AssetLoadState.Loaded;
    }

    /// <summary>
    /// Base initialiser for file backed asset
    /// </summary>
    /// <param name="path">The path to the file in the VFS</param>
    /// <param name="file">The file entry in the VFS, pass <c>null</c> for a lazily loaded asset</param>
    protected Asset(string? path, DVfsFile? file) {
        _loadState = AssetLoadState.Unloaded;
        this.path = path;

        if (file == null) {
            _file = null;
            return;
        }

        _file = new WeakReference<DVfsFile>(file);

        if (file.references > 1) Logger.EngineLogger.Warn(
            "Asset at {} is stored in the VFS multiple times, this asset may not detect when it's file has been replaced in the VFS",
            path
        );
    }
    public AssetLoadState loadState => _loadState;

    ~Asset() {
        UnloadAssetImpl();
    }

    public Task LoadAsset() {
        void LoadAction() {
            if (_loadState != AssetLoadState.Loading) return;
            var rawFile = GetRawFile();
            if (rawFile == null) return;

            LoadAssetImpl(rawFile.GetFileStream());

            _loadState = AssetLoadState.Loaded;
            _loadTask = null;
        }
        
        if (isVirtual) return Task.CompletedTask;
        lock (this) {
            switch (loadState) {
                case AssetLoadState.Unloaded:
                    _loadState = AssetLoadState.Loading;

                    _loadTask = new Task(LoadAction, TaskCreationOptions.LongRunning);
                    return _loadTask;
                case AssetLoadState.Loading:
                    return _loadTask!;
                case AssetLoadState.Loaded:
                    return Task.CompletedTask;
                case AssetLoadState.Unloading:
                    _loadState = AssetLoadState.Loading;
                    _loadTask = _loadTask!.ContinueWith(_ => {
                        LoadAction();
                    });
                    return _loadTask;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public Task UnloadAsset() {
        if (isVirtual) return Task.CompletedTask;
        lock (this) {
            switch (_loadState) {
                case AssetLoadState.Unloaded:
                    return Task.CompletedTask;
                case AssetLoadState.Loading:
                    _loadState = AssetLoadState.Unloading;
                    _loadTask = _loadTask!.ContinueWith(task => {
                            // Start unload again
                        }
                    );
                    return _loadTask;
                case AssetLoadState.Loaded:
                    _loadTask = new Task(() => {
                        // Queue unload
                    }, TaskCreationOptions.LongRunning);
                    return _loadTask;
                case AssetLoadState.Unloading:
                    return _loadTask!;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    protected DVfsFile? GetRawFile() {
        if (isVirtual) return null;

        if (_file != null && _file.TryGetTarget(out var file)) return file;

        file = AssetManager.instance.GetRawAssetFile(path!);
        _file = new WeakReference<DVfsFile>(file);

        return file;
    }

    protected abstract void LoadAssetImpl(Stream assetData);

    protected abstract void UnloadAssetImpl();
}