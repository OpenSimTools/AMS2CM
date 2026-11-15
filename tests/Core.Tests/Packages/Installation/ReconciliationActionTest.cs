using System.IO.Abstractions.TestingHelpers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation;

[UnitTest]
public class ReconciliationActionTest
{
    #region Setup

    private readonly MockFileSystem fs = new();
    private readonly Mock<IBackupStrategy> backup = new();
    private readonly ReconciliationAction<ReconciliationService.IEventHandler>.Factory factory;
    private readonly PackageInstallationState state = new(DateTimeOffset.UnixEpoch, 1, false, [], ["File"], ["Owner"]);

    public ReconciliationActionTest()
    {
        var provider = new Mock<IBackupStrategyProvider<DateTimeOffset, ReconciliationService.IEventHandler>>();
        provider.Setup(p => p.BackupStrategy(It.IsAny<DateTimeOffset>(), It.IsAny<ReconciliationService.IEventHandler>()))
            .Returns(backup.Object);
        factory = new ReconciliationAction<ReconciliationService.IEventHandler>.Factory(
            fs.Directory.CreateDirectory("Game"), provider.Object, Mock.Of<ReconciliationService.IEventHandler>());
    }

    #endregion

    [Fact]
    public void UninstallDoesNotRestoreClaimedFile()
    {
        var action = factory.Uninstall("Package", state);
        var before = new Mock<Action<RootedPath>>();

        action.Execute(new ProcessingCallbacks<RootedPath> { Accept = _ => false, Before = before.Object });

        backup.VerifyNoOtherCalls();
        before.VerifyNoOtherCalls();
        action.InstalledFiles.Should().BeEmpty();
    }

    [Fact]
    public void FailedRestoreRetainsRemainingOwnership()
    {
        backup.Setup(b => b.RestoreBackup(It.IsAny<RootedPath>())).Throws(new IOException("Restore failed"));
        var action = factory.Uninstall("Package", state);

        var execute = () => action.Execute(new ProcessingCallbacks<RootedPath>());

        execute.Should().Throw<IOException>();
        action.InstalledFiles.Should().ContainSingle("File");
        action.InstallState.Should().Be(IInstallation.State.PartiallyInstalled);
        action.ShadowedBy.Should().ContainSingle("Owner");
    }
}
