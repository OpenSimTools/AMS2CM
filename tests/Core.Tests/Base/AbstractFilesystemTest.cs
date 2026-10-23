using Core.Utils;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace Core.Tests.Base;

public abstract class AbstractFilesystemTest : IDisposable
{
    protected readonly MockFileSystem FileSystem = new();
    protected readonly IDirectoryInfo TestDir;

    protected AbstractFilesystemTest()
    {
        TestDir = FileSystem.Directory.CreateDirectory(GetType().Name);
    }

    public void Dispose()
    {
        TestDir.Delete(recursive: true);
    }

    protected void CreateTestFiles(params string[] relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            CreateTestFile(relativePath);
        }
    }

    protected IFileInfo CreateTestFile(string relativePath, string content = "") =>
        CreateFile(TestPath(relativePath), content);

    protected RootedPath TestPath(string relativePath) => new RootPath(TestDir.FullName).Combine(relativePath);

    protected IFileInfo CreateFile(RootedPath path, string content = "")
    {
        var parentDirFullPath = Path.GetDirectoryName(path.Full);
        if (parentDirFullPath is not null)
        {
            FileSystem.Directory.CreateDirectory(parentDirFullPath);
        }

        FileSystem.File.WriteAllText(path.Full, content);
        return FileSystem.FileInfo.New(path.Full);
    }
}
