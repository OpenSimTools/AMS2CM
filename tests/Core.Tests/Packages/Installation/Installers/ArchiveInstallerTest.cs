using System.IO.Abstractions;
using System.IO.Compression;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation.Installers;

[IntegrationTest]
public class ArchiveInstallerTest : IDisposable
{
    #region Setup

    private readonly FileSystem fs = new();
    private readonly IDirectoryInfo testDirectory;
    private readonly string destinationRoot;
    private readonly Mock<IBackupStrategy> backupStrategy = new();

    public ArchiveInstallerTest()
    {
        testDirectory = fs.Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), $"{nameof(ArchiveInstallerTest)}-{Guid.NewGuid():N}"));
        destinationRoot = testDirectory.CreateSubdirectory("Destination").FullName;
    }

    public void Dispose()
    {
        testDirectory.Delete(recursive: true);
        GC.SuppressFinalize(this);
    }

    #endregion

    [Fact]
    public void RelativeDirectoryPaths_ReturnsDistinctFileParents()
    {
        var archive = CreateArchive(
            ("a", ""),
            ("nested/b", ""),
            ("nested/c", ""),
            ("other/d", ""),
            ("other/deep/e", ""));
        var installer = new ArchiveInstaller("Package", 1, archive);

        installer.RelativeDirectoryPaths.Should().BeEquivalentTo(
            "", "nested", "other", Path.Combine("other", "deep"));
    }

    [Fact]
    public void RelativeDirectoryPaths_IgnoresDirectoryEntries()
    {
        var archive = CreateArchive(
            ("empty/", ""));
        var installer = new ArchiveInstaller("Package", 1, archive);

        installer.RelativeDirectoryPaths.Should().BeEmpty();
    }

    [Fact]
    public void Install_ExtractsFiles()
    {
        var archiveFile = Path.Combine("nested", "file");
        var archive = CreateArchive(
            (archiveFile, "New"));

        var installer = new ArchiveInstaller("Package", 1, archive);

        installer.Install(path => new RootPath(destinationRoot).Combine(path),
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        fs.File.ReadAllText(Path.Combine(destinationRoot, archiveFile)).Should().Be("New");
        installer.InstalledFiles.Select(path => path.Relative).Should().BeEquivalentTo(Path.Combine(archiveFile));
    }

    [Fact]
    public void Install_ReplacesExistingDestination()
    {
        var destinationFile = Path.Combine(destinationRoot, "existing");
        fs.File.WriteAllText(destinationFile, "Old");
        var archive = CreateArchive(
            ("existing", "New"));

        var installer = new ArchiveInstaller("Package", 1, archive);

        installer.Install(path => new RootPath(destinationRoot).Combine(path),
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        fs.File.ReadAllText(destinationFile).Should().Be("New");
        installer.InstalledFiles.Select(path => path.Relative).Should().BeEquivalentTo("existing");
    }

    [Fact]
    public void Install_IgnoresDirectories()
    {
        var archive = CreateArchive(
            ("empty/", ""));
        var installer = new ArchiveInstaller("Package", 1, archive);

        installer.Install(path => new RootPath(destinationRoot).Combine(path),
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        fs.Directory.Exists(Path.Combine(destinationRoot, "empty")).Should().BeFalse();
        installer.InstalledFiles.Should().BeEmpty();
    }

    [Fact]
    public void Install_FileRemovalDeletesDestination()
    {
        var archive = CreateArchive(
            ("to-remove" + BaseInstaller.RemoveFileSuffix, "Unused"));
        var destinationFile = Path.Combine(destinationRoot, "to-remove");
        fs.File.WriteAllText(destinationFile, "Old");
        var installer = new ArchiveInstaller("Package", 1, archive);

        installer.Install(path => new RootPath(destinationRoot).Combine(path),
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        fs.File.Exists(destinationFile).Should().BeFalse();
        installer.InstalledFiles.Select(path => path.Relative).Should().BeEquivalentTo("to-remove");
    }

    #region Utility methods

    private IFileInfo CreateArchive(params (string Name, string Content)[] entries)
    {
        var archiveFile = fs.FileInfo.New(Path.Combine(testDirectory.FullName, "Package.zip"));
        using var archiveStream = archiveFile.Create();
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return archiveFile;
    }

    #endregion
}
