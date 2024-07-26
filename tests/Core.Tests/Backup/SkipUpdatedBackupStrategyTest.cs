using System.IO.Abstractions.TestingHelpers;
using Core.Backup;
using FluentAssertions;
using static Core.Backup.MoveFileBackupStrategy;

namespace Core.Tests.Backup;

[IntegrationTest]
public class SkipUpdatedBackupStrategyTest
{
    private const string OriginalFile = "original";

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

        innerStategyMock.Verify(_ => _.PerformBackup(OriginalFile));
    }

    [Fact]
    public void DeleteBackup_ProxiesCallToInnerStategy()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, null);

        subs.DeleteBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.DeleteBackup(OriginalFile));
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNoBackupTime()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, null);

        subs.RestoreBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.RestoreBackup(OriginalFile));
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNoOriginalFile()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>());
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, DateTime.UtcNow);

        subs.RestoreBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.RestoreBackup(OriginalFile));
    }

    [Fact]
    public void RestoreBackup_DeletesBackupIfOverwritten()
    {
        var fileCreationTime = DateTime.UtcNow;
        var backupTime = fileCreationTime.Subtract(TimeSpan.FromSeconds(1));
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, new MockFileData("") { CreationTime = fileCreationTime } },
        });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, backupTime);

        subs.RestoreBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.DeleteBackup(OriginalFile));
    }

    [Fact]
    public void RestoreBackup_ProxiesCallToInnerStategyIfNotOverwritten()
    {
        var backupTime = DateTime.UtcNow;
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, new MockFileData("") { CreationTime = backupTime } },
        });
        var subs = new SkipUpdatedBackupStrategy(fs, innerStategyMock.Object, backupTime);

        subs.RestoreBackup(OriginalFile);

        innerStategyMock.Verify(_ => _.RestoreBackup(OriginalFile));
    }
}
