using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Packages.Installation;
using FluentAssertions;

namespace Core.Tests.Packages.Installation;

[UnitTest]
public class CreationTimeFileUpdateTrackerTest
{
    #region Setup

    private static readonly DateTimeOffset BaselineTime = new(2020, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));

    private readonly MockFileSystem fs = new();
    private readonly CreationTimeFileUpdateTracker tracker = new();

    #endregion

    [Fact]
    public void TrackUpdates_LeavesMissingFileAbsent()
    {
        var file = fs.FileInfo.New("inexistent");

        tracker.TrackUpdates(file, BaselineTime);

        file.Exists.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    public void TrackUpdates_ClampsCreationTimeToBaseline(int secondsFromBaseline, int expectedSecondsFromBaseline)
    {
        var creationTime = BaselineTime.UtcDateTime.AddSeconds(secondsFromBaseline);
        var file = CreateFile(creationTime);

        tracker.TrackUpdates(file, BaselineTime);

        file.CreationTimeUtc.Should().Be(BaselineTime.UtcDateTime.AddSeconds(expectedSecondsFromBaseline));
    }

    [Fact]
    public void HasBeenUpdated_ReturnsFalseForMissingFile()
    {
        var file = fs.FileInfo.New("inexistent");

        tracker.HasBeenUpdated(file, BaselineTime).Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void HasBeenUpdated_RequiresCreationTimeAfterBaseline(int secondsFromBaseline, bool expected)
    {
        var file = CreateFile(BaselineTime.UtcDateTime.AddSeconds(secondsFromBaseline));

        tracker.HasBeenUpdated(file, BaselineTime).Should().Be(expected);
    }

    #region Utility Methods

    private IFileInfo CreateFile(DateTime creationTime)
    {
        var filePath = $"file-{creationTime.Second}.txt";
        fs.AddFile(filePath, new MockFileData("") { CreationTime = new DateTimeOffset(creationTime) });
        return fs.FileInfo.New(filePath);
    }

    #endregion
}
