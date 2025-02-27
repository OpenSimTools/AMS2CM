using System.IO.Abstractions.TestingHelpers;
using Core.Packages.Installation.Backup;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation.Backup;

[IntegrationTest]
public class MoveFileBackupStrategyTest
{
    private const string OriginalFile = "original";
    private const string OriginalContents = "something";

    private static readonly RootedPath OriginalPath = new("", OriginalFile);

    private readonly Mock<MoveFileBackupStrategy.IBackupFileNaming> backupFileNamingMock = new();

    private MoveFileBackupStrategy.IBackupFileNaming BackupFileNaming => backupFileNamingMock.Object;
    private string BackupFile => BackupFileNaming.ToBackup(OriginalFile);

    public MoveFileBackupStrategyTest()
    {
        backupFileNamingMock.Setup(_ => _.ToBackup(It.IsAny<string>())).Returns<string>(_ => $"b{_}");
    }

    [Fact]
    public void PerformBackup_MovesOriginalToBackup()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, OriginalContents },
        });
        var mfbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        mfbs.PerformBackup(OriginalPath);

        fs.FileExists(OriginalFile).Should().BeFalse();
        fs.File.ReadAllText(BackupFile).Should().Be(OriginalContents);
    }

    [Fact]
    public void PerformBackup_ErrorsIfNameIsBackupName()
    {
        var fs = new MockFileSystem();
        backupFileNamingMock.Setup(_ => _.IsBackup(OriginalFile)).Returns(true);
        var mfbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        mfbs.Invoking(_ => _.PerformBackup(OriginalPath))
            .Should().Throw<InvalidOperationException>();

        fs.FileExists(BackupFile).Should().BeFalse();
    }

    [Fact]
    public void PerformBackup_SkipsBackupIfFileNotPresent()
    {
        var fs = new MockFileSystem();
        var mfbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        mfbs.PerformBackup(OriginalPath);

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
        var mfbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        mfbs.PerformBackup(OriginalPath);

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
        var mfbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        mfbs.RestoreBackup(OriginalPath);

        fs.File.ReadAllText(OriginalFile).Should().Be(OriginalContents);
        fs.FileExists(BackupFile).Should().BeFalse();
    }

    [Fact]
    public void RestoreBackup_OverwritesOriginalFile()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, "other contents" },
            { BackupFile, OriginalContents},
        });
        var mfbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        mfbs.RestoreBackup(OriginalPath);

        fs.File.ReadAllText(OriginalFile).Should().Be(OriginalContents);
        fs.FileExists(BackupFile).Should().BeFalse();
    }

    [Fact]
    public void RestoreBackup_WhenNoOriginalFile()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { BackupFile, OriginalContents},
        });
        var mfbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        mfbs.RestoreBackup(OriginalPath);

        fs.File.ReadAllText(OriginalFile).Should().Be(OriginalContents);
        fs.FileExists(BackupFile).Should().BeFalse();
    }

    [Fact]
    public void RestoreBackup_DeletesOriginalFileIfNoBackup()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, "other contents" },
        });
        var mfbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        mfbs.RestoreBackup(OriginalPath);

        fs.FileExists(OriginalFile).Should().BeFalse();
    }

    [Fact]
    public void DeleteBackup_RemovesBackupIfItExists()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { BackupFile, OriginalContents},
        });
        var mfbs = new MoveFileBackupStrategy(fs, BackupFileNaming);

        mfbs.DeleteBackup(OriginalPath);

        fs.FileExists(OriginalFile).Should().BeFalse();
        fs.FileExists(BackupFile).Should().BeFalse();

        mfbs.DeleteBackup(OriginalPath); // Check that it does not error
    }
}
