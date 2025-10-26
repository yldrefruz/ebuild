using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ebuild.api.Toolchain;

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
        /// <summary>
        /// Returns the path to the cache directory for the specified module instance. The cache
        /// directory is located under <c>.ebuild/{moduleName}/cache/{variantId}</c> relative to the
        /// module directory.
        /// </summary>
        /// <param name="module">The module instance for which to get the cache directory.</param>
        /// <returns>The full path to the module's cache directory.</returns>
        public static string GetModuleCacheDir(ModuleBase module)
        {
            return Path.Join(module.Context.ModuleDirectory!.FullName, ".ebuild",
                            ((IModuleFile)module.Context.SelfReference).GetName(), "cache",
                            module.GetVariantId().ToString());
        }

        /// <summary>
        /// Checks whether a symbol exists as a function, variable, or preprocessor macro by
        /// generating and compiling a test source file. This is inspired by CMake's
        /// check_symbol_exists and check_cxx_symbol_exists commands.
        /// 
        /// The check generates a minimal source file that includes the specified headers and
        /// references the symbol. If the header files define the symbol as a macro, it is considered
        /// available and assumed to work. If the symbol is declared as a function or variable,
        /// the check ensures that it compiles and links successfully.
        /// 
        /// Note: This function does not detect types, enum values, or C++ templates. For those,
        /// consider using other approaches. For C++ overloaded functions, this check may be unreliable.
        /// </summary>
        /// <param name="module">The module for which the compiler will be created.</param>
        /// <param name="symbol">The symbol to check for (e.g., "fopen", "SEEK_SET", "std::fopen").</param>
        /// <param name="headers">One or more header files to include, separated by semicolons (e.g., "stdio.h" or "cstdio;iostream").</param>
        /// <param name="additionalCompilerFlags">Optional additional compiler flags to pass (e.g., include paths, definitions).</param>
        /// <param name="isCpp">If true, generates a C++ source file (.cpp); otherwise generates a C source file (.c). Default is false (C).</param>
        /// <returns>True if the symbol exists and can be compiled/linked; false otherwise.</returns>
        public static async Task<bool> CheckSymbolExists(
            ModuleBase module,
            string symbol,
            string headers,
            string? additionalCompilerFlags = null,
            bool isCpp = true)
        {
            var cacheDir = GetModuleCacheDir(module);
            Dictionary<string, bool> cachedValues;
            if (File.Exists(Path.Combine(cacheDir, "check_symbol.results")))
            {
                var lines = File.ReadAllLines(Path.Combine(cacheDir, "check_symbol.results"));
                // process the results. File looks somewhat like an ini file
                cachedValues = lines.Select(line => line.Split('=')).ToDictionary(parts => parts[0].Trim(), parts => Boolean.Parse(parts[1].Trim()));
            }
            else
            {
                cachedValues = [];
            }

            if (cachedValues.TryGetValue(symbol, out var cachedResult))
            {
                return cachedResult;
            }
            Directory.CreateDirectory(cacheDir);
            // Create a temporary directory for the test
            var tempDir = Path.Combine(Path.GetTempPath(), "ebuild_symbol_check_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Parse headers (semicolon-separated list)
                var headerList = headers.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                // Generate the test source file
                var extension = isCpp ? "cpp" : "c";
                var sourceFile = Path.Combine(tempDir, $"check_symbol.{extension}");
                var objectFile = Path.Combine(tempDir, $"check_symbol.obj");

                var sourceBuilder = new StringBuilder();

                // Include all headers
                foreach (var header in headerList)
                {
                    sourceBuilder.AppendLine($"#include <{header}>");
                }

                sourceBuilder.AppendLine();

                // Check if symbol is a macro
                sourceBuilder.AppendLine("#ifndef " + symbol);

                sourceBuilder.AppendLine("  // Symbol is not a macro, try to use it as a function or variable");
                sourceBuilder.AppendLine("  int main() {");
                sourceBuilder.AppendLine("      // Reference the symbol to ensure it exists and links");
                sourceBuilder.AppendLine("      (void)" + symbol + ";");
                sourceBuilder.AppendLine("    return 0;");
                sourceBuilder.AppendLine("  }");
                sourceBuilder.AppendLine("#else");
                sourceBuilder.AppendLine("  // Symbol is a macro");
                sourceBuilder.AppendLine("  int main() { return 0; }");
                sourceBuilder.AppendLine("#endif");


                File.WriteAllText(sourceFile, sourceBuilder.ToString());

                // Prepare compiler command
                var compilerArgs = new List<string>();

                // Add additional flags if provided
                if (!string.IsNullOrWhiteSpace(additionalCompilerFlags))
                {
                    compilerArgs.Add(additionalCompilerFlags);
                }

                // Add source file and output object file
                compilerArgs.Add($"\"{sourceFile}\"");
                var compilerExecutable = module.Context.Toolchain.GetCompilerFactory(module, module.Context.InstancingParams!)?.GetExecutablePath(module, module.Context.InstancingParams!);
                if (compilerExecutable == null)
                {
                    return false;
                }
                // Platform-specific compilation flags
                if (compilerExecutable.Contains("cl.exe", StringComparison.OrdinalIgnoreCase) ||
                    compilerExecutable.Contains("cl", StringComparison.OrdinalIgnoreCase))
                {
                    // MSVC: compile only, no linking
                    compilerArgs.Add($"/c");
                    compilerArgs.Add($"/Fo\"{objectFile}\"");
                    compilerArgs.Add("/nologo");
                }
                else
                {
                    // GCC/Clang: compile only
                    compilerArgs.Add("-c");
                    compilerArgs.Add($"-o");
                    compilerArgs.Add($"\"{objectFile}\"");
                }

                var arguments = string.Join(" ", compilerArgs);

                // Run the compiler
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = compilerExecutable,
                    Arguments = arguments,
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                // read output to prevent deadlocks
                if (process == null)
                {
                    return false;
                }
                _ = process.StandardOutput.ReadToEndAsync();
                _ = process.StandardError.ReadToEndAsync();

                process.WaitForExit();

                // Check if compilation succeeded
                cachedValues.Add(symbol, process.ExitCode == 0 && File.Exists(objectFile));
            }
            catch
            {
                // An error occurred during the check
                return false;
            }
            finally
            {
                // Write to the cache file
                var resultLines = cachedValues.Select(kv => $"{kv.Key}={kv.Value}");
                File.WriteAllLines(Path.Combine(cacheDir, "check_symbol.results"), resultLines);
                // Clean up temporary directory
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            return cachedValues[symbol];
        }

        /// <summary>
        /// Checks whether one or more header files exist and can be included together by
        /// generating and compiling a test source file. This is inspired by CMake's
        /// check_include_files command.
        /// 
        /// The check generates a minimal source file that includes the specified headers in order.
        /// If all headers can be included successfully and the source file compiles, the check
        /// succeeds. This is useful for checking if headers exist and are compatible when included
        /// together (e.g., some headers on Darwin/BSD require other headers to be included first).
        /// </summary>
        /// <param name="module">The module for which the compiler will be created.</param>
        /// <param name="includes">One or more header files to include, separated by semicolons (e.g., "stdio.h" or "sys/socket.h;net/if.h"). Headers are included in the order specified.</param>
        /// <param name="additionalCompilerFlags">Optional additional compiler flags to pass (e.g., include paths, definitions).</param>
        /// <param name="isCpp">If true, generates a C++ source file (.cpp) and uses the C++ compiler; otherwise generates a C source file (.c) and uses the C compiler. Default is true (C++).</param>
        /// <returns>True if all headers exist and can be included together successfully; false otherwise.</returns>
        public static async Task<bool> CheckIncludeFilesExists(
            ModuleBase module,
            string includes,
            string? additionalCompilerFlags = null,
            bool isCpp = true)
        {
            var cacheDir = GetModuleCacheDir(module);
            Dictionary<string, bool> cachedValues;
            if (File.Exists(Path.Combine(cacheDir, "check_include_files.results")))
            {
                var lines = File.ReadAllLines(Path.Combine(cacheDir, "check_include_files.results"));
                // process the results. File looks somewhat like an ini file
                cachedValues = lines.Select(line => line.Split('=')).ToDictionary(parts => parts[0].Trim(), parts => Boolean.Parse(parts[1].Trim()));
            }
            else
            {
                cachedValues = [];
            }

            if (cachedValues.TryGetValue(includes, out var cachedResult))
            {
                return cachedResult;
            }
            Directory.CreateDirectory(cacheDir);
            // Create a temporary directory for the test
            var tempDir = Path.Combine(Path.GetTempPath(), "ebuild_include_check_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Parse headers (semicolon-separated list)
                var headerList = includes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                // Generate the test source file
                var extension = isCpp ? "cpp" : "c";
                var sourceFile = Path.Combine(tempDir, $"check_include_files.{extension}");
                var objectFile = Path.Combine(tempDir, $"check_include_files.obj");

                var sourceBuilder = new StringBuilder();

                // Include all headers in order
                foreach (var header in headerList)
                {
                    sourceBuilder.AppendLine($"#include <{header}>");
                }

                sourceBuilder.AppendLine();
                sourceBuilder.AppendLine("int main() { return 0; }");

                File.WriteAllText(sourceFile, sourceBuilder.ToString());

                // Prepare compiler command
                var compilerArgs = new List<string>();

                // Add additional flags if provided
                if (!string.IsNullOrWhiteSpace(additionalCompilerFlags))
                {
                    compilerArgs.Add(additionalCompilerFlags);
                }

                // Add source file and output object file
                compilerArgs.Add($"\"{sourceFile}\"");
                var compilerExecutable = module.Context.Toolchain.GetCompilerFactory(module, module.Context.InstancingParams!)?.GetExecutablePath(module, module.Context.InstancingParams!);
                if (compilerExecutable == null)
                {
                    return false;
                }
                // Platform-specific compilation flags
                if (compilerExecutable.Contains("cl.exe", StringComparison.OrdinalIgnoreCase) ||
                    compilerExecutable.Contains("cl", StringComparison.OrdinalIgnoreCase))
                {
                    // MSVC: compile only, no linking
                    compilerArgs.Add($"/c");
                    compilerArgs.Add($"/Fo\"{objectFile}\"");
                    compilerArgs.Add("/nologo");
                }
                else
                {
                    // GCC/Clang: compile only
                    compilerArgs.Add("-c");
                    compilerArgs.Add($"-o");
                    compilerArgs.Add($"\"{objectFile}\"");
                }

                var arguments = string.Join(" ", compilerArgs);

                // Run the compiler
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = compilerExecutable,
                    Arguments = arguments,
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                // read output to prevent deadlocks
                if (process == null)
                {
                    return false;
                }
                _ = process.StandardOutput.ReadToEndAsync();
                _ = process.StandardError.ReadToEndAsync();

                process.WaitForExit();

                // Check if compilation succeeded
                cachedValues.Add(includes, process.ExitCode == 0 && File.Exists(objectFile));
            }
            catch
            {
                // An error occurred during the check
                return false;
            }
            finally
            {
                // Write to the cache file
                var resultLines = cachedValues.Select(kv => $"{kv.Key}={kv.Value}");
                File.WriteAllLines(Path.Combine(cacheDir, "check_include_files.results"), resultLines);
                // Clean up temporary directory
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            return cachedValues[includes];
        }


    }
}