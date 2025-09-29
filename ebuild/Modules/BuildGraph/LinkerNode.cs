using ebuild.api.Linker;
using ebuild.Modules.BuildGraph;
using Microsoft.Extensions.Logging;

namespace ebuild.BuildGraph
{
    class LinkerNode(LinkerBase linker, LinkerSettings settings) : Node("Linker")
    {
        public LinkerBase Linker = linker;
        public LinkerSettings Settings = settings;
        private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("LinkerNode");

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
                    if (File.Exists(inputFile))
                    {
                        var inputModTime = File.GetLastWriteTimeUtc(inputFile);
                        if (inputModTime > outputModTime)
                        {
                            Logger.LogInformation("Linking {outputFile}: Input file {inputFile} modified after output file", 
                                outputFile, inputFile);
                            return false;
                        }
                    }
                    else
                    {
                        Logger.LogInformation("Linking {outputFile}: Input file {inputFile} not found", 
                            outputFile, inputFile);
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
