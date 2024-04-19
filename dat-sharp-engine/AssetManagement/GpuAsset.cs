using dat_sharp_vfs;

namespace dat_sharp_engine.AssetManagement;

public abstract class GpuAsset : Asset {
    protected GpuAsset() { }
    protected GpuAsset(string? path, DVfsFile? file) : base(path, file) { }

    private volatile AssetLoadState _gpuLoadState = AssetLoadState.Unloaded;
    public AssetLoadState gpuLoadState => _gpuLoadState;
}