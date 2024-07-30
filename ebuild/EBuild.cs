using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;


namespace ebuild;

public class EBuild
{
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
        var foundEBuildProj = FindEBuildProject();
        if (foundEBuildProj == null)
        {
            Console.WriteLine("Can't find ebuild.csproj");
            return;
        }

        var toLoadDllFile = CompileModuleFile(moduleTarget, foundEBuildProj!);
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
            EbuildLocation = Assembly.GetExecutingAssembly().Location
        };
        PlatformRegistry.LoadFromAssembly(Assembly.GetExecutingAssembly());
        CompilerRegistry.LoadFromAssembly(Assembly.GetExecutingAssembly());
        var createdModule = (Module)Activator.CreateInstance(loadedModuleType, [moduleContext])!;
        var compiler = CompilerRegistry.GetCompiler(createdModule);
        Console.WriteLine("The compiler for module {0} is {1}({2})", nameof(createdModule), compiler.GetName(),
            compiler.GetExecutablePath());
        compiler.SetCurrentTarget(createdModule);
        var targetWorkingDir = Path.Join(Directory.GetParent(moduleTarget)!.FullName, "Binaries");
        Directory.CreateDirectory(targetWorkingDir);
        Directory.SetCurrentDirectory(targetWorkingDir);
        compiler.Compile(moduleContext);
    }
}