using Core.Packages.Installation.Backup;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation.Backup;

[UnitTest]
public class SkipInstalledBackupStrategyTest
{
    #region Setup

    private readonly RootedPath path = new RootPath("Original").Combine("File");
    private readonly DateTimeOffset backupTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1));
    private readonly Mock<IBackupStrategy> inner = new();
    private readonly Mock<IBackupStrategyProvider<DateTimeOffset, IBackupEventHandler>> baseProvider = new();

    public SkipInstalledBackupStrategyTest()
    {
        baseProvider.Setup(m => m.BackupStrategy(It.IsAny<DateTimeOffset>(), It.IsAny<IBackupEventHandler>()))
            .Returns(inner.Object);
    }

    private SkipInstalledBackupStrategy.Provider<IBackupEventHandler> Provider(params RootedPath[] files) =>
        new(baseProvider.Object, files);

    #endregion

    [Fact]
    public void PerformBackup_InnerIsCalledIfPathNotInstalled()
    {
        var strategy = Provider().BackupStrategy(backupTime, null);

        strategy.PerformBackup(path);
        strategy.PerformBackup(path);

        inner.Verify(m => m.PerformBackup(path), Times.Exactly(2));
    }

    [Fact]
    public void PerformBackup_InnerIsNotCalledIfPathInstalled()
    {
        var strategy = Provider(path).BackupStrategy(backupTime, null);

        strategy.PerformBackup(path);

        inner.Verify(m => m.PerformBackup(path), Times.Never);
    }

    [Fact]
    public void AfterInstall_PreventsBackupsAcrossStrategies()
    {
        var provider = Provider();
        var first = provider.BackupStrategy(backupTime, null);
        var second = provider.BackupStrategy(backupTime.AddDays(1), null);

        first.AfterInstall(path);
        second.PerformBackup(path);

        inner.Verify(m => m.AfterInstall(path), Times.Once);
        inner.Verify(m => m.PerformBackup(path), Times.Never);
    }

    [Fact]
    public void AfterInstall_FailureDoesNotTrackPath()
    {
        var strategy = Provider().BackupStrategy(backupTime, null);
        inner.Setup(m => m.AfterInstall(path)).Throws<IOException>();

        strategy.Invoking(m => m.AfterInstall(path)).Should().Throw<IOException>();
        strategy.PerformBackup(path);

        inner.Verify(m => m.PerformBackup(path), Times.Once);
    }

    [Fact]
    public void RestoreBackup_MakesPathEligibleForBackupAcrossStrategies()
    {
        var provider = Provider(path);
        var uninstall = provider.BackupStrategy(backupTime, null);
        var install = provider.BackupStrategy(backupTime, null);

        uninstall.RestoreBackup(path);
        install.PerformBackup(path);

        inner.Verify(m => m.RestoreBackup(path), Times.Once);
        inner.Verify(m => m.PerformBackup(path), Times.Once);
    }

    [Fact]
    public void RestoreBackup_FailureLeavesPathTracked()
    {
        var strategy = Provider(path).BackupStrategy(backupTime, null);
        inner.Setup(m => m.RestoreBackup(path)).Throws<IOException>();

        strategy.Invoking(m => m.RestoreBackup(path)).Should().Throw<IOException>();
        strategy.PerformBackup(path);

        inner.Verify(m => m.PerformBackup(path), Times.Never);
    }

    [Fact]
    public void DeleteBackup_DoesNotUntrackInstalledFile()
    {
        var strategy = Provider(path).BackupStrategy(DateTimeOffset.UtcNow, null);

        strategy.DeleteBackup(path);
        strategy.PerformBackup(path);

        inner.Verify(m => m.DeleteBackup(path), Times.Once);
        inner.Verify(m => m.PerformBackup(path), Times.Never);
    }

    [Fact]
    public void Providers_DoNotShareTracking()
    {
        Provider().BackupStrategy(backupTime, null).AfterInstall(path);

        Provider().BackupStrategy(backupTime, null).PerformBackup(path);

        inner.Verify(m => m.PerformBackup(path), Times.Once);
    }
}
