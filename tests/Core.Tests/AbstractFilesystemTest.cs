namespace Core.Tests;

[IntegrationTest]
public abstract class AbstractFilesystemTest : IDisposable
{
    protected readonly DirectoryInfo testDir;

    protected AbstractFilesystemTest()
    {
        testDir = Directory.CreateTempSubdirectory(GetType().Name);
    }

    public void Dispose()
    {
        testDir.Delete(recursive: true);
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

    protected string TestPath(string relativePath) =>
        Path.Combine(testDir.FullName, relativePath);

    protected static FileInfo CreateFile(string fullPath, string content = "")
    {
        var parentDirFullPath = Path.GetDirectoryName(fullPath);
        if (parentDirFullPath is not null)
        {
            Directory.CreateDirectory(parentDirFullPath);
        }
        File.WriteAllText(fullPath, content);
        return new FileInfo(fullPath);
    }
}
