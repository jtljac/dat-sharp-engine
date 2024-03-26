using CommandLine;
using dat_asset_processor.AssetProcessors;
using NLog;
using NLog.Fluent;

namespace dat_asset_processor;

// ReSharper disable once ClassNeverInstantiated.Global
internal class Options {
    [Option('a',
        "asset-path",
        Min = 1,
        Required = true,
        HelpText = "Add a path to a directory to process, you can specify multiple directories with multiple -a options. When there are files that have the same sub-path, the file in the most recently specified directory will be converted."
    )]
    public IEnumerable<string> Files { get; set; }

    [Option('s',
        "shader-include",
        Required = false,
        HelpText = "Add a path to a directory to use for shader includes, you can specify multiple directories with multiple -s options."
    )]
    public IEnumerable<string> ShaderIncludes { get; set; }

    [Option('o', "output", Required = true, HelpText = "A path to the directory to put the converted files in.")]
    public string Output { get; set; }

    [Option('f',
        "force",
        Required = false,
        Default = false,
        HelpText = "Force convert file even if there is already a newer file at the output"
    )]
    public bool Force { get; set; }

    [Option('S',
        "symlink",
        Required = false,
        Default = false,
        HelpText = "Create symlinks for unrecognised files that are usually copied across without any extra processing."
    )]
    public bool SymLink { get; set; }

    [Option('n',
        "no-default",
        Required = false,
        Default = false,
        HelpText = "Skip any files that do not have a registered Processor"
    )]
    public bool NoDefault { get; set; }

    [Option('w',
        "watch",
        Required = false,
        Default = false,
        HelpText = "Watch for changes and automatically process them"
    )]
    public bool Watch { get; set; }
}

public static class Program {
    private static Dictionary<string, IBaseAssetProcessor> _assetProcessors = null!;
    private static IBaseAssetProcessor _defaultProcessor = null!;
    private static Options _args = null!;

    private static void SetupProcessors() {
        _defaultProcessor = new CopyProcessor(_args.SymLink);

        _assetProcessors = new Dictionary<string, IBaseAssetProcessor>();
        var textureProcessor = new BasicMeshProcessor();
        _assetProcessors[".png"] = textureProcessor;

        _assetProcessors[".obj"] = new BasicMeshProcessor();
    }

    private static int Main(string[] args) {
        var parserResult = new Parser(settings => settings.AllowMultiInstance = true).ParseArguments<Options>(args);

        if (parserResult.Errors.Any()) {
            Logger.ProcessorLogger.Error("{}", parserResult.Errors);
            return 1;
        }

        _args = parserResult.Value;

        SetupProcessors();
        Console.Out.WriteLine(string.Join('\n', _args.Files));

        if (File.Exists(_args.Output)) {
            throw new IOException("Argument Output is a file. Must be a directory");
        }

        if (!Directory.Exists(_args.Output)) {
            Directory.CreateDirectory(_args.Output);
        }

        _args.Output = _args.Output;

        List<Watcher> watchers = [];

        foreach (var directory in _args.Files) {
            if (File.Exists(directory)) {
                Logger.ProcessorLogger.Error("Passed asset directory: {}, is a file, skipping", directory);
            } else if (!Directory.Exists(directory)) {
                Logger.ProcessorLogger.Error("Passed asset directory: {}, was not found, skipping", directory);
            } else {
                ProcessDirectory(directory, directory);

                // if (_args.Watch) {
                //     watchers.Add(new Watcher(directory, _args.Output));
                // }
            }
        }

        if (watchers.Count == 0) return 0;

        Logger.ProcessorLogger.Info("Starting Watcher");
        // // Loop infinitely
        // while (true) {
        //     watchers[0].WaitForChanged();
        // }

        return 0;
    }

    /// <summary>
    /// Recursively process directories
    /// </summary>
    /// <param name="directory">The directory to process</param>
    /// <param name="baseDirectory">The base directory, where sub-paths will be transferred to the output</param>
    public static void ProcessDirectory(string directory, string baseDirectory) {
        var outputDir = Path.Join(_args.Output, Path.GetRelativePath(baseDirectory, directory));

        if (File.Exists(outputDir)) {
            Logger.ProcessorLogger.Error(
                "File exists in output where folder is expected, skipping this directory\nInput Directory: {}\nOutputPath: {}",
                directory,
                outputDir
            );
            return;
        }

        if (!Directory.Exists(outputDir)) {
            Directory.CreateDirectory(outputDir);
        }

        foreach (var file in Directory.GetFiles(directory)) {
            ProcessFile(file, outputDir);
        }

        foreach (var subDir in Directory.GetDirectories(directory)) {
            ProcessDirectory(subDir, baseDirectory);
        }
    }

    public static void ProcessFile(string file, string outputDir) {
        // TODO: Some level of optimisation so this doesn't execute if it doesn't need to
        try {
            var processor = _assetProcessors.GetValueOrDefault(Path.GetExtension(file), _defaultProcessor);

            if ((_args.NoDefault) && processor == _defaultProcessor) return;

            var outputFile = processor.ProcessFile(file, outputDir);
            Logger.ProcessorLogger.Info("{}: \nInput: {}\nOutput: {}\n", processor.GetProcessorName(), file, outputFile);
        }
        catch (Exception e) {
            Logger.ProcessorLogger.Error("Failed to process \"{}\".\n Exception: {}", file, e);
        }
    }


}