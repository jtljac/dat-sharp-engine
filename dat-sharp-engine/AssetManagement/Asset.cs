namespace dat_sharp_engine.AssetManagement;

public abstract class Asset(string? path) {
    /// <summary>
    /// The path to the file in the <see cref="AssetManager"/>.
    /// <para/>
    /// If this is <c>null</c> then the asset is virtual, see <see cref="isVirtual"/>.
    /// </summary>
    public readonly string? path = path;

    /// <summary>
    /// If <c>true</c>, this asset does not exist on the disk and therefore has no loading or unloading functionality
    /// </summary>
    public bool isVirtual => path == null;

    private volatile AssetLoadState _loadState = AssetLoadState.Unloaded;
    private volatile Task? loadTask;
    public AssetLoadState loadState => _loadState;

    ~Asset() {
        UnloadAssetImpl();
    }

    public Task LoadAsset() {
        if (isVirtual) return Task.CompletedTask;
        lock (this) {
            switch (loadState) {
                case AssetLoadState.Unloaded:
                    // Queue load
                    break;
                case AssetLoadState.Loading:
                    return loadTask!;
                case AssetLoadState.Loaded:
                    return Task.CompletedTask;
                case AssetLoadState.Unloading:
                    _loadState = AssetLoadState.Loading;
                    loadTask = loadTask!.ContinueWith(task => {
                        // Start load again
                    });
                    return loadTask;
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
                    loadTask = loadTask!.ContinueWith(task => {
                            // Start unload again
                        }
                    );
                    return loadTask;
                case AssetLoadState.Loaded:
                    // Queue unload
                    break;
                case AssetLoadState.Unloading:
                    return loadTask!;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public abstract void LoadAsset(Stream assetData);

    protected abstract void UnloadAssetImpl();
}