﻿using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ebuild.api;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class ModuleContext
{
    public required ModuleReference SelfReference;
    public required string Platform;
    public required string Compiler;
    public Architecture TargetArchitecture = RuntimeInformation.OSArchitecture;
    public Dictionary<string, string> Options = new();
    public List<string> AdditionalDependencyPaths = new();


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

    public List<Message> Messages = new();


    public void AddMessage(Message.SeverityTypes severityType, string message)
    {
        Messages.Add(new Message(message, severityType));
    }
}