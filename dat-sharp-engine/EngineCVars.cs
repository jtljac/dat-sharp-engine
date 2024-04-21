using System.Globalization;
using dat_sharp_engine.Util;

namespace dat_sharp_engine;

/// <summary>
/// A class to hold all the CVars used by the engine
/// <para/>
/// This is necessary as C# Lazy initialises class statics, meaning CVars defined inside classes will not be setup until
/// after the class has first been accessed
/// </summary>
public static class EngineCVars {
    // Core
    public static readonly CVar<uint> ThreadCountCVar = new("uThreadCount",
        "The number of extra threads to create for the engine, 0 to disable",
        0,
        CVarCategory.Core,
        CVarFlags.RequiresRestart
    );

    public static readonly CVar<float> LongRunningThreadRatioCVar = new("fLongThreadRatio",
        "The % of threads that are allowed to execute long running tasks, for example IO based Tasks",
        0.25f,
        CVarCategory.Core,
        CVarFlags.RequiresRestart,
        value => float.Clamp(value, 0, 1)
    );

    public static readonly CVar<string> LocaleCVar = new("sLocale", "The locale code currently being used for localisation", "en-US", CVarCategory.Core, CVarFlags.None,
        value => {
            try {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                CultureInfo.GetCultureInfo(LocaleCVar.value);

                return value;
            }
            catch (CultureNotFoundException e) {
                throw new ArgumentException("Unsupported Localisation", e);
            }
        });
    
    // Graphics
    public static readonly CVar<bool> ResizableCvar = new("bWindowResizable", "Allow resizing the game window", false, CVarCategory.Graphics, CVarFlags.None);
    public static readonly CVar<int> WindowModeCvar = new("eWindowMode", "The window mode, 0 for windowed, 1 for fullscreen, 2 for borderless", 0, CVarCategory.Graphics, CVarFlags.None);
    public static readonly CVar<int> WindowWidthCvar = new("uWindowWidth", "The width of the game window", 1366, CVarCategory.Graphics, CVarFlags.None);
    public static readonly CVar<int> WindowHeightCvar = new("uWindowHeight", "The height of the game window", 768, CVarCategory.Graphics, CVarFlags.None);
    public static readonly CVar<int> GpuUuidCvar = new("iGpuUuid", "The UUID of the GPU to use for rendering", 0, CVarCategory.Graphics, CVarFlags.None);
    public static readonly CVar<uint> BufferedFramesCvar = new("iBufferedFrames", "The number of buffered frames to render with", 2, CVarCategory.Graphics, CVarFlags.None);
    public static readonly CVar<bool> VsyncCvar = new("bVsync", "Enable VSync", true, CVarCategory.Graphics, CVarFlags.None);
}