using ebuild.api.Linker;
using ebuild.Modules.BuildGraph;
using Microsoft.Extensions.Logging;

namespace ebuild.BuildGraph
{
    class LinkerNode : Node
    {
        public LinkerBase Linker;
        public LinkerSettings Settings;
        private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("LinkerNode");

        public LinkerNode(LinkerBase linker, LinkerSettings settings) : base("Linker")
        {
            Linker = linker;
            Settings = settings;
            Name = $"Linker({Path.GetFileName(settings.OutputFile)}) <- [{string.Join(", ", settings.InputFiles.Select(f => Path.GetFileName(f)))}]";
        }

        public override async Task ExecuteAsync(IWorker worker, CancellationToken cancellationToken = default)
        {
            await base.ExecuteAsync(worker, cancellationToken);

            if (worker is BuildWorker && ShouldSkipLinking())
            {
                // Skip linking - output is up to date
                return;
            }

            bool result = await Linker.Link(Settings, cancellationToken);
            if (!result)
                throw new Exception($"Linking failed for output file {Settings.OutputFile}");
        }

        private bool ShouldSkipLinking()
        {
            try
            {
                var outputFile = Settings.OutputFile;

                // Always link if output file doesn't exist
                if (!File.Exists(outputFile))
                {
                    Logger.LogInformation("Linking {outputFile}: Output file not found", outputFile);
                    return false;
                }

                var outputModTime = File.GetLastWriteTimeUtc(outputFile);

                // Check if any input file is newer than output
                foreach (var inputFile in Settings.InputFiles)
                {
                    string foundInputFile = inputFile;
                    if (!File.Exists(inputFile))
                    {
                        // Search in library paths
                        bool found = false;
                        foreach (var libPath in Settings.LibraryPaths)
                        {
                            var candidate = Path.Combine(libPath, Path.GetFileName(inputFile));
                            if (File.Exists(candidate))
                            {
                                foundInputFile = candidate;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            Logger.LogInformation("Linking {outputFile}: Input file {inputFile} not found in library paths",
                                outputFile, inputFile);
                            return false;
                        }
                    }
                    var inputModTime = File.GetLastWriteTimeUtc(foundInputFile);
                    if (inputModTime > outputModTime)
                    {
                        Logger.LogDebug("Linking {outputFile}: Input file {inputFile} modified after output file",
                            outputFile, foundInputFile);
                        return false;
                    }
                }

                // All checks passed - can skip linking
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Error checking linking status for {outputFile}: {error}. Will link.",
                    Settings.OutputFile, ex.Message);
                return false;
            }
        }

        public override string ToString() => $"- LinkerNode({Settings.OutputFile})";
    }
}
