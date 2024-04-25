using dat_sharp_engine;
using Version = Silk.NET.SDL.Version;

DatSharpEngine.instance.Initialise(new ApplicationSettings {
    name = "Test Game",
    version = new Version(0, 1, 0)
});

DatSharpEngine.instance.StartLoop();
