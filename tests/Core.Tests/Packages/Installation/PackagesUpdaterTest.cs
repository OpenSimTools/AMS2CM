using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Packages.Repository;
using Core.Utils;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Packages.Installation;

public class PackagesUpdaterTest
{
    #region Initialisation

    private class TestException : Exception;

    private readonly Mock<IBackupStrategy> backupStrategyMock = new();
    private readonly Mock<PackagesUpdater.IEventHandler> eventHandlerMock = new();
    private readonly DateTime fakeUtcInstallationDate = DateTime.Today.AddDays(10).ToUniversalTime();
    private readonly TimeSpan fakeLocalTimeOffset = TimeSpan.FromHours(3);
    private IReadOnlyDictionary<string, PackageInstallationState>? recordedState;
    private readonly string destinationDir = Path.GetRandomFileName();

    #endregion

    [Fact]
    public void Apply_NoPackages()
    {
        var progress = new List<double>();
        eventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Apply(
            new Dictionary<string, PackageInstallationState>(),
            []
        );

        recordedState.Should().BeEmpty();

        progress.Should().Equal(1.0);
    }

    [Fact]
    public void Apply_TracksProgress()
    {
        var progress = new List<double>();
        eventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["U1"] = new(Time: null, FsHash: null, Partial: false, Dependencies: [], Files: [], ShadowedBy: []),
                ["U2"] = new(Time: null, FsHash: null, Partial: false, Dependencies: [], Files: [], ShadowedBy: [])
            },                                       // 25%
            [
                InstallerOf("I1", fsHash: null, []), // 50%
                InstallerOf("I2", fsHash: null, []), // 75%
                InstallerOf("I3", fsHash: null, []), // 100%
            ]
        );

        recordedState.Should().BeEmpty();

        progress.Should().Equal(0.25, 0.5, 0.75, 1.0);
    }

    [Fact]
    public void Apply_InstallsSelectedPackages()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>(),
            [
                InstallerOf("A", fsHash: 42, files: [
                    "AF"
                ])
            ]
        );

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: fakeUtcInstallationDate, FsHash: 42, Partial: false, Dependencies: [], Files: [
                "AF"
            ], ShadowedBy: [])
        });

        backupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF")));
        backupStrategyMock.Verify(m => m.AfterInstall(DestinationPath("AF")));
        backupStrategyMock.VerifyNoOtherCalls();

        eventHandlerMock.Verify(m => m.UninstallNoPackages());
        eventHandlerMock.Verify(m => m.InstallStart());
        eventHandlerMock.Verify(m => m.InstallCurrent("A"));
        eventHandlerMock.Verify(m => m.InstallEnd());
        eventHandlerMock.Verify(m => m.ProgressUpdate(It.IsAny<IPercent>()));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_UninstallsUnselectedPackages()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>{
                ["A"] = new(
                        Time: null,
                        FsHash: 42,
                        Partial: false,
                        Dependencies: [],
                        Files: ["AF"],
                        ShadowedBy: [])
            },
            []
        );

        recordedState.Should().BeEmpty();

        backupStrategyMock.Verify(m => m.RestoreBackup(DestinationPath("AF")));
        backupStrategyMock.VerifyNoOtherCalls();

        eventHandlerMock.Verify(m => m.UninstallStart());
        eventHandlerMock.Verify(m => m.UninstallCurrent("A"));
        eventHandlerMock.Verify(m => m.UninstallEnd());
        eventHandlerMock.Verify(m => m.InstallNoPackages());
        eventHandlerMock.Verify(m => m.ProgressUpdate(It.IsAny<IPercent>()));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_UpdatesChangedPackages()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(Time: null, FsHash: 1, Partial: false, Dependencies: [], Files: [
                    "AF",
                    "AF1",
                ], ShadowedBy: [])
            },
            [
                InstallerOf("A", fsHash: 2, [
                    "AF",
                    "AF2"
                ])
            ]
        );

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: fakeUtcInstallationDate, FsHash: 2, Partial: false, Dependencies: [], Files: [
                "AF",
                "AF2"
            ], ShadowedBy: [])
        });

        backupStrategyMock.Verify(m => m.RestoreBackup(DestinationPath("AF1")));
        backupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF2")));
    }

    [Fact]
    public void Apply_PreservesPackageDependencies()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>(),
            [
                InstallerOf("A", fsHash: 42, files: [
                    "AF"
                ], dependencies: ["X"])
            ]
        );

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: fakeUtcInstallationDate, FsHash: 42, Partial: false, Dependencies: ["X"], Files: [
                "AF"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_FirstInstalledFilesTakePrecedence()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>(),
            [
                InstallerOf("A", fsHash: 1, files: [
                    "AF1", "AF2"
                ]),
                InstallerOf("B", fsHash: 2, files: [
                    "BF"
                ]),
                InstallerOf("C", fsHash: 3, files: [
                    "AF1", "BF", "CF"
                ])
            ]
        );

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: fakeUtcInstallationDate, FsHash: 1, Partial: false, Dependencies: [], Files: [
                "AF1", "AF2"
            ], ShadowedBy: []),
            ["B"] = new(Time: fakeUtcInstallationDate, FsHash: 2, Partial: false, Dependencies: [], Files: [
                "BF"
            ], ShadowedBy: []),
            ["C"] = new(Time: fakeUtcInstallationDate, FsHash: 3, Partial: false, Dependencies: [], Files: [
                "CF"
            ], ShadowedBy: ["A", "B"])
        });
    }

    [Fact]
    public void Apply_RestoresFilesPreviouslyShadowedByUninstalledPackage()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(Time: null, FsHash: 1, Partial: false, Dependencies: [], Files: [
                    "AF1",
                ], ShadowedBy: []),
                ["B"] = new(Time: null, FsHash: 2, Partial: false, Dependencies: [], Files: [
                    "SF", // SF in A was shadowed by B
                    "BF1",
                ], ShadowedBy: [])
            },
            [
                InstallerOf("A", fsHash: 1, [
                    "SF",
                    "AF1"
                ])
            ]
        );

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: fakeUtcInstallationDate, FsHash: 1, Partial: false, Dependencies: [], Files: [
                "SF",
                "AF1"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_InstallStopsIfBackupFails()
    {
        backupStrategyMock.Setup(m => m.PerformBackup(DestinationPath("Fail"))).Throws<TestException>();

        this.Invoking(m => m.Apply(
            new Dictionary<string, PackageInstallationState>(),
            [
                InstallerOf("A", fsHash: 42, files: [
                    "AF1", "Fail", "AF2"
                ])
            ]
        )).Should().Throw<TestException>();

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: fakeUtcInstallationDate, FsHash: 42, Partial: true, Dependencies: [], Files: [
                "AF1",
                "Fail" // We don't know where it failed, so we add it
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_UninstallStopsIfBackupFails()
    {
        backupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        this.Invoking(m => m.Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(Time: null, FsHash: 42, Partial: false, Dependencies: [], Files: [
                    "AF1",
                    "Fail",
                    "AF2"
                ], ShadowedBy: [])
            },
            []
        )).Should().Throw<TestException>();

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: null, FsHash: 42, Partial: true, Dependencies: [], Files: [
                "Fail", // We don't know where it failed, so we leave it
                "AF2"
            ], ShadowedBy: [])
        });
    }


    [Fact]
    public void Apply_UninstallFailuresResultsInPartialInstallation()
    {
        backupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        this.Invoking(m => m.Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(Time: null, FsHash: null, Partial: false, Dependencies: [], Files: [
                    "Fail"
                ], ShadowedBy: [])
            },
            []
        )).Should().Throw<TestException>();

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: null, FsHash: null, Partial: true, Dependencies: [], Files: [
                "Fail"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_PartialPackagesStayPartial()
    {
        backupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        this.Invoking(m => m.Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(Time: null, FsHash: null, Partial: true, Dependencies: [], Files: [
                    "Fail"
                ], ShadowedBy: [])
            },
            []
        )).Should().Throw<TestException>();

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: null, FsHash: null, Partial: true, Dependencies: [], Files: [
                "Fail"
            ], ShadowedBy: [])
        });
    }


    [Fact]
    public void Apply_UninstallRemovesEmptyDirectories()
    {
        var subDir = Path.Combine("D1", "D2");
        Directory.CreateDirectory(DestinationPath(subDir).Full);

        Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(Time: null, FsHash: null, Partial: true, Dependencies: [], Files: [
                    Path.Combine(subDir, "F1")
                ], ShadowedBy: [])
            },
            []
        );

        recordedState.Should().BeEmpty();

        Directory.Exists(DestinationPath("D1").Full).Should().BeFalse();
    }

    #region Utility methods

    protected RootedPath DestinationPath(string relativePath) => new(destinationDir, relativePath);

    private void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> oldState,
        IReadOnlyCollection<IInstaller> installers)
    {
        var packages = installers.Select(i => new Package(i.PackageName, "", true, null));
        var backupStrategyProviderMock = new Mock<IBackupStrategyProvider<PackageInstallationState, PackagesUpdater.IEventHandler>>();
        backupStrategyProviderMock.Setup(m => m.BackupStrategy(It.IsAny<PackageInstallationState>(), eventHandlerMock.Object))
            .Returns(backupStrategyMock.Object);
        var packagesUpdater = new PackagesUpdater<PackagesUpdater.IEventHandler>(
            new InstallerForPackage(installers),
            backupStrategyProviderMock.Object,
            new FakeTimeProvider(fakeUtcInstallationDate.WithOffset(fakeLocalTimeOffset)));
        packagesUpdater.Apply(
            oldState,
            packages,
            destinationDir,
            newState => recordedState = newState,
            eventHandlerMock.Object,
            CancellationToken.None);
    }

    private class InstallerForPackage : IInstallerFactory
    {
        private readonly IReadOnlyCollection<IInstaller> installers;

        internal InstallerForPackage(IReadOnlyCollection<IInstaller> installers)
        {
            this.installers = installers;
        }

        public IInstaller PackageInstaller(Package package) =>
            installers.First(installer => installer.PackageName == package.Name);
    }

    private static IInstaller InstallerOf(string name, int? fsHash, IReadOnlyCollection<string> files) =>
        InstallerOf(name, fsHash, files, Array.Empty<string>());

    private static IInstaller InstallerOf(string name, int? fsHash,
        IReadOnlyCollection<string> files, IReadOnlyCollection<string> dependencies) =>
        new StaticFilesInstaller(name, fsHash, files, dependencies);

    private class StaticFilesInstaller : BaseInstaller<object>
    {
        private static readonly object NoContext = new();
        private readonly IReadOnlyCollection<string> files;

        internal StaticFilesInstaller(string packageName, int? packageFsHash, IReadOnlyCollection<string> files,
            IReadOnlyCollection<string> packageDependencies) :
            base(packageName, packageFsHash, packageDependencies)
        {
            this.files = files;
        }

        protected override void InstalAllFiles(InstallBody body)
        {
            foreach (var file in files)
            {
                body(file, NoContext);
            }
        }

        protected override void InstallFile(RootedPath destinationPath, object context)
        {
            // Do not install any file for real
        }

        // Install everything from the root directory

        private static readonly string DirAtRoot = "X";

        public override IEnumerable<string> RelativeDirectoryPaths => [DirAtRoot];
    }

    #endregion
}
