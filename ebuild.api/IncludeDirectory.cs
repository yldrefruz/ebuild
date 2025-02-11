namespace ebuild.api;

public class IncludeDirectory(string directory)
{
    public readonly string Directory = directory;
    DirectoryInfo ToDirectoryInfo() => new DirectoryInfo(directory);
}