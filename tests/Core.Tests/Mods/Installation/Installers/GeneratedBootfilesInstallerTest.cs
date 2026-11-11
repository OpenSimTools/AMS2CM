using System.IO.Abstractions.TestingHelpers;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Mods.Installation.Installers;

[UnitTest]
public class GeneratedBootfilesInstallerTest
{
    #region Setup

    private readonly MockFileSystem fs = new();
    private readonly Mock<MoveFileBackupStrategy.IBackupFileNaming> backupFileNaming = new();

    private static string SomeSourceFile => SourceFiles.First();
    private static IEnumerable<string> SourceFiles => SourceBackupNames.Keys;
    private static readonly Dictionary<string, string> SourceBackupNames = new()
    {
        ["BOOTFLOW.bff"] = "first.bin",
        ["BOOTSPLASH.bff"] = "second.bin",
        ["PHYSICSPERSISTENT.bff"] = "third.bin"
    };
    private static readonly DateTime Timestamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public GeneratedBootfilesInstallerTest()
    {
        fs.Directory.CreateDirectory("Backups");
        foreach (var name in SourceFiles)
        {
            var path = fs.Path.GetFullPath(Path.Combine("Game", "Pakfiles", name));
            backupFileNaming.Setup(n => n.ToBackup(path)).Returns(BackupPath(name));
            fs.AddFile(path, new MockFileData(name)
            {
                LastWriteTime = Timestamp,
                CreationTime = Timestamp
            });
        }
    }

    #endregion

    [Theory]
    [InlineData("BOOTFLOW.bff")]
    [InlineData("BOOTSPLASH.bff")]
    [InlineData("PHYSICSPERSISTENT.bff")]
    public void EachSourceChangesVersion(string name)
    {
        var original = CreateInstaller().PackageVersionHash;
        fs.File.AppendAllText(Path.Combine("Game", "Pakfiles", name), "Changed");

        CreateInstaller().PackageVersionHash.Should().NotBe(original);
    }

    [Fact]
    public void VersionIsCachedBeforeMutation()
    {
        var expected = CreateInstaller().PackageVersionHash;
        var installer = CreateInstaller();
        fs.File.Delete(Path.Combine("Game", "Pakfiles", SomeSourceFile));

        installer.PackageVersionHash.Should().Be(expected);
    }

    [Fact]
    public void OwnedSourcesUseOriginalBackupsIndividually()
    {
        var original = CreateInstaller().PackageVersionHash;
        var relative = Path.Combine("Pakfiles", SomeSourceFile);
        var path = Path.Combine("Game", relative);
        fs.File.Move(path, BackupPath(SomeSourceFile));
        fs.AddFile(path, new MockFileData("Installed content") { CreationTime = Timestamp });
        var installer = CreateInstaller(new Dictionary<string, PackageInstallationState>
        {
            ["Bootfiles"] = new(new DateTimeOffset(Timestamp), 1, false, [], [relative], [])
        });

        installer.PackageVersionHash.Should().Be(original);
    }

    [Fact]
    public void UpdatedOwnedSourceUsesLiveFile()
    {
        var relative = Path.Combine("Pakfiles", SomeSourceFile);
        var path = Path.Combine("Game", relative);
        fs.AddFile(BackupPath(SomeSourceFile), "Old source");
        fs.File.SetCreationTimeUtc(path, Timestamp.AddDays(1));
        var expected = CreateInstaller().PackageVersionHash;
        var installer = CreateInstaller(new Dictionary<string, PackageInstallationState>
        {
            ["Bootfiles"] = new(new DateTimeOffset(Timestamp), 1, false, [], [relative], [])
        });

        installer.PackageVersionHash.Should().Be(expected);
    }

    [Fact]
    public void ConstructionReportsMissingSources()
    {
        fs.Directory.Delete("Game", recursive: true);

        var create = () => CreateInstaller();

        create.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ConstructionReportsMissingBackup()
    {
        var relative = Path.Combine("Pakfiles", SomeSourceFile);
        var state = new Dictionary<string, PackageInstallationState>
        {
            ["Bootfiles"] = new(new DateTimeOffset(Timestamp), 1, false, [], [relative], [])
        };

        var create = () => CreateInstaller(state);

        create.Should().Throw<FileNotFoundException>().Which.FileName.Should().Be(BackupPath(SomeSourceFile));
    }

    [Fact]
    public void GenerationUsesBackupWithUnrelatedFileName()
    {
        var relative = Path.Combine("Pakfiles", SomeSourceFile);
        fs.File.Move(Path.Combine("Game", relative), BackupPath(SomeSourceFile));
        var installer = CreateInstaller(new Dictionary<string, PackageInstallationState>
        {
            ["Bootfiles"] = new(new DateTimeOffset(Timestamp), 1, false, [], [relative], [])
        });

        var install = () => installer.Install(new PathRoot(fs.DirectoryInfo.New("Destination")).Combine,
            Mock.Of<IBackupStrategy>(), new ProcessingCallbacks<RootedPath>());

        // The real archive reader cannot open mock files; its error identifies the selected input.
        install.Should().Throw<IOException>().WithMessage($"*'{BackupPath(SomeSourceFile)}'*");
    }

    [Fact]
    public void GenerationFailureDoesNotInstallFiles()
    {
        var path = Path.Combine("Game", "Pakfiles", SomeSourceFile);
        var original = fs.File.ReadAllText(path);
        var installer = CreateInstaller();

        var install = () => installer.Install(new PathRoot(fs.DirectoryInfo.New("Destination")).Combine,
            Mock.Of<IBackupStrategy>(), new ProcessingCallbacks<RootedPath>());

        install.Should().Throw<Exception>();
        installer.InstalledFiles.Should().BeEmpty();
        fs.File.ReadAllText(path).Should().Be(original);
    }

    [Fact]
    public void GenerationReselectsRestoredSource()
    {
        var relative = Path.Combine("Pakfiles", SomeSourceFile);
        var path = Path.Combine("Game", relative);
        fs.File.Move(path, BackupPath(SomeSourceFile));
        var state = new Dictionary<string, PackageInstallationState>
        {
            ["Bootfiles"] = new(new DateTimeOffset(Timestamp), 1, false, [], [relative], [])
        };
        var installer = CreateInstaller(state);
        var backupStrategy = new MoveFileBackupStrategy(fs, backupFileNaming.Object, null);
        backupStrategy.RestoreBackup(new PathRoot(fs.DirectoryInfo.New("Game")).Combine(relative));
        state.Remove("Bootfiles");

        var install = () => installer.Install(new PathRoot(fs.DirectoryInfo.New("Destination")).Combine,
            Mock.Of<IBackupStrategy>(), new ProcessingCallbacks<RootedPath>());

        // The archive reader uses the real filesystem. Reaching the live path proves reselection,
        // even though the mock file cannot be opened by that reader.
        install.Should().Throw<IOException>().WithMessage($"*'{fs.Path.GetFullPath(path)}'*");
    }

    #region Utility Methods

    private GeneratedBootfilesInstaller CreateInstaller(
        IReadOnlyDictionary<string, PackageInstallationState>? state = null) =>
        new("Generated", fs.DirectoryInfo.New("Game"), fs.Directory.CreateDirectory("Temp"),
            state ?? new Dictionary<string, PackageInstallationState>(), backupFileNaming.Object);

    private string BackupPath(string name) => fs.Path.GetFullPath(Path.Combine("Backups", SourceBackupNames[name]));

    #endregion
}
