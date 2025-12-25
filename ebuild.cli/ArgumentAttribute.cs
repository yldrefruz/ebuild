using System;

namespace ebuild.cli
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ArgumentAttribute : Attribute
    {
        public int Order { get; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool AllowMultiple { get; set; } = false;
        public bool IsRequired { get; set; } = false;

        public ArgumentAttribute(int order)
        {
            Order = order;
        }
    }
}
