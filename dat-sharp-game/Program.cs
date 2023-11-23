// See https://aka.ms/new-console-template for more information

using dat_sharp_engine;
using Version = Silk.NET.SDL.Version;

DatSharpEngine engine = new(new ApplicationSettings {
    name = "Test Game",
    version = new Version(0, 1, 0)
});

engine.StartLoop();
