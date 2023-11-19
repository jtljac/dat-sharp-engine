using Version = Silk.NET.SDL.Version;

namespace dat_sharp_engine; 

public interface IApplicationSettings {
    public string getName();
    public Version getVersion();
}