namespace dat_asset_processor;

/// <summary>
/// An interface for processing a file to convert it into a DatSharpEngine friendly format and deposit it
/// </summary>
public interface IBaseAssetProcessor {
    string GetProcessorName() {
        return GetType().Name;
    }

    /// <summary>
    /// Process a file
    /// <para/>
    /// It can be assumed when this method is called that it may overwrite the file at the dest if it already exists
    /// </summary>
    /// <param name="src">The file to process</param>
    /// <param name="destDir">The directory to write the processed file into</param>
    /// <returns>The path to the output file</returns>
    string ProcessFile(string src, string destDir);
}