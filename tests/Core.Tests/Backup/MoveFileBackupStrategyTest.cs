using System.IO.Abstractions.TestingHelpers;
using Core.Backup;
using FluentAssertions;
using static Core.Backup.MoveFileBackupStrategy;

namespace Core.Tests.Backup;

[IntegrationTest]
public class MoveFileBackupStrategyTest
{
    private static readonly string OriginalFile = "original";
    private static readonly string OriginalContents = "something";

    private readonly Mock<MoveFileBackupStrategy.IBackupFileNaming> backupFileNamingMock;

    private MoveFileBackupStrategy.IBackupFileNaming BackupFileNaming => backupFileNamingMock.Object;
    private string BackupFile => BackupFileNaming.ToBackup(OriginalFile);

    public MoveFileBackupStrategyTest()
    {
        backupFileNamingMock = new();
        backupFileNamingMock.Setup(_ => _.ToBackup(It.IsAny<string>())).Returns<string>(_ => $"b{_}");
    }

    [Fact]
    public void PerformBackup_MovesOriginalToBackup()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, OriginalContents },
        });
        var sbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        sbs.PerformBackup(OriginalFile);

        fs.FileExists(OriginalFile).Should().BeFalse();
        fs.File.ReadAllText(BackupFile).Should().Be(OriginalContents);
    }

    [Fact]
    public void PerformBackup_ErrorsIfNameIsBackupName()
    {
        var fs = new MockFileSystem();
        backupFileNamingMock.Setup(_ => _.IsBackup(OriginalFile)).Returns(true);
        var sbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        sbs.Invoking(_ => _.PerformBackup(OriginalFile))
            .Should().Throw<InvalidOperationException>();

        fs.FileExists(BackupFile).Should().BeFalse();
    }

    [Fact]
    public void PerformBackup_SkipsBackupIfFileNotPresent()
    {
        var fs = new MockFileSystem();
        var sbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        sbs.PerformBackup(OriginalFile);

        fs.FileExists(BackupFile).Should().BeFalse();
    }

    [Fact]
    public void PerformBackup_KeepsExistingBackup()
    {
        var oldBackupContents = "old backup";
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, OriginalContents },
            { BackupFile, oldBackupContents },
        });
        var sbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        sbs.PerformBackup(OriginalFile);

        fs.FileExists(OriginalFile).Should().BeFalse();
        fs.File.ReadAllText(BackupFile).Should().Be(oldBackupContents);
    }

    [Fact]
    public void RestoreBackup_MovesBackupToOriginal()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { BackupFile, OriginalContents},
        });
        var sbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        sbs.RestoreBackup(OriginalFile);

        fs.File.ReadAllText(OriginalFile).Should().Be(OriginalContents);
        fs.FileExists(BackupFile).Should().BeFalse();
    }

    [Fact]
    public void RestoreBackup_LeavesOriginalFileIfNoBackup()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, "other contents" },
        });
        var sbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        sbs.RestoreBackup(OriginalFile);

        fs.FileExists(OriginalFile).Should().BeTrue();
    }

    [Fact]
    public void RestoreBackup_ErrorsIfOriginalFileExists()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, "other contents" },
            { BackupFile, OriginalContents},
        });
        var sbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        sbs.Invoking(_ =>  _.RestoreBackup(OriginalFile)).Should().Throw<IOException>();

        fs.File.ReadAllText(OriginalFile).Should().NotBe(OriginalContents);
        fs.FileExists(BackupFile).Should().BeTrue();
    }

    [Fact]
    public void DeleteBackup_RemovesBackupIfItExists()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { BackupFile, OriginalContents},
        });
        var sbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        sbs.DeleteBackup(OriginalFile);

        fs.FileExists(OriginalFile).Should().BeFalse();
        fs.FileExists(BackupFile).Should().BeFalse();

        sbs.DeleteBackup(OriginalFile); // Check that it does not error
    }
}
