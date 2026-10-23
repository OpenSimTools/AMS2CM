using System.IO.Abstractions.TestingHelpers;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation.Installers;
using Core.Tests.Utils;
using Core.Utils;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Mods.Installation.Installers;

[UnitTest]
public class ModInstallerTest
{
    private const string GameSupportedModDirectory = "ModDirectory";
    private const string GameDirAtRoot = "DirAtRoot";
    private const string BootfilesPackageName = "BootFilesPackage";
    private const string VehicleListFile = "vehiclelist.lst";
    private const string TrackListFile = "tracklist.lst";
    private const string DrivelineFile = "driveline.rg";

    #region Setup

    private readonly MockFileSystem fs = new();
    private readonly FakeTimeProvider fakeTimeProvider = new();
    private readonly Mock<ModInstaller.IConfig> configMock = new();
    private readonly Mock<IBackupStrategy> backupStrategyMock = new();
    private readonly Mock<Action<RootedPath>> callbackMock = new();

    private readonly string destDir;
    private readonly string tempDir;

    private readonly RootPath destRoot;

    public ModInstallerTest()
    {
        var gsmd = InvertCasing(GameSupportedModDirectory);
        configMock.Setup(c => c.DirsAtRoot).Returns([InvertCasing(GameDirAtRoot), gsmd]);
        configMock.Setup(c => c.GameSupportedModDir).Returns(gsmd);
        configMock.Setup(c => c.GenerateModDetails).Returns(true);
        configMock.Setup(c => c.VehicleListFileName).Returns(VehicleListFile);
        configMock.Setup(c => c.TrackListFileName).Returns(TrackListFile);
        configMock.Setup(c => c.DrivelineFileName).Returns(DrivelineFile);

        destDir = fs.Directory.CreateDirectory("Dest").FullName;
        tempDir = fs.Directory.CreateDirectory("Temp").FullName;

        destRoot = new RootPath(destDir);
    }

    #endregion

    [Fact]
    public void AutomaticModConfigurationSkippedWhenNothingToConfigure()
    {
        string[] packageFiles =
        [
            Path.Combine(GameDirAtRoot, "NotRequiringConfiguration"),
        ];

        var modInstaller = InstallWithModInstaller(InstallerOf("A", null, packageFiles));

        modInstaller.InstalledFiles.Should().BeEquivalentPathTo(ToDestRootedPath(packageFiles));
        modInstaller.PackageDependencies.Should().BeEmpty();

        VerifyCallbackCalledWith(packageFiles);

        fs.AllFiles.Should().BeEquivalentPathTo(ToDestPath(packageFiles));
    }

    [Fact]
    public void AutomaticModConfigurationSkippedWhenConfiguredByPackage()
    {
        string[] packageFiles =
        [
            Path.Combine(GameDirAtRoot, "File.crd"),
            // This disables configuration
            Path.Combine(GameSupportedModDirectory, "Anything")
        ];

        var modInstaller = InstallWithModInstaller(InstallerOf("A", null, packageFiles));

        modInstaller.InstalledFiles.Should().BeEquivalentPathTo(ToDestRootedPath(packageFiles));
        modInstaller.PackageDependencies.Should().BeEmpty();

        VerifyCallbackCalledWith(packageFiles);

        fs.AllFiles.Should().BeEquivalentPathTo(ToDestPath(packageFiles));
    }

    [Fact]
    public void AutomaticModConfigurationForVehicles()
    {
        var crdFile = Path.Combine(GameDirAtRoot, "File.crd");
        var notInGameSupportedDir = Path.Combine(GameSupportedModDirectory + "Suffix", "File");
        var drivelineRecord = $"RECORD Something{Environment.NewLine}";

        var packageFiles = new Dictionary<string, string>
        {
            [Path.Combine("SubDir", crdFile)] = "Anything",
            [Path.Combine("SubDir", notInGameSupportedDir)] = "Anything",
            ["FileAtRoot.txt"] = drivelineRecord,
        };

        var modInstaller = InstallWithModInstaller(InstallerOf("A", 0xbadcafe, packageFiles));

        string[] expectedFiles =
        {
            crdFile,
            notInGameSupportedDir,
            Path.Combine(GameSupportedModDirectory, "A_badcafe", VehicleListFile),
            Path.Combine(GameSupportedModDirectory, "A_badcafe", DrivelineFile),
            Path.Combine(GameSupportedModDirectory, "A_badcafe", "A_badcafe.xml")
        };

        modInstaller.InstalledFiles.Should().BeEquivalentPathTo(ToDestRootedPath(expectedFiles));
        modInstaller.PackageDependencies.Should().BeEmpty();

        VerifyCallbackCalledWith(expectedFiles);

        fs.AllFiles.Should().BeEquivalentPathTo(ToDestPath(expectedFiles));
        fs.GetFile(Path.Combine(destDir, GameSupportedModDirectory, "A_badcafe", VehicleListFile))
            .TextContents.Should().Be(crdFile);
        backupStrategyMock.Verify(m => m.AfterInstall(
            destRoot.Combine(GameSupportedModDirectory, "A_badcafe", VehicleListFile)));
        fs.GetFile(Path.Combine(destDir, GameSupportedModDirectory, "A_badcafe", DrivelineFile))
            .TextContents.Should().Be(drivelineRecord.Trim());
        backupStrategyMock.Verify(m => m.AfterInstall(
            destRoot.Combine(GameSupportedModDirectory, "A_badcafe", DrivelineFile)));
    }

    [Fact]
    public void AutomaticModConfigurationCanBeDisabled()
    {
        configMock.Setup(c => c.GenerateModDetails).Returns(false);

        string[] packageFiles =
        [
            Path.Combine(GameDirAtRoot, "File.crd")
        ];

        var modInstaller = InstallWithModInstaller(InstallerOf("A", null, packageFiles));

        var expectedFiles = packageFiles.Concat([
            Path.Combine(GameSupportedModDirectory, "A_0", VehicleListFile)
            // No mod xml
        ]).ToArray();

        modInstaller.InstalledFiles.Should().BeEquivalentPathTo(ToDestRootedPath(expectedFiles));
        modInstaller.PackageDependencies.Should().ContainSingle(BootfilesPackageName);

        VerifyCallbackCalledWith(expectedFiles);

        fs.AllFiles.Should().BeEquivalentPathTo(ToDestPath(expectedFiles));
    }

    [Fact]
    public void AutomaticModConfigurationNotPossibleForTracks()
    {
        string[] packageFiles =
        [
            Path.Combine(GameDirAtRoot, "File.trd")
        ];

        var modInstaller = InstallWithModInstaller(InstallerOf("Bee Cee", null, packageFiles));

        var trackListFile = Path.Combine(GameSupportedModDirectory, "BeeCee_0", TrackListFile);
        var expectedFiles = packageFiles.Concat([
            trackListFile
            // No mod xml
        ]).ToArray();

        modInstaller.InstalledFiles.Should().BeEquivalentPathTo(ToDestRootedPath(expectedFiles));
        modInstaller.PackageDependencies.Should().ContainSingle(BootfilesPackageName);

        VerifyCallbackCalledWith(expectedFiles);

        fs.AllFiles.Should().BeEquivalentPathTo(ToDestPath(expectedFiles));

        fs.GetFile(Path.Combine(destDir, trackListFile))
            .TextContents.Should().Be(Path.Combine(GameDirAtRoot, "@File.trd"));
    }

    [Fact]
    public void TracksAndCarsAreConfiguredOnlyWhenInstalled()
    {
        var modFile = Path.Combine(GameDirAtRoot, "Foo");
        string[] packageFiles =
        [
            Path.Combine("Subdir", modFile),
            Path.Combine("Outside.trd"),
            Path.Combine("Outside.crd"),
        ];

        var modInstaller = InstallWithModInstaller(InstallerOf("M", null, packageFiles));

        modInstaller.InstalledFiles.Should().BeEquivalentPathTo(ToDestRootedPath([modFile]));
    }

    [Fact]
    public void DeletedFilesAreNotConfigured()
    {
        string[] filesToDelete =
        [
            Path.Combine(GameDirAtRoot, "File.crd"),
            Path.Combine(GameDirAtRoot, "File.trd")
        ];

        ToDestPath(filesToDelete).ForEach(fs.AddEmptyFile);

        InstallWithModInstaller(InstallerOf("A", 1,
            filesToDelete.Select(f => f + BaseInstaller.RemoveFileSuffix)
        ));

        fs.AllFiles.Should().BeEmpty();
    }

    [Fact]
    public void ConfiguredFilesExtensionsAreCaseInsensitive()
    {
        string[] files =
        [
            Path.Combine(GameDirAtRoot, "File.CRD"),
            Path.Combine(GameDirAtRoot, "File.TRD")
        ];

        InstallWithModInstaller(InstallerOf("A", 1, files));

        fs.AllFiles.Should().BeEquivalentPathTo(ToDestPath([
            .. files,
            Path.Combine(GameSupportedModDirectory, "A_1", VehicleListFile),
            Path.Combine(GameSupportedModDirectory, "A_1", TrackListFile),
        ]));
    }

    #region Utility Methods

    private ModInstaller InstallWithModInstaller(IPackageInstaller inner)
    {
        var modInstaller = new ModInstaller(inner, fs.DirectoryInfo.New(tempDir), configMock.Object,
            fs.DirectoryInfo.New(destDir), BootfilesPackageName);
        modInstaller.Install(destRoot.Combine,
            backupStrategyMock.Object, new ProcessingCallbacks<RootedPath> { Before = callbackMock.Object });
        fs.Directory.Delete(tempDir, recursive: true);
        return modInstaller;
    }

    private IPackageInstaller InstallerOf(string name, int? versionHash, IEnumerable<string> files) =>
        InstallerOf(name, versionHash, files.ToDictionary(f => f, _ => Convert.ToString(versionHash) ?? string.Empty));

    private StaticFilesInstaller InstallerOf(string name, int? versionHash,
        IReadOnlyDictionary<string, string> fileContents) =>
        new(fs, fakeTimeProvider, name, versionHash, fileContents, Array.Empty<string>());

    private List<string> ToDestPath(IReadOnlyCollection<string> relativePaths) =>
        [.. relativePaths.Select(f => Path.Combine(destDir, f))];

    private HashSet<RootedPath> ToDestRootedPath(IReadOnlyCollection<string> relativePaths) =>
        [.. relativePaths.Select(destRoot.Combine)];

    private void VerifyCallbackCalledWith(IReadOnlyCollection<string> relativePaths)
    {
        foreach (var rp in ToDestRootedPath(relativePaths))
        {
            callbackMock.Verify(a => a(rp), Times.Once);
        }

        callbackMock.VerifyNoOtherCalls();
    }

    private static string InvertCasing(string path) =>
        new(path.Select(InvertCasing).ToArray());

    private static char InvertCasing(char c)
    {
        return char.IsAsciiLetterUpper(c) ? char.ToLowerInvariant(c) :
            char.IsAsciiLetterLower(c) ? char.ToUpperInvariant(c) : c;
    }

    #endregion
}
