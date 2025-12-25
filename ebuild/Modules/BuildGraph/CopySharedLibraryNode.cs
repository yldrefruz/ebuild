using ebuild.api;

namespace ebuild.Modules.BuildGraph;



class CopySharedLibraryToRootModuleBinNode(string name, string sourcePath) : Node(name)
{
    public string SourcePath { get; init; } = sourcePath;
    public override async Task ExecuteAsync(IWorker worker, CancellationToken cancellationToken)
    {
        ModuleBase root = worker.WorkingGraph.Module;
        string outDir = root.GetBinaryOutputDirectory();
        try
        {
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            File.Copy(SourcePath, Path.Combine(outDir, Path.GetFileName(SourcePath)), true);
            var platform = worker.WorkingGraph.Module.Context.Platform;
            if (platform.SupportsDebugFiles)
            {
                var debugFilePath = Path.ChangeExtension(SourcePath, worker.WorkingGraph.Module.Context.Platform.ExtensionForCompiledSourceFile_DebugFile);
                if (File.Exists(debugFilePath))
                {
                    File.Copy(debugFilePath, Path.Combine(outDir, Path.GetFileName(debugFilePath)), true);
                }
            }
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to copy shared library from \"{SourcePath}\" to \"{Path.Combine(outDir, Path.GetFileName(SourcePath))}\": {ex.Message}", ex);
        }
    }
}