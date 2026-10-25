using System.IO.Abstractions.TestingHelpers;
using Core.Packages.Installation.Backup;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation.Backup;

[UnitTest]
public class MoveFileBackupStrategyTest
{
    private const string OriginalFile = "original";
    private const string OriginalContents = "something";

    private readonly MockFileSystem fs = new();
    private readonly RootedPath originalPath;

    private readonly Mock<MoveFileBackupStrategy.IBackupFileNaming> backupFileNamingMock = new();
    private readonly Mock<IBackupEventHandler> eventHandlerMock = new();

    private MoveFileBackupStrategy.IBackupFileNaming BackupFileNaming => backupFileNamingMock.Object;
    private string BackupFile => BackupFileNaming.ToBackup(OriginalFile);

    public MoveFileBackupStrategyTest()
    {
        originalPath = new RootPath(fs.Directory.GetCurrentDirectory()).Combine(OriginalFile);
        backupFileNamingMock.Setup(m => m.ToBackup(It.IsAny<string>())).Returns<string>(f => $"{f}.backup");
    }

    [Fact]
    public void PerformBackup_MovesOriginalToBackup()
    {
        fs.AddFile(OriginalFile, OriginalContents);
        var mfbs = new MoveFileBackupStrategy(fs.File, BackupFileNaming, eventHandlerMock.Object);

        mfbs.PerformBackup(originalPath);

        fs.FileExists(OriginalFile).Should().BeFalse();
        fs.File.ReadAllText(BackupFile).Should().Be(OriginalContents);

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void PerformBackup_ErrorsIfNameIsBackupName()
    {
        backupFileNamingMock.Setup(_ => _.IsBackup(originalPath.Full)).Returns(true);
        var mfbs = new MoveFileBackupStrategy(fs.File, BackupFileNaming, eventHandlerMock.Object);

        mfbs.Invoking(_ => _.PerformBackup(originalPath))
            .Should().Throw<InvalidOperationException>();

        fs.FileExists(BackupFile).Should().BeFalse();

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void PerformBackup_SkipsBackupIfFileNotPresent()
    {
        var mfbs = new MoveFileBackupStrategy(fs.File, BackupFileNaming, eventHandlerMock.Object);

        mfbs.PerformBackup(originalPath);

        fs.FileExists(BackupFile).Should().BeFalse();

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void PerformBackup_KeepsExistingBackup()
    {
        var oldBackupContents = "old backup";
        fs.AddFile(OriginalFile, OriginalContents);
        fs.AddFile(BackupFile, oldBackupContents);
        var mfbs = new MoveFileBackupStrategy(fs.File, BackupFileNaming, eventHandlerMock.Object);

        mfbs.PerformBackup(originalPath);

        fs.FileExists(OriginalFile).Should().BeFalse();
        fs.File.ReadAllText(BackupFile).Should().Be(oldBackupContents);

        eventHandlerMock.Verify(m => m.BackupSkipped(originalPath));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_MovesBackupToOriginal()
    {
        fs.AddFile(BackupFile, OriginalContents);
        var mfbs = new MoveFileBackupStrategy(fs.File, BackupFileNaming, eventHandlerMock.Object);

        mfbs.RestoreBackup(originalPath);

        fs.File.ReadAllText(OriginalFile).Should().Be(OriginalContents);
        fs.FileExists(BackupFile).Should().BeFalse();

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_OverwritesOriginalFile()
    {
        fs.AddFile(OriginalFile, "other contents");
        fs.AddFile(BackupFile, OriginalContents);
        var mfbs = new MoveFileBackupStrategy(fs.File, BackupFileNaming, eventHandlerMock.Object);

        mfbs.RestoreBackup(originalPath);

        fs.File.ReadAllText(OriginalFile).Should().Be(OriginalContents);
        fs.FileExists(BackupFile).Should().BeFalse();

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_WhenNoOriginalFile()
    {
        fs.AddFile(BackupFile, OriginalContents);
        var mfbs = new MoveFileBackupStrategy(fs.File, BackupFileNaming, eventHandlerMock.Object);

        mfbs.RestoreBackup(originalPath);

        fs.File.ReadAllText(OriginalFile).Should().Be(OriginalContents);
        fs.FileExists(BackupFile).Should().BeFalse();

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RestoreBackup_DeletesOriginalFileIfNoBackup()
    {
        fs.AddFile(OriginalFile, "other contents");
        var mfbs = new MoveFileBackupStrategy(fs.File, BackupFileNaming, eventHandlerMock.Object);

        mfbs.RestoreBackup(originalPath);

        fs.FileExists(OriginalFile).Should().BeFalse();

        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void DeleteBackup_RemovesBackupIfItExists()
    {
        fs.AddFile(BackupFile, OriginalContents);
        var mfbs = new MoveFileBackupStrategy(fs.File, BackupFileNaming, eventHandlerMock.Object);

        mfbs.DeleteBackup(originalPath);

        fs.FileExists(OriginalFile).Should().BeFalse();
        fs.FileExists(BackupFile).Should().BeFalse();

        mfbs.DeleteBackup(originalPath); // Check that it does not error

        eventHandlerMock.VerifyNoOtherCalls();
    }
}
