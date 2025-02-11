// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ClassNeverInstantiated.Global

using System.Runtime.InteropServices;

namespace ebuild.api;

public class ModuleContext(FileInfo moduleFile, string buildType, PlatformBase platform, string compilerName,
    FileInfo? outputBinary, Architecture? targetArchitecture = null)
{
    public class Message(string value, Message.SeverityTypes type)
    {
        public enum SeverityTypes
        {
            Info,
            Warning,
            Error,
            Fatal
        }

        public SeverityTypes GetSeverity() => type;
        public string GetMessage() => value;

        public override string ToString()
        {
            return type + " : " + value;
        }
    }

    public DirectoryInfo? ModuleDirectory => ModuleFile.Directory;

    public FileInfo ModuleFile { get; } = moduleFile;
    public FileInfo? OutputBinary = outputBinary;
    public string BuildType = buildType;

    public PlatformBase Platform = platform;
    public string CompilerName = compilerName;
    public List<ModuleBase> DependantModules = new();
    public Architecture TargetArchitecture = targetArchitecture ?? RuntimeInformation.OSArchitecture;


    public List<Message> Messages = new();


    public void AddMessage(Message.SeverityTypes severityType, string message)
    {
        Messages.Add(new Message(message, severityType));
    }
}