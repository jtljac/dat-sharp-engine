namespace dat_sharp_engine.Util;

public class FileUtil {
    /// <summary>
    /// Get the directory that contains the game application, I.E. where all the game files are
    /// </summary>
    /// <returns>The game's base directory</returns>
    public static string GetAppDirectory() {
        return AppContext.BaseDirectory;
    }

    /// <summary>
    /// Get the directory where all the assets are stored
    /// </summary>
    /// <returns>The game's Asset Directory</returns>
    public static string GetAssetDirectory() {
        return Path.Join(GetAppDirectory(), "assets");
    }
}