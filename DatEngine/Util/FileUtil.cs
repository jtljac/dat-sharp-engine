namespace DatEngine.Util;

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

    /// <summary>
    /// Get the directory that can be used for storing use save content and settings
    /// <para>
    /// On Windows, this is typically <c>%appdata%/Roaming</c>
    /// </para>
    /// <para>
    /// On Linux, this is typically <c>~/.config</c>
    /// </para>
    /// </summary>
    /// <param name="gameName">The name of the game (You need the subfolder)</param>
    /// <returns>The directory in which persistent content can be stored</returns>
    public static string GetUserDirectory(string gameName) {
        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), gameName);
    }
}