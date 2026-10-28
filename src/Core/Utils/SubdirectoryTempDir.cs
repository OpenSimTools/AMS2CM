using System.IO.Abstractions;

namespace Core.Utils;

public class SubdirectoryTempDir : ITempDir
{
    private const string TempDirName = "Temp";

    public SubdirectoryTempDir(IDirectoryInfo parentPath)
    {
        BaseDirectory = parentPath.FileSystem.DirectoryInfo.New(Path.Combine(parentPath.FullName, TempDirName));
    }

    public IDirectoryInfo BaseDirectory { get; }

    public void Cleanup()
    {
        if (BaseDirectory.FileSystem.Directory.Exists(BaseDirectory.FullName))
        {
            BaseDirectory.FileSystem.Directory.Delete(BaseDirectory.FullName, recursive: true);
        }
    }
}
