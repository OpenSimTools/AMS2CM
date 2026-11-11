using System.IO.Abstractions;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using PCarsTools;
using PCarsTools.Encryption;

namespace Core.Mods.Installation.Installers;

internal class GeneratedBootfilesInstaller : BaseDirectoryInstaller
{
    internal const string PakfilesDirectory = "Pakfiles";
    internal const string BootFlowPakFileName = "BOOTFLOW.bff";
    internal const string BootSplashPakFileName = "BOOTSPLASH.bff";
    internal const string PhysicsPersistentPakFileName = "PHYSICSPERSISTENT.bff";

    private static readonly string[] SourceNames =
        [BootFlowPakFileName, BootSplashPakFileName, PhysicsPersistentPakFileName];

    private readonly IDirectoryInfo gameInstallationDirectory;
    private readonly IReadOnlyDictionary<string, PackageInstallationState> state;
    private readonly MoveFileBackupStrategy.IBackupFileNaming backupFileNaming;

    private readonly string BmtFilesWildcard =
        Path.Combine("vehicles", "_data", "effects", "backfire", "*.bmt");

    /// <remarks>The state must reflect live ownership, updated in place throughout reconciliation.</remarks>
    public GeneratedBootfilesInstaller(string packageName, IDirectoryInfo gameInstallationDirectory,
        IDirectoryInfo tempDir, IReadOnlyDictionary<string, PackageInstallationState> state,
        MoveFileBackupStrategy.IBackupFileNaming backupFileNaming) :
        base(CreateExtractionDirectory(tempDir), packageName,
            HashSources(gameInstallationDirectory, state, backupFileNaming))
    {
        this.gameInstallationDirectory = gameInstallationDirectory;
        this.state = state;
        this.backupFileNaming = backupFileNaming;
    }

    private static IDirectoryInfo CreateExtractionDirectory(IDirectoryInfo tempDir) =>
        tempDir.CreateSubdirectory(Guid.NewGuid().ToString());

    private static int HashSources(IDirectoryInfo gameInstallationDirectory,
        IReadOnlyDictionary<string, PackageInstallationState> state,
        MoveFileBackupStrategy.IBackupFileNaming backupFileNaming)
    {
        var sources = SelectSources(gameInstallationDirectory, state, backupFileNaming);
        return FileSystemHash.Hash32(SourceNames.Select(name => sources[name]));
    }

    private static Dictionary<string, IFileInfo> SelectSources(IDirectoryInfo gameInstallationDirectory,
        IReadOnlyDictionary<string, PackageInstallationState> state,
        MoveFileBackupStrategy.IBackupFileNaming backupFileNaming)
    {
        return SourceNames.ToDictionary(name => name, name =>
        {
            var relative = Path.Combine(PakfilesDirectory, name);
            var live = gameInstallationDirectory.FileSystem.FileInfo.New(
                Path.Combine(gameInstallationDirectory.FullName, relative));
            var owner = state.Values.FirstOrDefault(s => s.Files.Any(p =>
                StringComparer.OrdinalIgnoreCase.Equals(Paths.NormalizeDirectorySeparators(p), relative)));
            var source = owner is not null && (!live.Exists || live.CreationTimeUtc <= owner.Time.UtcDateTime)
                ? gameInstallationDirectory.FileSystem.FileInfo.New(backupFileNaming.ToBackup(live.FullName)) : live;
            return source.Exists ? source
                : throw new FileNotFoundException("Missing bootfiles generation source", source.FullName);
        });
    }

    protected override void InstallAllFiles(InstallBody body)
    {
        GenerateBootfiles(SelectSources(gameInstallationDirectory, state, backupFileNaming));
        base.InstallAllFiles(body);
    }

    protected override void InstallFile(RootedPath destinationPath, IFileInfo fileInfo)
    {
        fileInfo.MoveTo(destinationPath.Full, overwrite: true);
    }

    private void GenerateBootfiles(IReadOnlyDictionary<string, IFileInfo> selectedSources)
    {
        ExtractPakFileFromGame(selectedSources, BootFlowPakFileName);
        ExtractPakFileFromGame(selectedSources, PhysicsPersistentPakFileName);
        CreateEmptyFile(ExtractedPakPath($"{PhysicsPersistentPakFileName}{BaseInstaller.RemoveFileSuffix}"));
        FileSystem.File.Copy(SourceFile(selectedSources, BootSplashPakFileName), ExtractedPakPath(BootFlowPakFileName));
        DeleteFromExtractedFiles(BmtFilesWildcard);
    }

    private void ExtractPakFileFromGame(IReadOnlyDictionary<string, IFileInfo> selectedSources, string fileName)
    {
        var filePath = SourceFile(selectedSources, fileName);
        BPakFileEncryption.SetKeyset(KeysetType.PC2AndAbove);
        using var pakFile = BPakFile.FromFile(filePath, withExtraInfo: true, outputWriter: TextWriter.Null);
        pakFile.UnpackAll(Source.FullName);
    }

    private static string SourceFile(IReadOnlyDictionary<string, IFileInfo> selectedSources, string name) =>
        selectedSources[name].FullName;

    private string ExtractedPakPath(string name) =>
        Path.Combine(Source.FullName, PakfilesDirectory, name);

    private void CreateEmptyFile(string path)
    {
        CreateParentDirectory(path);
        FileSystem.File.WriteAllBytes(path, Array.Empty<byte>());
    }

    private void CreateParentDirectory(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (parent is not null)
            FileSystem.Directory.CreateDirectory(parent);
    }

    private void DeleteFromExtractedFiles(string wildcardRelative)
    {
        foreach (var file in FileSystem.Directory.EnumerateFiles(Source.FullName, wildcardRelative))
        {
            FileSystem.File.Delete(file);
        }
    }
}
