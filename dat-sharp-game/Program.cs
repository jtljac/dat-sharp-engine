// See https://aka.ms/new-console-template for more information

using dat_sharp_engine;
using dat_sharp_engine.Util;
using SmartFormat;
using Version = Silk.NET.SDL.Version;

// var testing = 4;
// var testing2 = "Testing";
//
// var test = Localisation.Formattable($"{testing2}");
// System.Console.WriteLine(test);

DatSharpEngine.Instance.Initialise(new ApplicationSettings {
    name = "Test Game",
    version = new Version(0, 1, 0)
});

DatSharpEngine.Instance.StartLoop();
