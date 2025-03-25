using System.Diagnostics.CodeAnalysis;

namespace ebuild;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class ModuleMeta
{
    public List<string>? AdditionalCompilationFiles = new();
    public List<string>? AdditionalReferences = new();
}