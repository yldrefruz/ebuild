using System.Runtime.InteropServices;

namespace ebuild.api.Linker
{



    public class LinkerSettings
    {
        public required string[] InputFiles;
        public required string OutputFile;
        public required ModuleType OutputType;
        public required Architecture TargetArchitecture;
        public required string IntermediateDir;
        public string[] LibraryPaths = [];
        public string[] LinkerFlags = [];
        public bool ShouldCreateDebugFiles = false;
        public bool IsDebugBuild = false;
        public string[] DelayLoadLibraries = [];
    
    }
}