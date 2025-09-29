using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ebuild.api.Compiler;

namespace ebuild.Modules.BuildGraph;

/// <summary>
/// Manages compilation state tracking for incremental builds
/// </summary>
public class CompilationDatabase
{
    private static readonly Dictionary<string, CompilationDatabase> _dbCache = [];

#pragma warning disable IDE0052 // Fields are used for serialization
    private readonly string _databasePath;
    private readonly string _sourceFile;
    private CompilationEntry? _cached;
#pragma warning restore IDE0052

    private CompilationDatabase(string moduleDirectory, string moduleName, string sourceFile)
    {
        var dbDir = Path.Combine(moduleDirectory, ".ebuild", moduleName, "compdb");
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
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourceFile));
        var hexHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        _databasePath = Path.Combine(dbDir, $"{sourceFileName}-${hexHash}.compile.json");
        _sourceFile = sourceFile;
    }

    public static CompilationDatabase Get(string moduleDirectory, string moduleName, string sourceFile)
    {
        var dbDir = Path.Combine(moduleDirectory, ".ebuild", moduleName, "compdb");
        var sourceFileName = Path.GetFileNameWithoutExtension(sourceFile);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourceFile));
        var hexHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        var dbPath = Path.Combine(dbDir, $"{sourceFileName}-${hexHash}.compile.json");
        lock (_dbCache)
        {
            if (_dbCache.TryGetValue(dbPath, out var db))
                return db;
            db = new CompilationDatabase(moduleDirectory, moduleName, sourceFile);
            _dbCache[dbPath] = db;
            return db;
        }
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
            IncludePaths = [.. settings.IncludePaths],
            ForceIncludes = [.. settings.ForceIncludes],
            Dependencies = []
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