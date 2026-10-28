using System.IO.Abstractions.TestingHelpers;
using Core.Packages.Installation.Backup;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation.Backup;

[IntegrationTest]
public class SkipUpdatedBackupStrategyTest
{
    #region Setup

    private static readonly DateTimeOffset Unused = DateTimeOffset.MinValue;

    private readonly MockFileSystem fs;
    private readonly RootedPath originalFile;

    private readonly Mock<IBackupStrategy> innerStrategyMock = new();
    private readonly Mock<IBackupEventHandler> eventHandlerMock = new();

    public SkipUpdatedBackupStrategyTest()
    {
        fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        originalFile = new PathRoot(fs.DirectoryInfo.New("root")).Combine("original");
    }

    #endregion

    [Fact]
    public void PerformBackup_ProxiesCallToInnerStategy()
    {
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, Unused, eventHandlerMock.Object);

        subs.PerformBackup(originalFile);

        innerStrategyMock.Verify(m => m.PerformBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void DeleteBackup_ProxiesCallToInnerStategy()
    {
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, Unused, eventHandlerMock.Object);

        subs.DeleteBackup(originalFile);

        innerStrategyMock.Verify(m => m.DeleteBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNoBackupTime()
    {
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, Unused, eventHandlerMock.Object);

        subs.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.RestoreBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNoOriginalFile()
    {
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, Unused, eventHandlerMock.Object);

        subs.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.RestoreBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_DeletesBackupIfOverwritten()
    {
        var fileCreationTime = DateTimeOffset.UtcNow;
        var backupTime = fileCreationTime.Subtract(TimeSpan.FromSeconds(1));
        fs.AddFile(originalFile.Full, new MockFileData("") { CreationTime = fileCreationTime });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, backupTime,
            eventHandlerMock.Object);

        subs.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.DeleteBackup(originalFile));

        eventHandlerMock.Verify(m => m.RestoreSkipped(originalFile));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStrategyIfNotOverwritten()
    {
        var backupTime = DateTimeOffset.UtcNow;
        fs.AddFile(originalFile.Full, new MockFileData("") { CreationTime = backupTime });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, backupTime,
            eventHandlerMock.Object);

        subs.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.RestoreBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void AfterInstall_EnsuresDateInThePast()
    {
        var backupTime = DateTimeOffset.UtcNow;
        var futureDate = backupTime.AddDays(1);
        fs.AddFile(originalFile.Full, new MockFileData("") { CreationTime = futureDate });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, backupTime,
            eventHandlerMock.Object);

        subs.AfterInstall(originalFile);

        fs.File.GetCreationTimeUtc(originalFile.Full).Should().BeOnOrBefore(backupTime.UtcDateTime);

        eventHandlerMock.VerifyNoOtherCalls();
    }
}
