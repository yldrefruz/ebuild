using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ebuild.api
{
    /// <summary>
    /// A collection of small helper methods used by module implementations and the build system.
    /// This static class contains file gathering helpers, archive download/extraction utilities,
    /// and a CMake-style configure_file implementation used by module templates.
    /// </summary>
    public static class ModuleUtilities
    {
        /// <summary>
        /// Recursively finds all files under the directory resolved from <paramref name="root"/>
        /// (resolved relative to the module's directory) that match any of the supplied
        /// <paramref name="extensions"/>. Extensions should be provided without a leading dot
        /// (for example: "cpp", "c", "h").
        /// </summary>
        /// <param name="module">Module instance used to resolve the module directory.</param>
        /// <param name="root">Relative or absolute path to search under (relative paths are resolved against <c>module.Context.ModuleDirectory</c>).</param>
        /// <param name="extensions">One or more file extensions to include (without leading dot).</param>
        /// <returns>An array of absolute file paths that match the requested extensions. Returns an empty array when none are found.</returns>
        public static string[] GetAllSourceFiles(this ModuleBase module, string root, params string[] extensions)
        {
            List<string> files = [];
            string findAt = Path.GetFullPath(root, module.Context.ModuleDirectory!.FullName);
            foreach (var extension in extensions)
            {
                files.AddRange(Directory.GetFiles(findAt, "*." + extension, SearchOption.AllDirectories));
            }

            return files.ToArray();
        }

        /// <summary>
        /// Downloads an archive from <paramref name="Url"/>, optionally validates its SHA-256
        /// hash against <paramref name="ExpectedHash"/>, stores it as <c>source.zip</c> inside
        /// <paramref name="ExtractDirectory"/>, and extracts the archive to that directory.
        /// If a cached <c>source.zip</c> already exists and matches the expected hash it is reused.
        /// </summary>
        /// <param name="Url">HTTP(S) URL of the archive to download.</param>
        /// <param name="ExtractDirectory">Directory to write the archive and extract its contents into.</param>
        /// <param name="ExpectedHash">Optional hex-encoded SHA-256 string used to validate the downloaded bytes. If null no validation is performed.</param>
        /// <returns><c>true</c> when the archive is present and extracted successfully; <c>false</c> on network failure or hash mismatch.</returns>
        public static bool GetAndExtractSourceFromArchiveUrl(string Url, string ExtractDirectory, string? ExpectedHash)
        {

            using var client = new HttpClient();
            var archiveDir = Path.Join(ExtractDirectory, "source.zip");
            if (File.Exists(archiveDir))
            {
                var content = File.ReadAllBytes(archiveDir);
                var hash = SHA256.HashData(content);
                if (ExpectedHash == null || (ExpectedHash != null && hash.SequenceEqual(Convert.FromHexString(ExpectedHash))))
                {
                    return true;
                }
            }
            var response = client.GetAsync(Url).Result;
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsByteArrayAsync().Result;
                Directory.CreateDirectory(ExtractDirectory);
                File.WriteAllBytes(archiveDir, content);

                var hash = SHA256.HashData(content);
                if (ExpectedHash != null && !hash.SequenceEqual(Convert.FromHexString(ExpectedHash)))
                {
                    return false;
                }
                ZipArchive archive = new(new MemoryStream(content));
                archive.ExtractToDirectory(ExtractDirectory);
                return true;
            }
            return false;
        }


        /// <summary>
        /// A simplified reimplementation of CMake's configure_file. It supports
        /// the primary behaviors: variable substitution (@VAR@, ${VAR}, $CACHE{VAR}, $ENV{VAR}),
        /// #cmakedefine / #cmakedefine01 processing, and the options COPYONLY, ESCAPE_QUOTES, @ONLY,
        /// and NEWLINE_STYLE. This function takes a variables dictionary (case-insensitive by key).
        ///
        /// Note: This is not a byte-for-byte recreation of CMake; it implements the common
        /// transformations needed by ebuild's configuration flow.
        /// </summary>
        public static void CMakeConfigureFile(
            string inputFile,
            string outputFile,
            Dictionary<string, string>? variables = null,
            bool copyOnly = false,
            bool escapeQuotes = false,
            bool atOnly = false,
            string? newlineStyle = null)
        {
            if (copyOnly && newlineStyle != null)
                throw new ArgumentException("COPYONLY may not be used with NEWLINE_STYLE");

            variables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Resolve paths (basic handling: if output is a directory, place file there)
            var inputPath = Path.GetFullPath(inputFile);
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Input file not found", inputPath);

            string outputPath = outputFile;
            if (Directory.Exists(outputPath) || outputPath.EndsWith(Path.DirectorySeparatorChar.ToString()) || outputPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                Directory.CreateDirectory(outputPath);
                outputPath = Path.Combine(outputPath, Path.GetFileName(inputPath));
            }
            else
            {
                var outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);
            }

            if (copyOnly)
            {
                File.Copy(inputPath, outputPath, true);
                return;
            }

            // Read the input text (preserve as text)
            var content = File.ReadAllText(inputPath);

            static bool IsFalseish(string? value)
            {
                if (string.IsNullOrEmpty(value))
                    return false; // unset vs empty - treat empty as set in many cmake uses; keep simple
                switch (value.Trim().ToUpperInvariant())
                {
                    case "0":
                    case "FALSE":
                    case "OFF":
                    case "N":
                    case "NO":
                        return true;
                    default:
                        return false;
                }
            }

            // Replacement helper uses the provided variables dictionary and environment variables
            string ReplaceVariables(string input)
            {
                string result = input;

                // ${VAR}
                if (!atOnly)
                {
                    result = Regex.Replace(result, @"\$\{([A-Za-z_][A-Za-z0-9_]*)\}", m =>
                    {
                        var name = m.Groups[1].Value;
                        if (variables!.TryGetValue(name, out var val))
                        {
                            if (escapeQuotes) val = val.Replace("\"", "\\\"");
                            return val;
                        }
                        return string.Empty;
                    });

                    // $CACHE{VAR}
                    result = Regex.Replace(result, @"\$CACHE\{([A-Za-z_][A-Za-z0-9_]*)\}", m =>
                    {
                        var name = m.Groups[1].Value;
                        if (variables!.TryGetValue(name, out var val))
                        {
                            if (escapeQuotes) val = val.Replace("\"", "\\\"");
                            return val;
                        }
                        return string.Empty;
                    });

                    // $ENV{VAR}
                    result = Regex.Replace(result, @"\$ENV\{([A-Za-z_][A-Za-z0-9_]*)\}", m =>
                    {
                        var name = m.Groups[1].Value;
                        var ev = Environment.GetEnvironmentVariable(name) ?? string.Empty;
                        if (escapeQuotes) ev = ev.Replace("\"", "\\\"");
                        return ev;
                    });
                }

                // @VAR@
                result = Regex.Replace(result, @"@([A-Za-z_][A-Za-z0-9_]*)@", m =>
                {
                    var name = m.Groups[1].Value;
                    if (variables!.TryGetValue(name, out var val))
                    {
                        if (escapeQuotes) val = val.Replace("\"", "\\\"");
                        return val;
                    }
                    return string.Empty;
                });

                return result;
            }

            // Process lines for #cmakedefine and #cmakedefine01
            var lines = Regex.Split(content, "\r\n|\r|\n");
            for (int i = 0; i < lines.Length; ++i)
            {
                var line = lines[i];

                var m01 = CMakeDefine01Regex.Match(line);
                if (m01.Success)
                {
                    var name = m01.Groups[1].Value;
                    var tail = m01.Groups.Count >= 3 ? m01.Groups[2].Value : null;
                    var isSet = variables.TryGetValue(name, out var val) && !IsFalseish(val);
                    if (!string.IsNullOrEmpty(tail))
                    {
                        tail = ReplaceVariables(tail);
                        lines[i] = $"#define {name} " + (isSet ? tail + (tail.Length > 0 ? " " : "") + "1" : tail + (tail.Length > 0 ? " " : "") + "0");
                    }
                    else
                    {
                        lines[i] = $"#define {name} " + (isSet ? "1" : "0");
                    }
                    continue;
                }

                var m = CMakeDefineRegex.Match(line);
                if (m.Success)
                {
                    var name = m.Groups[1].Value;
                    var tail = m.Groups.Count >= 3 ? m.Groups[2].Value : null;
                    var isSet = variables.TryGetValue(name, out var val) && !IsFalseish(val);
                    if (isSet)
                    {
                        if (!string.IsNullOrEmpty(tail))
                        {
                            tail = ReplaceVariables(tail);
                            lines[i] = $"#define {name} {tail}";
                        }
                        else
                        {
                            lines[i] = $"#define {name}";
                        }
                    }
                    else
                    {
                        lines[i] = $"/* #undef {name} */";
                    }
                    continue;
                }

                // General variable substitution for the line
                lines[i] = ReplaceVariables(line);
            }

            var outContent = string.Join("\n", lines);

            if (newlineStyle != null)
            {
                var nl = newlineStyle.ToUpperInvariant() switch
                {
                    "UNIX" or "LF" => "\n",
                    "DOS" or "WIN32" or "CRLF" => "\r\n",
                    _ => Environment.NewLine,
                };
                outContent = Regex.Replace(outContent, "\r\n|\r|\n", nl);
            }

            // Write output; overwrite if exists
            File.WriteAllText(outputPath, outContent, new UTF8Encoding(false));
        }

        /// <summary>
        /// Regex used to detect CMake-style <c>#cmakedefine NAME [value]</c> lines. Captures the
        /// name in group 1 and an optional trailing value in group 2.
        /// </summary>
        public static readonly Regex CMakeDefineRegex = new(@"^#\s*cmakedefine\s+([A-Za-z_][A-Za-z0-9_]*)\b(?:\s+(.*))?$");

        /// <summary>
        /// Regex used to detect CMake-style <c>#cmakedefine01 NAME [value]</c> lines. Captures the
        /// name in group 1 and an optional trailing value in group 2. This variant is handled by
        /// emitting either <c>#define NAME 1</c> or <c>#define NAME 0</c> depending on whether the
        /// variable is set.
        /// </summary>
        public static readonly Regex CMakeDefine01Regex = new(@"^#\s*cmakedefine01\s+([A-Za-z_][A-Za-z0-9_]*)\b(?:\s+(.*))?$");
    }
}