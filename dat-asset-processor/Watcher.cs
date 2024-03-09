namespace dat_asset_processor;

public class Watcher {
    private readonly FileSystemWatcher _watcher;
    private readonly string _baseDirectory;
    private readonly string _outputDirectory;
    public Watcher(string watchDirectory, string outputDirectory) {
        _baseDirectory = watchDirectory;
        _outputDirectory = outputDirectory;
        _watcher = new FileSystemWatcher(watchDirectory);

        _watcher.NotifyFilter = NotifyFilters.Attributes
                                | NotifyFilters.CreationTime
                                | NotifyFilters.DirectoryName
                                | NotifyFilters.FileName
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Security
                                | NotifyFilters.Size;

        _watcher.IncludeSubdirectories = true;
        _watcher.EnableRaisingEvents = true;

        _watcher.Changed += OnChanged;
        _watcher.Created += OnCreated;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;
    }

    ~Watcher() {
        _watcher.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs e) {
        if (e.ChangeType != WatcherChangeTypes.Changed || Directory.Exists(e.FullPath)) return;

        var outputDir = GetOutputDirectory(e.FullPath);

        Program.ProcessFile(e.FullPath, outputDir);
    }

    private void OnCreated(object sender, FileSystemEventArgs e) {
        var outputDir = GetOutputDirectory(e.FullPath);

        if (Directory.Exists(e.FullPath)) {
            Directory.CreateDirectory(outputDir);
        } else if ((File.GetAttributes(e.FullPath) & FileAttributes.Hidden) == 0){
            Program.ProcessFile(e.FullPath, outputDir);
        }
    }
    private void OnDeleted(object sender, FileSystemEventArgs e) {
        var outputDir = GetOutputDirectory(e.FullPath);

        if (Directory.Exists(e.FullPath)) {
            Directory.Delete(outputDir, true);
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e) {
        var outputDir = GetOutputDirectory(e.FullPath);

        if (Directory.Exists(e.FullPath)) {
            Directory.Delete(GetOutputDirectory(e.OldFullPath));
            Directory.CreateDirectory(e.FullPath);
            Program.ProcessDirectory(outputDir, _baseDirectory);
        } else {
            Program.ProcessFile(e.FullPath, outputDir);
        }
    }

    private void OnError(object sender, ErrorEventArgs e) {
        Logger.ProcessorLogger.Error("Watcher for \"{}\" experienced an error:\n{}", _baseDirectory, e.GetException());
        throw new NotImplementedException();
    }

    private string GetOutputDirectory(string filePath) {
        return Path.GetDirectoryName(Path.Join(_outputDirectory, Path.GetRelativePath(_baseDirectory, filePath)))!;
    }

    public void WaitForChanged() {
        _watcher.WaitForChanged(WatcherChangeTypes.All);
    }
}