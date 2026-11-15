using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Utils;

namespace Core.Tests.Packages.Installation.Backup;

[UnitTest]
public class SkipUpdatedBackupStrategyTest
{
    #region Setup

    private static readonly DateTimeOffset BackupTime = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly MockFileSystem fs = new();
    private readonly RootedPath originalFile;
    private readonly Mock<IFileUpdateTracker> updateTracker = new(MockBehavior.Strict);
    private readonly Mock<IBackupStrategy> innerStrategyMock = new(MockBehavior.Strict);
    private readonly Mock<IBackupEventHandler> eventHandlerMock = new(MockBehavior.Strict);

    public SkipUpdatedBackupStrategyTest()
    {
        originalFile = new PathRoot(fs.DirectoryInfo.New("root")).Combine("original");
    }

    #endregion

    [Fact]
    public void PerformBackup_ProxiesCallToInnerStrategy()
    {
        innerStrategyMock.Setup(m => m.PerformBackup(originalFile));
        var strategy = CreateStrategy();

        strategy.PerformBackup(originalFile);

        innerStrategyMock.Verify(m => m.PerformBackup(originalFile), Times.Once);
    }

    [Fact]
    public void RestoreBackup_DeletesBackupIfUpdated()
    {
        updateTracker.Setup(t => t.HasBeenUpdated(It.Is<IFileInfo>(f => f.FullName == originalFile.Full), BackupTime))
            .Returns(true);
        innerStrategyMock.Setup(m => m.DeleteBackup(originalFile));
        eventHandlerMock.Setup(m => m.RestoreSkipped(originalFile));
        var strategy = CreateStrategy();

        strategy.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.DeleteBackup(originalFile), Times.Once);
        eventHandlerMock.Verify(m => m.RestoreSkipped(originalFile), Times.Once);
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStrategyIfNotUpdated()
    {
        updateTracker.Setup(t => t.HasBeenUpdated(It.Is<IFileInfo>(f => f.FullName == originalFile.Full), BackupTime))
            .Returns(false);
        innerStrategyMock.Setup(m => m.RestoreBackup(originalFile));
        var strategy = CreateStrategy();

        strategy.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.RestoreBackup(originalFile), Times.Once);
    }

    [Fact]
    public void DeleteBackup_ProxiesCallToInnerStrategy()
    {
        innerStrategyMock.Setup(m => m.DeleteBackup(originalFile));
        var strategy = CreateStrategy();

        strategy.DeleteBackup(originalFile);

        innerStrategyMock.Verify(m => m.DeleteBackup(originalFile), Times.Once);
    }

    [Fact]
    public void AfterInstall_TracksUpdatesAfterInnerStrategy()
    {
        var sequence = new MockSequence();
        innerStrategyMock.InSequence(sequence).Setup(m => m.AfterInstall(originalFile));
        updateTracker.InSequence(sequence)
            .Setup(t => t.TrackUpdates(It.Is<IFileInfo>(f => f.FullName == originalFile.Full), BackupTime));
        var strategy = CreateStrategy();

        strategy.AfterInstall(originalFile);

        innerStrategyMock.Verify(m => m.AfterInstall(originalFile), Times.Once);
        updateTracker.Verify(t => t.TrackUpdates(It.Is<IFileInfo>(f => f.FullName == originalFile.Full), BackupTime),
            Times.Once);
    }

    #region Utility Methods

    private SkipUpdatedBackupStrategy CreateStrategy() =>
        new(fs, updateTracker.Object, innerStrategyMock.Object, BackupTime, eventHandlerMock.Object);

    #endregion
}
