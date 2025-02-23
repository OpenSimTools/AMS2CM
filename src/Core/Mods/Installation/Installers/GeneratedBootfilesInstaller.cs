using Core.Games;
using Core.Packages.Installation.Installers;
using Core.Utils;
using PCarsTools;
using PCarsTools.Encryption;

namespace Core.Mods.Installation.Installers;

internal class GeneratedBootfilesInstaller : BaseDirectoryInstaller
{
    internal const string VirtualPackageName = "__bootfiles_generated";
    internal const string PakfilesDirectory = "Pakfiles";
    internal const string BootFlowPakFileName = "BOOTFLOW.bff";
    internal const string BootSplashPakFileName = "BOOTSPLASH.bff";
    internal const string PhysicsPersistentPakFileName = "PHYSICSPERSISTENT.bff";

    private readonly string pakPath;
    private readonly string BmtFilesWildcard =
        Path.Combine("vehicles", "_data", "effects", "backfire", "*.bmt");

    public GeneratedBootfilesInstaller(ITempDir tempDir, BaseInstaller.IConfig config, IGame game) :
        base(VirtualPackageName, null, tempDir, config)
    {
        pakPath = Path.Combine(game.InstallationDirectory, PakfilesDirectory);
    }

    protected override DirectoryInfo Source => StagingDir;

    protected override void InstalAllFiles(InstallBody body)
    {
        GenerateBootfiles();
        base.InstalAllFiles(body);
    }

    protected override void InstallFile(RootedPath destinationPath, FileInfo fileInfo)
    {
        File.Move(fileInfo.FullName, destinationPath.Full);
    }

    #region Bootfiles Generation

    private void GenerateBootfiles()
    {
        ExtractPakFileFromGame(BootFlowPakFileName);
        ExtractPakFileFromGame(PhysicsPersistentPakFileName);
        CreateEmptyFile(ExtractedPakPath($"{PhysicsPersistentPakFileName}{BaseInstaller.RemoveFileSuffix}"));
        File.Copy(Path.Combine(pakPath, BootSplashPakFileName), ExtractedPakPath(BootFlowPakFileName));
        DeleteFromExtractedFiles(BmtFilesWildcard);
    }

    private void ExtractPakFileFromGame(string fileName)
    {
        var filePath = Path.Combine(pakPath, fileName);
        BPakFileEncryption.SetKeyset(KeysetType.PC2AndAbove);
        using var pakFile = BPakFile.FromFile(filePath, withExtraInfo: true, outputWriter: TextWriter.Null);
        pakFile.UnpackAll(StagingDir.FullName);
    }

    private string ExtractedPakPath(string name) =>
        Path.Combine(StagingDir.FullName, PakfilesDirectory, name);

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
        foreach (var file in Directory.EnumerateFiles(StagingDir.FullName, wildcardRelative))
        {
            File.Delete(file);
        }
    }

    #endregion
}
