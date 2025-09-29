using System.Text.Json;
using ebuild.api;
using ebuild.api.Compiler;

namespace ebuild.BuildGraph
{
    /// <summary>
    /// Manages compilation state tracking for incremental builds
    /// </summary>
    public class CompilationDatabase
    {
        private readonly string _databasePath;
        private readonly string _sourceFile;
        private CompilationEntry? _cached;

        public CompilationDatabase(string moduleDirectory, string moduleName, string sourceFile)
        {
            var dbDir = Path.Combine(moduleDirectory, ".ebuild", moduleName);
            try
            {
                Directory.CreateDirectory(dbDir);
            }
            catch
            {
                // If we can't create the directory, use a temp path that won't work
                // This allows the class to be constructed but operations will fail gracefully
            }
            
            var sourceFileName = Path.GetFileNameWithoutExtension(sourceFile);
            _databasePath = Path.Combine(dbDir, $"{sourceFileName}.compile.json");
            _sourceFile = sourceFile;
        }

        public CompilationEntry? GetEntry()
        {
            if (_cached != null)
                return _cached;

            if (!File.Exists(_databasePath))
                return null;

            try
            {
                var json = File.ReadAllText(_databasePath);
                _cached = JsonSerializer.Deserialize<CompilationEntry>(json);
                return _cached;
            }
            catch
            {
                return null;
            }
        }

        public void SaveEntry(CompilationEntry entry)
        {
            try
            {
                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_databasePath, json);
                _cached = entry;
            }
            catch
            {
                // Ignore save errors
            }
        }

        public void RemoveEntry()
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
                _cached = null;
            }
            catch
            {
                // Ignore delete errors
            }
        }

        public static CompilationEntry CreateFromSettings(CompilerSettings settings, string outputFile)
        {
            return new CompilationEntry
            {
                SourceFile = settings.SourceFile,
                OutputFile = outputFile,
                LastCompiled = DateTime.UtcNow,
                Definitions = settings.Definitions.Select(d => d.ToString()).ToList(),
                IncludePaths = new List<string>(settings.IncludePaths),
                ForceIncludes = new List<string>(settings.ForceIncludes),
                Dependencies = new List<string>()
            };
        }
    }

    public class CompilationEntry
    {
        public string SourceFile { get; set; } = string.Empty;
        public string OutputFile { get; set; } = string.Empty;
        public DateTime LastCompiled { get; set; }
        public List<string> Definitions { get; set; } = new();
        public List<string> IncludePaths { get; set; } = new();
        public List<string> ForceIncludes { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
    }
}