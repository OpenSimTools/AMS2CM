using System.IO.Abstractions.TestingHelpers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class RootPathTest
{
    private readonly MockFileSystem fs = new();

    private static readonly string RootDirectory = Path.Combine(Path.GetTempPath(), nameof(RootPathTest));

    [Fact]
    public void Constructor_NormalizesRelativeRootAgainstCurrentDirectory()
    {
        fs.Directory.CreateDirectory(RootDirectory);
        fs.Directory.SetCurrentDirectory(RootDirectory);
        var relative = Path.Combine("a", ".", "b", "..") + Path.DirectorySeparatorChar;

        var root = new RootPath(fs.DirectoryInfo.New(relative));

        root.Full.Should().Be(Path.Combine(fs.Directory.GetCurrentDirectory(), "a"));
        root.FileSystem.Should().BeSameAs(fs);
        fs.Directory.Exists(root.Full).Should().BeFalse();
    }

    [Fact]
    public void Constructor_PreservesRootWhenDirectoryIsMoved()
    {
        var directory = fs.Directory.CreateDirectory(RootDirectory);
        var root = new RootPath(directory);
        var equivalent = new RootPath(fs.DirectoryInfo.New(RootDirectory));
        var child = root.Combine("child");
        var roots = new HashSet<RootPath> { root };
        var paths = new HashSet<RootedPath> { child };
        var hashCode = root.GetHashCode();

        directory.MoveTo(RootDirectory + "-moved");

        directory.FullName.Should().Be(RootDirectory + "-moved");
        root.Full.Should().Be(RootDirectory);
        root.Should().Be(equivalent);
        root.GetHashCode().Should().Be(hashCode);
        roots.Contains(equivalent).Should().BeTrue();
        paths.Contains(equivalent.Combine("child")).Should().BeTrue();
        child.Full.Should().Be(Path.Combine(RootDirectory, "child"));
    }

    [Fact]
    public void EqualityAndHashCode_CompareCanonicalPathsIgnoringCase()
    {
        var root = new RootPath(fs.DirectoryInfo.New(RootDirectory));
        var equivalent = new RootPath(fs.DirectoryInfo.New(Path.Combine(RootDirectory.ToUpperInvariant(), ".")));
        var different = new RootPath(fs.DirectoryInfo.New(RootDirectory + "-other"));

        (root == equivalent).Should().BeTrue();
        root.Equals(different).Should().BeFalse();
        root.GetHashCode().Should().Be(equivalent.GetHashCode());
        new HashSet<RootPath> { root, equivalent, different }.Should().HaveCount(2);
        root.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Combine_SingleAndMultiplePathsNormalizeWithinRoot()
    {
        var root = new RootPath(fs.DirectoryInfo.New(RootDirectory));

        var single = root.Combine(Path.Combine("a", "b", "..", "c"));
        var multiple = root.Combine("a", "", "b", "..", "c");

        single.Root.Should().BeSameAs(root);
        single.Relative.Should().Be(Path.Combine("a", "c"));
        single.Full.Should().Be(Path.Combine(RootDirectory, "a", "c"));
        multiple.Should().Be(single);
        multiple.Root.Should().BeSameAs(root);
    }

    [Fact]
    public void Combine_WithoutPathsRepresentsRoot()
    {
        var root = new RootPath(fs.DirectoryInfo.New(RootDirectory));

        var path = root.Combine();

        path.Root.Should().BeSameAs(root);
        path.Relative.Should().BeEmpty();
        path.Full.Should().Be(RootDirectory);
    }
}
