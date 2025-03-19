namespace Core.Utils;

public class SubdirectoryTempDir : ITempDir
{
    private const string TempDirName = "Temp";

    public SubdirectoryTempDir(string parentPath)
    {
        BasePath = Path.Combine(parentPath, TempDirName);
    }

    public string BasePath
    {
        get;
    }

    public void Cleanup()
    {
        if (Directory.Exists(BasePath))
        {
            Directory.Delete(BasePath, recursive: true);
        }
    }
}
