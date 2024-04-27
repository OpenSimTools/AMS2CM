namespace Core;

public class ModSubdirectoryTempDir : ITempDir
{
    private const string TempDirName = "Temp";

    public ModSubdirectoryTempDir(string modsDir)
    {
        BasePath = Path.Combine(modsDir, TempDirName);
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