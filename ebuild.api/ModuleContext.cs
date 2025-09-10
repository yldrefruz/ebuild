using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ebuild.api;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class ModuleContext
{
    public ModuleContext(ModuleContext m)
    {
        SelfReference = m.SelfReference;
        Platform = m.Platform;
        Compiler = m.Compiler;
        TargetArchitecture = m.TargetArchitecture;
        Options = m.Options;
        AdditionalDependencyPaths = m.AdditionalDependencyPaths;
    }

    public ModuleContext(ModuleReference reference, string platform, string compiler)
    {
        SelfReference = reference;
        Platform = platform;
        Compiler = compiler;
    }

    public ModuleReference SelfReference;
    public string Platform;
    public string Compiler;
    public Architecture TargetArchitecture = RuntimeInformation.OSArchitecture;
    public Dictionary<string, string> Options = [];
    public List<string> AdditionalDependencyPaths = [];
    public IModuleInstancingParams? InstancingParams;


    public FileInfo ModuleFile => new(SelfReference.GetFilePath());
    public DirectoryInfo? ModuleDirectory => ModuleFile.Directory;
    public string Configuration = "debug";
    public string RequestedVersion => SelfReference.GetVersion();
    public string RequestedOutput => SelfReference.GetOutput();


    public class Message(string value, Message.SeverityTypes type)
    {
        public enum SeverityTypes
        {
            Info,
            Warning,
            Error
        }

        public SeverityTypes GetSeverity() => type;
        public string GetMessage() => value;

        public override string ToString()
        {
            return type + " : " + value;
        }
    }

    public List<Message> Messages = [];


    public void AddMessage(Message.SeverityTypes severityType, string message)
    {
        Messages.Add(new Message(message, severityType));
    }
}