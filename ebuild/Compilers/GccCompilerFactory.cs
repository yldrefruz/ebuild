using ebuild.api;
using ebuild.api.Compiler;

namespace ebuild.Compilers
{
    public class GccCompilerFactory : ICompilerFactory
    {
        public string Name => "gcc";

        public Type CompilerType => typeof(GccCompiler);

        public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            // GCC is available if it exists on PATH (works on Windows with MinGW/Cygwin too)
            return FindExecutable("gcc") != null;
        }

        public CompilerBase CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new GccCompiler(instancingParams.Architecture);
        }

        private static string? FindExecutable(string executableName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
                return null;

            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path, executableName);
                if (File.Exists(fullPath))
                    return fullPath;
                
                // Try with .exe extension on Windows
                var exePath = fullPath + ".exe";
                if (File.Exists(exePath))
                    return exePath;
            }
            return null;
        }
    }
}