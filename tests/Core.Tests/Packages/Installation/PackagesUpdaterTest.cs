using System.Collections.ObjectModel;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Packages.Repository;
using Core.Tests.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Packages.Installation;

[IntegrationTest]
public class PackagesUpdaterTest : PackagesUpdaterTestBase<PackagesUpdater.IEventHandler>
{
    #region Initialisation

    private class TestException : Exception;

    // Randomness ensures that at least some test runs will fail if it's used
    private static readonly DateTime ValueNotUsed = Random.Shared.Next() > 0 ? DateTime.MaxValue : DateTime.MinValue;

    protected override IPackagesUpdater<PackagesUpdater.IEventHandler> NewPackagesUpdater(
        IInstallerFactory installerFactory,
        IBackupStrategyProvider<PackageInstallationState, PackagesUpdater.IEventHandler> backupStrategyProvider,
        TimeProvider timeProvider) =>
        new PackagesUpdater<PackagesUpdater.IEventHandler>(installerFactory, backupStrategyProvider, timeProvider);

    #endregion

    [Fact]
    public void Apply_NoPackages()
    {
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Apply([]);

        InstallationState.Should().BeEmpty();

        progress.Should().Equal(1.0);
    }

    [Fact]
    public void Apply_TracksProgress()
    {
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["U1"] =
                new(Time: ValueNotUsed, FsHash: null, Partial: false, Dependencies: [], Files: [], ShadowedBy: []),
            ["U2"] = new(Time: ValueNotUsed, FsHash: null, Partial: false, Dependencies: [], Files: [],
                ShadowedBy: [])
        };

        Apply([
            // Uninstall                            25%
            InstallerOf("I1", fsHash: null, []), // 50%
            InstallerOf("I2", fsHash: null, []), // 75%
            InstallerOf("I3", fsHash: null, []), // 100%
        ]);

        InstallationState.Should().BeEmpty();

        progress.Should().Equal(0.25, 0.5, 0.75, 1.0);
    }

    [Fact]
    public void Apply_InstallsSelectedPackages()
    {
        Apply([
            InstallerOf("A", fsHash: 42, files: [
                "AF"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: FakeUtcInstallationDate, FsHash: 42, Partial: false, Dependencies: [], Files: [
                "AF"
            ], ShadowedBy: [])
        });

        BackupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF")));
        BackupStrategyMock.Verify(m => m.AfterInstall(DestinationPath("AF")));
        BackupStrategyMock.VerifyNoOtherCalls();

        EventHandlerMock.Verify(m => m.UninstallNoPackages());
        EventHandlerMock.Verify(m => m.InstallStart());
        EventHandlerMock.Verify(m => m.InstallCurrent("A"));
        EventHandlerMock.Verify(m => m.InstallEnd());
        EventHandlerMock.Verify(m => m.ProgressUpdate(It.IsAny<IPercent>()));
        EventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_UninstallsUnselectedPackages()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(
                Time: ValueNotUsed,
                FsHash: 42,
                Partial: false,
                Dependencies: [],
                Files: ["AF"],
                ShadowedBy: [])
        };

        Apply([]);

        InstallationState.Should().BeEmpty();

        BackupStrategyMock.Verify(m => m.RestoreBackup(DestinationPath("AF")));
        BackupStrategyMock.VerifyNoOtherCalls();

        EventHandlerMock.Verify(m => m.UninstallStart());
        EventHandlerMock.Verify(m => m.UninstallCurrent("A"));
        EventHandlerMock.Verify(m => m.UninstallEnd());
        EventHandlerMock.Verify(m => m.InstallNoPackages());
        EventHandlerMock.Verify(m => m.ProgressUpdate(It.IsAny<IPercent>()));
        EventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_UpdatesChangedPackages()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: 1, Partial: false, Dependencies: [], Files:
            [
                "AF",
                "AF1",
            ], ShadowedBy: [])
        };

        Apply([
            InstallerOf("A", fsHash: 2, [
                "AF",
                "AF2"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: FakeUtcInstallationDate, FsHash: 2, Partial: false, Dependencies: [], Files: [
                "AF",
                "AF2"
            ], ShadowedBy: [])
        });

        BackupStrategyMock.Verify(m => m.RestoreBackup(DestinationPath("AF1")));
        BackupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF2")));
    }

    [Fact]
    public void Apply_PreservesPackageDependencies()
    {
        Apply([
            InstallerOf("A", fsHash: 42, files: [
                "AF"
            ], dependencies: ["X"])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: FakeUtcInstallationDate, FsHash: 42, Partial: false, Dependencies: ["X"], Files: [
                "AF"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_FirstInstalledFilesTakePrecedence()
    {
        Apply([
            InstallerOf("A", fsHash: 1, files: [
                "AF1", "AF2"
            ]),
            InstallerOf("B", fsHash: 2, files: [
                "BF"
            ]),
            InstallerOf("C", fsHash: 3, files: [
                "AF1", "BF", "CF"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: FakeUtcInstallationDate, FsHash: 1, Partial: false, Dependencies: [], Files: [
                "AF1", "AF2"
            ], ShadowedBy: []),
            ["B"] = new(Time: FakeUtcInstallationDate, FsHash: 2, Partial: false, Dependencies: [], Files: [
                "BF"
            ], ShadowedBy: []),
            ["C"] = new(Time: FakeUtcInstallationDate, FsHash: 3, Partial: false, Dependencies: [], Files: [
                "CF"
            ], ShadowedBy: ["A", "B"])
        });
    }

    [Fact]
    public void Apply_RestoresFilesPreviouslyShadowedByUninstalledPackage()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: 1, Partial: false, Dependencies: [], Files:
            [
                "AF1",
            ], ShadowedBy: []),
            ["B"] = new(Time: ValueNotUsed, FsHash: 2, Partial: false, Dependencies: [], Files:
            [
                "SF", // SF in A was shadowed by B
                "BF1",
            ], ShadowedBy: [])
        };

        Apply([
            InstallerOf("A", fsHash: 1, [
                "SF",
                "AF1"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: FakeUtcInstallationDate, FsHash: 1, Partial: false, Dependencies: [], Files: [
                "SF",
                "AF1"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_InstallStopsIfBackupFails()
    {
        BackupStrategyMock.Setup(m => m.PerformBackup(DestinationPath("Fail"))).Throws<TestException>();

        this.Invoking(m => m.Apply([
            InstallerOf("A", fsHash: 42, files: [
                "AF1", "Fail", "AF2"
            ])
        ])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: FakeUtcInstallationDate, FsHash: 42, Partial: true, Dependencies: [], Files: [
                "AF1",
                "Fail" // We don't know where it failed, so we add it
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_UninstallStopsIfBackupFails()
    {
        BackupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: 42, Partial: false, Dependencies: [], Files:
            [
                "AF1",
                "Fail",
                "AF2"
            ], ShadowedBy: [])
        };

        this.Invoking(m => m.Apply([])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: 42, Partial: true, Dependencies: [], Files: [
                "Fail", // We don't know where it failed, so we leave it
                "AF2"
            ], ShadowedBy: [])
        });
    }


    [Fact]
    public void Apply_UninstallFailuresResultsInPartialInstallation()
    {
        BackupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: null, Partial: false, Dependencies: [], Files:
            [
                "Fail"
            ], ShadowedBy: [])
        };

        this.Invoking(m => m.Apply([])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: null, Partial: true, Dependencies: [], Files: [
                "Fail"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_PartialPackagesStayPartial()
    {
        BackupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: null, Partial: true, Dependencies: [], Files:
            [
                "Fail"
            ], ShadowedBy: [])
        };

        this.Invoking(m => m.Apply([])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: null, Partial: true, Dependencies: [], Files: [
                "Fail"
            ], ShadowedBy: [])
        });
    }


    [Fact]
    public void Apply_UninstallRemovesEmptyDirectories()
    {
        var subDir = Path.Combine("D1", "D2");
        Directory.CreateDirectory(DestinationPath(subDir).Full);

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: null, Partial: true, Dependencies: [], Files:
            [
                Path.Combine(subDir, "F1")
            ], ShadowedBy: [])
        };

        Apply([]);

        InstallationState.Should().BeEmpty();

        Directory.Exists(DestinationPath("D1").Full).Should().BeFalse();
    }

    [Fact]
    public void Apply_HandlesPriorityInversion()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: 0, Partial: false, Dependencies: [], Files:
            [
                "AF",
                "Shared"
            ], ShadowedBy: []),
            ["B"] = new(Time: ValueNotUsed, FsHash: 0, Partial: false, Dependencies: [], Files:
            [
                "BF"
            ], ShadowedBy: ["A"])
        };

        Apply([
            InstallerOf("B", fsHash: 0, [
                "BF",
                "Shared"
            ]),
            InstallerOf("A", fsHash: 0, [
                "AF",
                "Shared"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: FakeUtcInstallationDate, FsHash: 0, Partial: false, Dependencies: [], Files: [
                "AF",

            ], ShadowedBy: ["B"]),
            ["B"] = new(Time: FakeUtcInstallationDate, FsHash: 0, Partial: false, Dependencies: [], Files: [
                "BF",
                "Shared"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_HandlesFileDeletionOnUpgrade()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, FsHash: 0, Partial: false, Dependencies: [], Files:
            [
                "A1",
                "A2"
            ], ShadowedBy: []),
        };

        Apply([
            InstallerOf("A", fsHash: 1, [
                "A1"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: FakeUtcInstallationDate, FsHash: 1, Partial: false, Dependencies: [], Files: [
                "A1",
            ], ShadowedBy: []),
        });
    }

    private static IInstaller InstallerOf(string name, int? fsHash, IReadOnlyCollection<string> files) =>
        InstallerOf(name, fsHash, files, Array.Empty<string>());

    private static IInstaller InstallerOf(string name, int? fsHash,
        IReadOnlyCollection<string> files, IReadOnlyCollection<string> dependencies) =>
        new StaticFilesInstaller(name, fsHash, files.ToDictionary(f => f, _ => ""), dependencies);
}

public abstract class PackagesUpdaterTestBase<TEventHandler> where TEventHandler : class
{
    protected readonly Mock<IBackupStrategy> BackupStrategyMock = new();
    protected readonly Mock<TEventHandler> EventHandlerMock = new();
    protected readonly DateTime FakeUtcInstallationDate = DateTime.Today.AddDays(10).ToUniversalTime();
    private readonly TimeSpan fakeLocalTimeOffset = TimeSpan.FromHours(3);
    protected IReadOnlyDictionary<string, PackageInstallationState>? InstallationState;
    private readonly string destinationDir = Path.GetRandomFileName();

    protected RootedPath DestinationPath(string relativePath) => new(destinationDir, relativePath);

    protected abstract IPackagesUpdater<TEventHandler> NewPackagesUpdater(
        IInstallerFactory installerFactory,
        IBackupStrategyProvider<PackageInstallationState, TEventHandler> backupStrategyProvider,
        TimeProvider timeProvider);

    protected void Apply(IInstaller[] installers)
    {
        var packages = installers.Select(i => new Package(i.PackageName, "", true, null));
        var backupStrategyProviderMock = new Mock<IBackupStrategyProvider<PackageInstallationState, TEventHandler>>();
        backupStrategyProviderMock.Setup(m => m.BackupStrategy(It.IsAny<PackageInstallationState>(), It.IsAny<TEventHandler>()))
            .Returns(BackupStrategyMock.Object);
        var packagesUpdater = NewPackagesUpdater(
            new InstallerForPackage(installers),
            backupStrategyProviderMock.Object,
            new FakeTimeProvider(FakeUtcInstallationDate.WithOffset(fakeLocalTimeOffset)));
        packagesUpdater.Apply(
            InstallationState ?? ReadOnlyDictionary<string, PackageInstallationState>.Empty,
            packages,
            destinationDir,
            newState => InstallationState = newState,
            EventHandlerMock.Object,
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
}
