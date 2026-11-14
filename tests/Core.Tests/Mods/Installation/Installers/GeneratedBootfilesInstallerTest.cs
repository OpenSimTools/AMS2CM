using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;

using static Core.Mods.Installation.Installers.GeneratedBootfilesInstaller;

namespace Core.Tests.Mods.Installation.Installers;

[UnitTest]
public class GeneratedBootfilesInstallerTest
{
    #region Setup

    private readonly MockFileSystem fs = new();
    private readonly Mock<Func<string, IFileInfo>> resolveSource = new(MockBehavior.Strict);
    private const string AlternateSourcePath = "Sources/first.bin";
    private static readonly DateTime Timestamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public GeneratedBootfilesInstallerTest()
    {
        foreach (var name in SourceNames)
        {
            var relative = Path.Combine(PakfilesDirectory, name);
            var path = Path.Combine("Game", relative);
            fs.AddFile(path, new MockFileData(name) { LastWriteTime = Timestamp });
            resolveSource.Setup(r => r(relative)).Returns(() => fs.FileInfo.New(path));
        }
    }

    #endregion

    [Theory]
    [InlineData(BootFlowPakFileName)]
    [InlineData(BootSplashPakFileName)]
    [InlineData(PhysicsPersistentPakFileName)]
    public void EachSourceChangesVersion(string name)
    {
        var original = CreateInstaller().PackageVersionHash;
        fs.File.AppendAllText(Path.Combine("Game", PakfilesDirectory, name), "Changed");

        CreateInstaller().PackageVersionHash.Should().NotBe(original);
    }

    [Fact]
    public void VersionIsCachedBeforeMutation()
    {
        var expected = CreateInstaller().PackageVersionHash;
        var installer = CreateInstaller();
        fs.File.Delete(Path.Combine("Game", PakfilesDirectory, BootFlowPakFileName));

        installer.PackageVersionHash.Should().Be(expected);
    }

    [Fact]
    public void ConstructionReportsMissingSources()
    {
        fs.Directory.Delete("Game", recursive: true);

        var create = () => CreateInstaller();

        create.Should().Throw<FileNotFoundException>().Which.FileName.Should()
            .Be(fs.Path.GetFullPath(Path.Combine("Game", PakfilesDirectory, BootFlowPakFileName)));
    }

    [Fact]
    public void GenerationUsesSourceWithUnrelatedFileName()
    {
        fs.AddFile(AlternateSourcePath, "Source");
        resolveSource.Setup(r => r(Path.Combine(PakfilesDirectory, BootFlowPakFileName)))
            .Returns(fs.FileInfo.New(AlternateSourcePath));
        var installer = CreateInstaller();

        var install = () => installer.Install(new PathRoot(fs.DirectoryInfo.New("Destination")).Combine,
            Mock.Of<IBackupStrategy>(), new ProcessingCallbacks<RootedPath>());

        // The real archive reader cannot open mock files; its error identifies the selected input.
        install.Should().Throw<IOException>().WithMessage($"*'{fs.Path.GetFullPath(AlternateSourcePath)}'*");
    }

    [Fact]
    public void GenerationFailureDoesNotInstallFiles()
    {
        var path = Path.Combine("Game", PakfilesDirectory, BootFlowPakFileName);
        var original = fs.File.ReadAllText(path);
        var installer = CreateInstaller();

        var install = () => installer.Install(new PathRoot(fs.DirectoryInfo.New("Destination")).Combine,
            Mock.Of<IBackupStrategy>(), new ProcessingCallbacks<RootedPath>());

        install.Should().Throw<Exception>();
        installer.InstalledFiles.Should().BeEmpty();
        fs.File.ReadAllText(path).Should().Be(original);
    }

    [Fact]
    public void GenerationResolvesSourcesAgain()
    {
        var installer = CreateInstaller();
        fs.AddFile(AlternateSourcePath, "Source");
        resolveSource.Setup(r => r(Path.Combine(PakfilesDirectory, BootFlowPakFileName)))
            .Returns(fs.FileInfo.New(AlternateSourcePath));

        var install = () => installer.Install(new PathRoot(fs.DirectoryInfo.New("Destination")).Combine,
            Mock.Of<IBackupStrategy>(), new ProcessingCallbacks<RootedPath>());

        // The archive reader's error identifies the source selected after construction.
        install.Should().Throw<IOException>().WithMessage($"*'{fs.Path.GetFullPath(AlternateSourcePath)}'*");
    }

    #region Utility Methods

    private GeneratedBootfilesInstaller CreateInstaller() =>
        new("Generated", fs.Directory.CreateDirectory("Temp"), resolveSource.Object);

    #endregion
}
