using System;

namespace ebuild.cli
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CommandAttribute : Attribute
    {
        public string Name { get; }
        public string[]? Aliases { get; set; }
        public string? Description { get; set; }
        public bool AutoRegister { get; set; } = true;

        public CommandAttribute(string name)
        {
            Name = name;
        }
    }
}
