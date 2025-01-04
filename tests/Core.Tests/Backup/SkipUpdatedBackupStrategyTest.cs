using System.IO.Abstractions.TestingHelpers;
using Core.Backup;
using Core.Mods;
using FluentAssertions;
using static Core.Backup.MoveFileBackupStrategy;

namespace Core.Tests.Backup;

[IntegrationTest]
public class SkipUpdatedBackupStrategyTest
{
    private readonly RootedPath OriginalFile = new("root", "original");

    private readonly Mock<IBackupStrategy> innerStategyMock;

    public SkipUpdatedBackupStrategyTest()
    {
        innerStategyMock = new();
    }

    [Fact]
    public void PerformBackup_ProxiesCallToInnerStategy()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, null);

        subs.PerformBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.PerformBackup(OriginalFile.Full));
    }

    [Fact]
    public void DeleteBackup_ProxiesCallToInnerStategy()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, null);

        subs.DeleteBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.DeleteBackup(OriginalFile.Full));
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNoBackupTime()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, null);

        subs.RestoreBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.RestoreBackup(OriginalFile.Full));
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNoOriginalFile()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, DateTime.UtcNow);

        subs.RestoreBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.RestoreBackup(OriginalFile.Full));
    }

    [Fact]
    public void RestoreBackup_DeletesBackupIfOverwritten()
    {
        var fileCreationTime = DateTime.UtcNow;
        var backupTime = fileCreationTime.Subtract(TimeSpan.FromSeconds(1));
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile.Full, new MockFileData("") { CreationTime = fileCreationTime } },
        });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, backupTime);

        subs.RestoreBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.DeleteBackup(OriginalFile.Full));
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNotOverwritten()
    {
        var backupTime = DateTime.UtcNow;
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile.Full, new MockFileData("") { CreationTime = backupTime } },
        });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, backupTime);

        subs.RestoreBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.RestoreBackup(OriginalFile.Full));
    }

    [Fact]
    public void AfterInstall_EnduresDateInThePast()
    {
        var futureDate = DateTime.UtcNow.AddDays(1);
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile.Full, new MockFileData("") { CreationTime = futureDate } },
        });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, null);

        subs.AfterInstall(OriginalFile);

        fs.File.GetCreationTimeUtc(OriginalFile.Full).Should().BeOnOrBefore(DateTime.UtcNow);
    }
}
