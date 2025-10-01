using System.Security.Cryptography;
using System.Text;
using ebuild.api.Compiler;
using Microsoft.Data.Sqlite;

namespace ebuild.Modules.BuildGraph;

/// <summary>
/// Manages compilation state tracking for incremental builds using SQLite
/// </summary>
public class CompilationDatabase
{
    private static readonly Dictionary<string, CompilationDatabase> _dbCache = [];

    private readonly string _databasePath;
    private readonly string _sourceFile;
    private readonly string _moduleName;
    private CompilationEntry? _cached;

    private CompilationDatabase(string moduleDirectory, string moduleName, string sourceFile)
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

        _databasePath = Path.Combine(dbDir, "compilation.db");
        _sourceFile = sourceFile;
        _moduleName = moduleName;
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS compilation_entries (
                    source_file TEXT PRIMARY KEY,
                    output_file TEXT NOT NULL,
                    last_compiled TEXT NOT NULL,
                    definitions TEXT NOT NULL,
                    include_paths TEXT NOT NULL,
                    force_includes TEXT NOT NULL,
                    dependencies TEXT NOT NULL
                )";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Ignore initialization errors - operations will fail gracefully
        }
    }

    public static CompilationDatabase Get(string moduleDirectory, string moduleName, string sourceFile)
    {
        var dbPath = Path.Combine(moduleDirectory, ".ebuild", moduleName, "compilation.db");
        var cacheKey = $"{dbPath}:{sourceFile}";
        lock (_dbCache)
        {
            if (_dbCache.TryGetValue(cacheKey, out var db))
                return db;
            db = new CompilationDatabase(moduleDirectory, moduleName, sourceFile);
            _dbCache[cacheKey] = db;
            return db;
        }
    }

    public CompilationEntry? GetEntry()
    {
        if (_cached != null)
            return _cached;

        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT source_file, output_file, last_compiled, definitions, include_paths, force_includes, dependencies
                FROM compilation_entries
                WHERE source_file = $sourceFile";
            command.Parameters.AddWithValue("$sourceFile", _sourceFile);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                _cached = new CompilationEntry
                {
                    SourceFile = reader.GetString(0),
                    OutputFile = reader.GetString(1),
                    LastCompiled = DateTime.Parse(reader.GetString(2)),
                    Definitions = DeserializeList(reader.GetString(3)),
                    IncludePaths = DeserializeList(reader.GetString(4)),
                    ForceIncludes = DeserializeList(reader.GetString(5)),
                    Dependencies = DeserializeList(reader.GetString(6))
                };
                return _cached;
            }

            return null;
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
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO compilation_entries 
                (source_file, output_file, last_compiled, definitions, include_paths, force_includes, dependencies)
                VALUES ($sourceFile, $outputFile, $lastCompiled, $definitions, $includePaths, $forceIncludes, $dependencies)";
            
            command.Parameters.AddWithValue("$sourceFile", entry.SourceFile);
            command.Parameters.AddWithValue("$outputFile", entry.OutputFile);
            command.Parameters.AddWithValue("$lastCompiled", entry.LastCompiled.ToString("o"));
            command.Parameters.AddWithValue("$definitions", SerializeList(entry.Definitions));
            command.Parameters.AddWithValue("$includePaths", SerializeList(entry.IncludePaths));
            command.Parameters.AddWithValue("$forceIncludes", SerializeList(entry.ForceIncludes));
            command.Parameters.AddWithValue("$dependencies", SerializeList(entry.Dependencies));

            command.ExecuteNonQuery();
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
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM compilation_entries WHERE source_file = $sourceFile";
            command.Parameters.AddWithValue("$sourceFile", _sourceFile);
            command.ExecuteNonQuery();

            _cached = null;
        }
        catch
        {
            // Ignore delete errors
        }
    }

    private static string SerializeList(List<string> list)
    {
        return string.Join("\n", list.Select(s => Convert.ToBase64String(Encoding.UTF8.GetBytes(s))));
    }

    private static List<string> DeserializeList(string serialized)
    {
        if (string.IsNullOrEmpty(serialized))
            return new List<string>();
        
        return serialized.Split('\n')
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => Encoding.UTF8.GetString(Convert.FromBase64String(s)))
            .ToList();
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