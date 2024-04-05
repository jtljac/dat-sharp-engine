namespace dat_sharp_engine.AssetManagement;

public abstract class GpuAsset(string? path) : Asset(path) {
    private volatile AssetLoadState _gpuLoadState = AssetLoadState.Unloaded;
    public AssetLoadState gpuLoadState => _gpuLoadState;
}