// See https://aka.ms/new-console-template for more information

using Version = Silk.NET.SDL.Version;

namespace dat_sharp_engine
{
    class Program
    {
        static void Main(string[] args) {
            DatSharpEngine engine = new DatSharpEngine(new ApplicationSettings {
                name = "Test Game",
                version = new Version(0, 1, 0)
            });
            engine.StartLoop();
        }
    }
}
