namespace dat_asset_processor.AssetProcessors;

/// <summary>
/// A simple asset processor that copies the src file to the destination without modification
/// </summary>
public class CopyProcessor(bool symLink) : IBaseAssetProcessor {
    /// <inheritdoc />
    public string GetProcessorName() {
        return "Copy Processor";
    }

    public string ProcessFile(string src, string destDir) {
        var destFile = Path.Join(destDir, Path.GetFileName(src));

        if (symLink) {
            if (File.Exists(destFile)) {
                File.Delete(destFile);
            }

            File.CreateSymbolicLink(src, destFile);
        } else {
            File.Copy(src, destFile, true);
        }

        return destFile;
    }
}