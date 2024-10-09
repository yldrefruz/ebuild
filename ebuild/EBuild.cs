using System.Diagnostics;
using System.Reflection;
using System.Text;


namespace ebuild;

public static class EBuild
{
    private static bool _generateCompileCommandsJson;
    private static bool _noCompile;
    private static bool _debug;
    private static string _additionalFlagsArg = "";
    private static bool _additionalFlags;

    public static string CompileModuleFile(string modulePath, string ebuildProjLocation)
    {
        var moduleDirectory = Directory.GetParent(modulePath)!.FullName;
        var localEBuildDirectory = Directory.CreateDirectory(Path.Join(moduleDirectory, ".ebuild"));
        var moduleName = Path.GetFileNameWithoutExtension(modulePath);
        var ebuildModuleIndex = moduleName.IndexOf(".ebuild_module", StringComparison.Ordinal);
        if (ebuildModuleIndex != -1)
            moduleName = moduleName.Remove(ebuildModuleIndex);
        var moduleProjectFileLocation = Path.Join(localEBuildDirectory.FullName, "module", moduleName + ".csproj");
        Directory.CreateDirectory(Directory.GetParent(moduleProjectFileLocation)!.FullName);
        var moduleProjectFile = File.Create(moduleProjectFileLocation);
        StreamWriter writer = new StreamWriter(moduleProjectFile);
        var moduleProjectContent = String.Format("""
                                                 <Project Sdk="Microsoft.NET.Sdk">
                                                     <PropertyGroup>
                                                         <OutputType>Library</OutputType>
                                                         <TargetFramework>net8.0</TargetFramework>
                                                         <ImplicitUsings>enable</ImplicitUsings>
                                                         <Nullable>enable</Nullable>
                                                     </PropertyGroup>
                                                     <ItemGroup>
                                                         <ProjectReference Include="{0}"/>
                                                     </ItemGroup>
                                                 </Project>
                                                 """, ebuildProjLocation);
        writer.Write(moduleProjectContent);
        writer.Close();
        writer.Dispose();
        moduleProjectFile.Close();
        moduleProjectFile.Dispose();
        File.Copy(modulePath,
            Path.Join(Directory.GetParent(moduleProjectFileLocation)!.FullName, moduleName + ".ebuild_module.cs"),
            true);
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "dotnet";
        psi.Arguments = $"build {moduleProjectFile.Name} --configuration Release";
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardError = true;
        psi.RedirectStandardOutput = true;
        psi.StandardErrorEncoding = psi.StandardOutputEncoding = Encoding.UTF8;
        psi.WorkingDirectory = Directory.GetParent(moduleProjectFileLocation)!.FullName;
        var p = new Process();
        p.StartInfo = psi;
        p.EnableRaisingEvents = true;
        p.Start();
        Console.WriteLine(p.StandardOutput.ReadToEnd());
        Console.WriteLine(p.StandardError.ReadToEnd());
        Console.Out.Flush();
        p.WaitForExit();
        var dllFile = Path.Join(Directory.GetParent(moduleProjectFileLocation)!.FullName, "bin", "Release", "net8.0",
            moduleName + ".dll");
        var toLoadDllFile = Path.Join(localEBuildDirectory.FullName, moduleName + ".dll");
        File.Copy(dllFile, toLoadDllFile, true);
        return toLoadDllFile;
    }

    public static string? FindEBuildProject()
    {
        var bFound = false;
        var currentDir = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
        while (!bFound && currentDir != null)
        {
            if (currentDir.GetFiles("ebuild.csproj").Length > 0)
                bFound = true;
            else
                currentDir = Directory.GetParent(currentDir.FullName);
        }

        return !bFound ? null : Path.Join(currentDir!.FullName, "ebuild.csproj");
    }

    public static void Main(string[] args)
    {
        var moduleTarget = args[0];
        for (int i = 0; i < args.Length; ++i)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-GenerateCompileCommands" when !_generateCompileCommandsJson:
                    Console.WriteLine(
                        "GenerateCompileCommands found, will create compile_commands.json at the moduleTarget's directory");
                    _generateCompileCommandsJson = true;
                    continue;
                case "-NoCompile" when !_noCompile:
                    Console.WriteLine("NoCompile found, will not compile");
                    _noCompile = true;
                    continue;
                case "-Debug" when !_debug:
                    _debug = true;
                    break;
                case "-AdditionalFlags":
                {
                    i++;
                    if (args.Length >= i) continue;
                    Console.WriteLine("Found additional flag " + args[i]);
                    _additionalFlags = true;
                    _additionalFlagsArg = args[i];
                    break;
                }
            }
        }


        var foundEBuildProj = FindEBuildProject();
        if (foundEBuildProj == null)
        {
            Console.WriteLine("Can't find ebuild.csproj");
            return;
        }

        var toLoadDllFile = CompileModuleFile(moduleTarget, foundEBuildProj);
        var loadedModuleAssembly = Assembly.LoadFile(toLoadDllFile);
        Type? loadedModuleType = null;
        foreach (var type in loadedModuleAssembly.GetTypes())
        {
            if (type.IsSubclassOf(typeof(Module)))
            {
                loadedModuleType = type;
            }
        }

        if (loadedModuleType == null)
        {
            Console.WriteLine("Can't find subclass of Module in provided file.");
            return;
        }

        var moduleContext = new ModuleContext()
        {
            ModuleFile = moduleTarget,
            ModuleDirectory = Directory.GetParent(moduleTarget)!.FullName,
            EbuildLocation = Assembly.GetExecutingAssembly().Location
        };
        PlatformRegistry.LoadFromAssembly(Assembly.GetExecutingAssembly());
        CompilerRegistry.LoadFromAssembly(Assembly.GetExecutingAssembly());
        var createdModule = (Module)Activator.CreateInstance(loadedModuleType, new object?[] { moduleContext })!;
        var compiler = CompilerRegistry.GetCompiler(createdModule);
        Console.WriteLine("The compiler for module {0} is {1}({2})", createdModule.Name, compiler.GetName(),
            compiler.GetExecutablePath());
        compiler.SetCurrentTarget(createdModule);
        var targetWorkingDir = Path.Join(Directory.GetParent(moduleTarget)!.FullName, "Binaries");
        Directory.CreateDirectory(targetWorkingDir);
        Directory.SetCurrentDirectory(targetWorkingDir);
        compiler.SetDebugBuild(_debug);
        if (_additionalFlags)
        {
            compiler.AdditionalFlags.AddRange(_additionalFlagsArg.Split(" "));
        }

        if (!_noCompile)
            compiler.Compile(moduleContext);
        if (_generateCompileCommandsJson)
            compiler.Generate("CompileCommandsJSON", moduleContext);
    }
}