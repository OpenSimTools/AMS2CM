using System.IO.Abstractions.TestingHelpers;
using Core.Packages.Installation.Backup;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation.Backup;

[IntegrationTest]
public class SkipUpdatedBackupStrategyTest
{
    private readonly RootedPath originalFile = new("root", "original");

    private readonly Mock<IBackupStrategy> innerStrategyMock = new();
    private readonly Mock<IBackupEventHandler> eventHandlerMock = new();

    [Fact]
    public void PerformBackup_ProxiesCallToInnerStategy()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, null, eventHandlerMock.Object);

        subs.PerformBackup(originalFile);

        innerStrategyMock.Verify(m => m.PerformBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void DeleteBackup_ProxiesCallToInnerStategy()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, null, eventHandlerMock.Object);

        subs.DeleteBackup(originalFile);

        innerStrategyMock.Verify(m => m.DeleteBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNoBackupTime()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, null, eventHandlerMock.Object);

        subs.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.RestoreBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNoOriginalFile()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, DateTime.UtcNow, eventHandlerMock.Object);

        subs.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.RestoreBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_DeletesBackupIfOverwritten()
    {
        var fileCreationTime = DateTime.UtcNow;
        var backupTime = fileCreationTime.Subtract(TimeSpan.FromSeconds(1));
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { originalFile.Full, new MockFileData("") { CreationTime = fileCreationTime } },
        });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, backupTime, eventHandlerMock.Object);

        subs.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.DeleteBackup(originalFile));

        eventHandlerMock.Verify(m => m.RestoreSkipped(originalFile));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStrategyIfNotOverwritten()
    {
        var backupTime = DateTime.UtcNow;
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { originalFile.Full, new MockFileData("") { CreationTime = backupTime } },
        });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, backupTime, eventHandlerMock.Object);

        subs.RestoreBackup(originalFile);

        innerStrategyMock.Verify(m => m.RestoreBackup(originalFile));

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void AfterInstall_EnsuresDateInThePast()
    {
        var futureDate = DateTime.UtcNow.AddDays(1);
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { originalFile.Full, new MockFileData("") { CreationTime = futureDate } },
        });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStrategyMock.Object, null, eventHandlerMock.Object);

        subs.AfterInstall(originalFile);

        fs.File.GetCreationTimeUtc(originalFile.Full).Should().BeOnOrBefore(DateTime.UtcNow);

        eventHandlerMock.VerifyNoOtherCalls();
    }
}
