using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class FileSystemHashTest
{
    private readonly MockFileSystem fs = new();
    private static readonly DateTime TestWriteTime = DateTime.Today;

    [Fact]
    public void Hash32_DifferentContentLength_ChangesHash()
    {
        var hash1 = FileSystemHash.Hash32(AddFile("1", "Content1", TestWriteTime));
        var hash2 = FileSystemHash.Hash32(AddFile("2", "Content2", TestWriteTime));
        var hash3 = FileSystemHash.Hash32(AddFile("3", "Shorter", TestWriteTime));

        hash1.Should().Be(hash2);
        hash1.Should().NotBe(hash3);
    }

    [Fact]
    public void Hash32_DifferentWriteTime_ChangesHash()
    {
        var hash1 = FileSystemHash.Hash32(AddFile("1", "Content", TestWriteTime));
        var hash2 = FileSystemHash.Hash32(AddFile("2", "Content", TestWriteTime.AddMinutes(1)));

        hash1.Should().NotBe(hash2);
    }

    private IFileInfo AddFile(string path, string contents, DateTime lastWriteTime)
    {
        fs.AddFile(path, new MockFileData(contents) { LastWriteTime = lastWriteTime });
        return fs.FileInfo.New(path);
    }
}
