using System.IO.Abstractions.TestingHelpers;
using Core.Backup;

namespace Core.Tests.Backup;

public class MoveFileBackupStrategyTest
{
    private static readonly string OriginalFile = "original";
    private static readonly string OriginalContents = "something";
    private static readonly string BackupFile = GenerateBackupFilePath(OriginalFile);

    private static string GenerateBackupFilePath(string fullPath) => $"b{fullPath}";

    [Fact]
    public void BackupFile_MovesOriginalToBackup()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, OriginalContents },
        });

        var sbs = new MoveFileBackupStrategy(fs, GenerateBackupFilePath);

        sbs.PerformBackup(OriginalFile);

        Assert.False(fs.FileExists(OriginalFile));
        Assert.Equal(OriginalContents, fs.File.ReadAllText(BackupFile));
    }

    [Fact]
    public void BackupFile_SkipsBackupIfFileNotPresent()
    {
        var fs = new MockFileSystem();

        var sbs = new MoveFileBackupStrategy(fs, GenerateBackupFilePath);

        sbs.PerformBackup(OriginalFile);

        Assert.False(fs.FileExists(BackupFile));
    }

    [Fact]
    public void BackupFile_KeepsExistingBackup()
    {
        var oldBackupContents = "old backup";
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, OriginalContents },
            { BackupFile, oldBackupContents },
        });

        var sbs = new MoveFileBackupStrategy(fs, GenerateBackupFilePath);

        sbs.PerformBackup(OriginalFile);

        Assert.False(fs.FileExists(OriginalFile));
        Assert.Equal(oldBackupContents, fs.File.ReadAllText(BackupFile));
    }

    [Fact]
    public void RestoreBackup_MovesBackupToOriginal()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { BackupFile, OriginalContents},
        });

        var sbs = new MoveFileBackupStrategy(fs, GenerateBackupFilePath);

        sbs.RestoreBackup(OriginalFile);

        Assert.Equal(OriginalContents, fs.File.ReadAllText(OriginalFile));
        Assert.False(fs.FileExists(BackupFile));
    }

    [Fact]
    public void RestoreBackup_LeavesOriginalFileIfNoBackup()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, "other contents" },
        });

        var sbs = new MoveFileBackupStrategy(fs, GenerateBackupFilePath);

        sbs.RestoreBackup(OriginalFile);

        Assert.True(fs.FileExists(OriginalFile));
    }

    [Fact]
    public void RestoreBackup_ErrorsIfOriginalFileExists()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { OriginalFile, "other contents" },
            { BackupFile, OriginalContents},
        });

        var sbs = new MoveFileBackupStrategy(fs, GenerateBackupFilePath);

        Assert.Throws<IOException>(() => sbs.RestoreBackup(OriginalFile));

        Assert.NotEqual(OriginalContents, fs.File.ReadAllText(OriginalFile));
        Assert.True(fs.FileExists(BackupFile));
    }

    [Fact]
    public void DeleteBackup_RemovesBackupIfItExists()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { BackupFile, OriginalContents},
        });

        var sbs = new MoveFileBackupStrategy(fs, GenerateBackupFilePath);

        sbs.DeleteBackup(OriginalFile);

        Assert.False(fs.FileExists(OriginalFile));
        Assert.False(fs.FileExists(BackupFile));

        sbs.DeleteBackup(OriginalFile); // Check that it does not error
    }
}
