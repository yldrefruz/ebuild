using System.Text.Json.Nodes;
using ebuild.api;
using ebuild.api.Compiler;
using Microsoft.Extensions.Logging;

namespace ebuild.Modules.BuildGraph;

class CompileSourceFileNode : Node
{
    public CompilerBase Compiler;
    public CompilerSettings Settings;
    private static readonly object _moduleRegistryLock = new();
    private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("CompileSourceFileNode");

    public CompileSourceFileNode(CompilerBase compiler, CompilerSettings settings) : base("CompileSourceFile")
    {
        Compiler = compiler;
        Settings = settings;
        Name = $"Compile(\"{settings.SourceFile}\" -> \"{settings.OutputFile}\")";
    }

    public async override Task ExecuteAsync(IWorker worker, CancellationToken cancellationToken = default)
    {
        if (worker is GenerateCompileCommandsJsonWorker)
        {
            if (Parent is ModuleDeclarationNode parentModuleNode)
            {
                Dictionary<ModuleBase, List<JsonObject>> compileCommandsModuleRegistry = worker.GlobalMetadata["compile_commands_module_registry"] as Dictionary<ModuleBase, List<JsonObject>> ?? throw new Exception("Global metadata compile_commands_module_registry is not of the correct type.");
                // Get the registry from the global metadata
                if (compileCommandsModuleRegistry == null)
                {
                    throw new Exception("Global metadata compile_commands_module_registry is null");
                }
                compileCommandsModuleRegistry.TryGetValue(parentModuleNode.Module, out List<JsonObject>? possibleList);
                // If the list exists for the module, use it otherwise create and assign it.
                if (possibleList == null)
                {
                    lock (_moduleRegistryLock)
                    {
                        possibleList = compileCommandsModuleRegistry[parentModuleNode.Module] = [];
                    }
                }

                await Compiler.Generate(Settings, cancellationToken, "compile_commands.json", possibleList);
            }

        }
        else if (worker is BuildWorker && ShouldSkipCompilation())
        {
            // Skip compilation - file is up to date
            return;
        }
        else
        {
            Logger.LogTrace("Compiling source file: {file}", Settings.SourceFile);
            var compilationSuccessful = await Compiler.Compile(Settings, cancellationToken);

            // Update compilation database based on result (only for BuildWorker)
            if (worker is BuildWorker)
            {
                if (compilationSuccessful)
                {
                    UpdateCompilationDatabase();
                }
                // else
                // {
                //     RemoveCompilationDatabase();
                // }
            }

            if (!compilationSuccessful)
            {
                throw new Exception($"Compilation failed for {Settings.SourceFile}");
            }
        }
    }

    private bool ShouldSkipCompilation()
    {
        try
        {
            if (Parent is not ModuleDeclarationNode parentModuleNode)
                return false;

            var module = parentModuleNode.Module;
            var outputFile = Settings.OutputFile;

            // Always compile if output file doesn't exist
            if (!File.Exists(outputFile))
            {
                Logger.LogTrace("Compiling {sourceFile}: Output file {outputFile} not found", Settings.SourceFile, outputFile);
                return false;
            }

            var outputModTime = File.GetLastWriteTimeUtc(outputFile);
            var sourceModTime = File.GetLastWriteTimeUtc(Settings.SourceFile);

            // Check if source file is newer than output
            if (sourceModTime > outputModTime)
            {
                Logger.LogTrace("Compiling {sourceFile}: Source file modified after output file", Settings.SourceFile);
                return false;
            }

            // Get compilation database
            var database = CompilationDatabase.Get(
                module.Context.ModuleDirectory.FullName,
                module.Name ?? "Unknown",
                Settings.SourceFile);

            var entry = database.GetEntry();
            if (entry == null)
            {
                Logger.LogTrace("Compiling {sourceFile}: No compilation database entry found", Settings.SourceFile);
                return false;
            }

            // Check if definitions have changed
            var currentDefs = Settings.Definitions.Select(d => d.ToString()).OrderBy(s => s).ToList();
            var cachedDefs = entry.Definitions.OrderBy(s => s).ToList();
            if (!currentDefs.SequenceEqual(cachedDefs))
            {
                Logger.LogTrace("Compiling {sourceFile}: Definitions have changed", Settings.SourceFile);
                return false;
            }

            // Check if include paths have changed
            var currentIncludes = Settings.IncludePaths.OrderBy(s => s).ToList();
            var cachedIncludes = entry.IncludePaths.OrderBy(s => s).ToList();
            if (!currentIncludes.SequenceEqual(cachedIncludes))
            {
                Logger.LogTrace("Compiling {sourceFile}: Include paths have changed", Settings.SourceFile);
                return false;
            }

            // Check if force includes have changed
            var currentForceIncludes = Settings.ForceIncludes.OrderBy(s => s).ToList();
            var cachedForceIncludes = entry.ForceIncludes.OrderBy(s => s).ToList();
            if (!currentForceIncludes.SequenceEqual(cachedForceIncludes))
            {
                Logger.LogTrace("Compiling {sourceFile}: Force includes have changed", Settings.SourceFile);
                return false;
            }

            // Scan current dependencies
            var allIncludePaths = new List<string>(Settings.IncludePaths);
            var sourceDir = Path.GetDirectoryName(Settings.SourceFile);
            if (!string.IsNullOrEmpty(sourceDir))
                allIncludePaths.Insert(0, sourceDir);

            var currentDeps = DependencyScanner.ScanDependencies(Settings.SourceFile, allIncludePaths, module);
            // Recursively scan dependencies for each force include
            foreach (var forceInclude in Settings.ForceIncludes)
            {
                var forceIncludeDeps = DependencyScanner.ScanDependencies(forceInclude, allIncludePaths, module);
                currentDeps.Add(forceInclude);
                currentDeps.AddRange(forceIncludeDeps);
            }
            currentDeps = currentDeps.Distinct().OrderBy(s => s).ToList();

            // Check if dependencies have changed
            var cachedDeps = entry.Dependencies.OrderBy(s => s).ToList();
            if (!currentDeps.SequenceEqual(cachedDeps))
            {
                Logger.LogTrace("Compiling {sourceFile}: Dependencies have changed", Settings.SourceFile);
                return false;
            }

            // Check if any dependency file is newer than output
            var latestDepTime = DependencyScanner.GetLatestModificationTime(currentDeps);
            if (latestDepTime > outputModTime)
            {
                Logger.LogTrace("Compiling {sourceFile}: Dependency file modified after output file", Settings.SourceFile);
                return false;
            }

            // All checks passed - can skip compilation
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Error checking compilation status for {sourceFile}: {error}. Will compile.",
                Settings.SourceFile, ex.Message);
            return false;
        }
    }

    private void UpdateCompilationDatabase()
    {
        try
        {
            if (Parent is not ModuleDeclarationNode parentModuleNode)
                return;

            var module = parentModuleNode.Module;
            var database = CompilationDatabase.Get(
                module.Context.ModuleDirectory.FullName,
                module.Name ?? "Unknown",
                Settings.SourceFile);

            // Scan dependencies
            var allIncludePaths = new List<string>(Settings.IncludePaths);
            var sourceDir = Path.GetDirectoryName(Settings.SourceFile);
            if (!string.IsNullOrEmpty(sourceDir))
                allIncludePaths.Insert(0, sourceDir);

            var dependencies = DependencyScanner.ScanDependencies(Settings.SourceFile, allIncludePaths, module);
            // Recursively scan dependencies for each force include
            foreach (var forceInclude in Settings.ForceIncludes)
            {
                var forceIncludeDeps = DependencyScanner.ScanDependencies(forceInclude, allIncludePaths, module);
                dependencies.Add(forceInclude);
                dependencies.AddRange(forceIncludeDeps);
            }
            dependencies = dependencies.Distinct().ToList();

            var entry = CompilationDatabase.CreateFromSettings(Settings, Settings.OutputFile);
            entry.Dependencies = dependencies;

            database.SaveEntry(entry);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to update compilation database for {sourceFile}: {error}",
                Settings.SourceFile, ex.Message);
        }
    }

    private void RemoveCompilationDatabase()
    {
        try
        {
            if (Parent is not ModuleDeclarationNode parentModuleNode)
                return;

            var module = parentModuleNode.Module;
            var database = CompilationDatabase.Get(
                module.Context.ModuleDirectory.FullName,
                module.Name ?? "Unknown",
                Settings.SourceFile);

            database.RemoveEntry();
            Logger.LogTrace("Removed compilation database entry for failed compilation: {sourceFile}", Settings.SourceFile);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to remove compilation database entry for {sourceFile}: {error}",
                Settings.SourceFile, ex.Message);
        }
    }

    public override string ToString() => $"CompileSourceFileNode({Settings.SourceFile})";
}
