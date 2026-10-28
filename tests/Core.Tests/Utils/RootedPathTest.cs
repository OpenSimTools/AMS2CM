using System.IO.Abstractions.TestingHelpers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class RootedPathTest
{
    private readonly MockFileSystem fs;
    private readonly PathRoot pathRoot;

    private static readonly string RootDirectory = Path.Combine(Path.GetTempPath(), nameof(RootedPathTest));

    public RootedPathTest()
    {
        fs = new MockFileSystem();
        pathRoot = new PathRoot(fs.DirectoryInfo.New(RootDirectory));
    }

    [Fact]
    public void Constructor_NormalizesRelativeAndAbsolutePathsWithinRoot()
    {
        var relative = Path.Combine("a", ".", "b", "..", "c");

        var path = new RootedPath(pathRoot, relative);
        var absolute = new RootedPath(pathRoot, Path.Combine(RootDirectory, relative));

        path.Root.Should().BeSameAs(pathRoot);
        path.Relative.Should().Be(Path.Combine("a", "c"));
        path.Full.Should().Be(Path.Combine(RootDirectory, "a", "c"));
        absolute.Should().Be(path);
        absolute.Root.Should().BeSameAs(pathRoot);
    }

    [Fact]
    public void Constructor_DefaultRelativeRepresentsRoot()
    {
        var path = new RootedPath(pathRoot);

        path.Root.Should().BeSameAs(pathRoot);
        path.Relative.Should().BeEmpty();
        path.Full.Should().Be(RootDirectory);
    }

    [Fact]
    public void EqualityAndHashCode_CompareRootsAndRelativePathsIgnoringCase()
    {
        var path = pathRoot.Combine("a", "b");
        var equivalent = new PathRoot(fs.DirectoryInfo.New(RootDirectory.ToUpperInvariant())).Combine("A", "B");
        var differentRelative = pathRoot.Combine("a", "c");
        var differentRoot = new PathRoot(fs.DirectoryInfo.New(Path.Combine(RootDirectory, "a"))).Combine("b");

        path.Equals(equivalent).Should().BeTrue();
        path.GetHashCode().Should().Be(equivalent.GetHashCode());
        path.Equals(differentRelative).Should().BeFalse();
        path.Full.Should().Be(differentRoot.Full);
        path.Equals(differentRoot).Should().BeFalse();
        new HashSet<RootedPath> { path, equivalent, differentRelative, differentRoot }.Should().HaveCount(3);
        path.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Constructor_RejectsPathsOutsideRoot()
    {
        var parent = () => new RootedPath(pathRoot, "..");
        var traversal = () => new RootedPath(pathRoot, Path.Combine("..", "a"));
        var absolute = () => new RootedPath(pathRoot, Path.Combine(RootDirectory + "-other", "a"));

        parent.Should().Throw<ArgumentException>();
        traversal.Should().Throw<ArgumentException>();
        absolute.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Combine_SingleAndMultiplePathsResolveAgainstCurrentPath()
    {
        var current = pathRoot.Combine("a", "b");

        var single = current.Combine(Path.Combine("..", "c"));
        var multiple = current.Combine("..", "", "c");

        single.Root.Should().BeSameAs(pathRoot);
        single.Relative.Should().Be(Path.Combine("a", "c"));
        single.Full.Should().Be(Path.Combine(RootDirectory, "a", "c"));
        multiple.Should().Be(single);
        multiple.Root.Should().BeSameAs(pathRoot);
    }

    [Fact]
    public void Combine_WithoutPathsPreservesCurrentPath()
    {
        var current = new PathRoot(fs.DirectoryInfo.New(RootDirectory)).Combine("a", "b");

        var path = current.Combine();

        path.Should().Be(current);
        path.Root.Should().BeSameAs(current.Root);
        path.Full.Should().Be(current.Full);
    }

    [Fact]
    public void Combine_RejectsTraversalOutsideRoot()
    {
        var current = new PathRoot(fs.DirectoryInfo.New(RootDirectory)).Combine("a", "b");

        var combine = () => current.Combine("..", "..", "..", "c");

        combine.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parent_ReturnsParentWithinOriginalRoot()
    {
        var path = pathRoot.Combine("a", "b");

        path.Combine("c").Parent().Should().Be(path);
    }

    [Fact]
    public void Parent_AtRootReturnsNull()
    {
        var path = new RootedPath(new PathRoot(fs.DirectoryInfo.New(RootDirectory)));

        path.Parent().Should().BeNull();
    }
}
