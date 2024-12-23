namespace ebuild.api;

public class IncludeDirectory(string Directory)
{
    DirectoryInfo ToDirectoryInfo() => new DirectoryInfo(Directory);
}