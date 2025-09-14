using System.Diagnostics.CodeAnalysis;

namespace ebuild;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class ModuleMeta
{
    public List<string>? AdditionalCompilationFiles = new();
    public List<string>? AdditionalReferences = new();



    public string GetAdditionalReferenceNodes(string moduleProjectFileDir, string directory)
    {
        return AdditionalReferences?
            .Aggregate(
                (current, f) =>
                    {
                        return current + $"<ReferenceInclude=\"{Path.GetRelativePath(moduleProjectFileDir, Path.GetRelativePath(directory, f))}\"/>\n";
                    }
            )
            ?? string.Empty;
    }


    public string GetAdditionalCompileNodes(string moduleProjectFileDir, string directory)
    {
        return AdditionalCompilationFiles?
                .Aggregate(
                    (current, f) =>
                        {
                            return current + $"<Compile Include=\"{Path.GetRelativePath(moduleProjectFileDir, Path.GetRelativePath(directory, f))}\"/>\n";
                        }
                    )
                    ?? string.Empty;
    }
}