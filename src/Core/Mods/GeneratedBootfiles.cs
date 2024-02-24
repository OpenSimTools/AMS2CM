using PCarsTools;
using PCarsTools.Encryption;

namespace Core.Mods;

internal class GeneratedBootfiles : ExtractedMod
{
    internal const string VirtualPackageName = "__bootfiles_generated";
    internal const string PakfilesDirectory = "Pakfiles";
    internal const string BootFlowPakFileName = "BOOTFLOW.bff";
    internal const string BootSplashPakFileName = "BOOTSPLASH.bff";
    internal const string PhysicsPersistentPakFileName = "PHYSICSPERSISTENT.bff";

    private readonly string pakPath;
    private readonly string BmtFilesWildcard =
        Path.Combine("vehicles", "_data", "effects", "backfire", "*.bmt");

    public GeneratedBootfiles(string gamePath, string generationBasePath)
        : base(VirtualPackageName, null, Path.Combine(generationBasePath, VirtualPackageName))
    {
        pakPath = Path.Combine(gamePath, PakfilesDirectory);
        GenerateBootfiles();
    }

    private void GenerateBootfiles()
    {
        ExtractPakFileFromGame(BootFlowPakFileName);
        ExtractPakFileFromGame(PhysicsPersistentPakFileName);
        CreateEmptyFile(ExtractedPakPath($"{PhysicsPersistentPakFileName}{JsgmeFileInstaller.RemoveFileSuffix}"));
        File.Copy(Path.Combine(pakPath, BootSplashPakFileName), ExtractedPakPath(BootFlowPakFileName));
        DeleteFromExtractedFiles(BmtFilesWildcard);
    }

    private void ExtractPakFileFromGame(string fileName)
    {
        var filePath = Path.Combine(pakPath, fileName);
        BPakFileEncryption.SetKeyset(KeysetType.PC2AndAbove);
        using var pakFile = BPakFile.FromFile(filePath, withExtraInfo: true, outputWriter: TextWriter.Null);
        pakFile.UnpackAll(extractedPath);
    }

    private string ExtractedPakPath(string name) =>
        Path.Combine(extractedPath, PakfilesDirectory, name);

    private void CreateEmptyFile(string path)
    {
        CreateParentDirectory(path);
        File.Create(path).Close();
    }

    private void CreateParentDirectory(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (parent is not null)
            Directory.CreateDirectory(parent);
    }

    private void DeleteFromExtractedFiles(string wildcardRelative)
    {
        foreach (var file in Directory.EnumerateFiles(extractedPath, wildcardRelative))
        {
            File.Delete(file);
        }
    }

    protected override IEnumerable<string> ExtractedRootDirs() => new[] { extractedPath };

    protected override ConfigEntries GenerateConfig() => ConfigEntries.Empty;
}
