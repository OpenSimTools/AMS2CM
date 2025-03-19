using Core.Utils;

namespace Core.Tests.Base;

[IntegrationTest]
public abstract class AbstractFilesystemTest : IDisposable
{
    protected readonly DirectoryInfo TestDir;

    protected AbstractFilesystemTest()
    {
        TestDir = Directory.CreateTempSubdirectory(GetType().Name);
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

    protected FileInfo CreateTestFile(string relativePath, string content = "") =>
        CreateFile(TestPath(relativePath), content);

    protected RootedPath TestPath(string relativePath) => new(TestDir.FullName, relativePath);

    protected static FileInfo CreateFile(RootedPath path, string content = "")
    {
        var parentDirFullPath = Path.GetDirectoryName(path.Full);
        if (parentDirFullPath is not null)
        {
            Directory.CreateDirectory(parentDirFullPath);
        }
        File.WriteAllText(path.Full, content);
        return new FileInfo(path.Full);
    }
}
