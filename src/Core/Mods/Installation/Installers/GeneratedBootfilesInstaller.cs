using System.IO.Abstractions;
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

    private static readonly string BmtFilesWildcard =
        Path.Combine("vehicles", "_data", "effects", "backfire", "*.bmt");

    internal static readonly string[] SourceNames =
        [BootFlowPakFileName, BootSplashPakFileName, PhysicsPersistentPakFileName];

    private readonly Func<string, IFileInfo> resolveSource;

    public GeneratedBootfilesInstaller(string packageName, IDirectoryInfo tempDir,
        Func<string, IFileInfo> resolveSource) :
        base(CreateExtractionDirectory(tempDir), packageName, HashSources(resolveSource))
    {
        this.resolveSource = resolveSource;
    }

    private static IDirectoryInfo CreateExtractionDirectory(IDirectoryInfo tempDir) =>
        tempDir.CreateSubdirectory(Guid.NewGuid().ToString());

    private static int HashSources(Func<string, IFileInfo> resolveSource)
    {
        var sources = SelectSources(resolveSource);
        return FileSystemHash.Hash32(SourceNames.Select(name => sources[name]));
    }

    private static Dictionary<string, IFileInfo> SelectSources(Func<string, IFileInfo> resolveSource) =>
        SourceNames.ToDictionary(name => name, name =>
        {
            var relative = Path.Combine(PakfilesDirectory, name);
            var source = resolveSource(relative);
            return source.Exists
                ? source
                : throw new FileNotFoundException("Missing bootfiles generation source", source.FullName);
        });

    protected override void InstallAllFiles(InstallBody body)
    {
        GenerateBootfiles();
        base.InstallAllFiles(body);
    }

    protected override void InstallFile(RootedPath destinationPath, IFileInfo fileInfo)
    {
        fileInfo.MoveTo(destinationPath.Full, overwrite: true);
    }

    private void GenerateBootfiles()
    {
        var selectedSources = SelectSources(resolveSource);
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
