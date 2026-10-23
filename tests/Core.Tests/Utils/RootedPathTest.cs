using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class RootedPathTest
{
    private static readonly string RootDirectory = Path.Combine(Path.GetTempPath(), nameof(RootedPathTest));

    [Fact]
    public void Constructor_NormalizesRelativeAndAbsolutePathsWithinRoot()
    {
        var root = new RootPath(RootDirectory);
        var relative = Path.Combine("a", ".", "b", "..", "c");

        var path = new RootedPath(root, relative);
        var absolute = new RootedPath(root, Path.Combine(RootDirectory, relative));

        path.Root.Should().BeSameAs(root);
        path.Relative.Should().Be(Path.Combine("a", "c"));
        path.Full.Should().Be(Path.Combine(RootDirectory, "a", "c"));
        absolute.Should().Be(path);
        absolute.Root.Should().BeSameAs(root);
    }

    [Fact]
    public void Constructor_DefaultRelativeRepresentsRoot()
    {
        var root = new RootPath(RootDirectory);

        var path = new RootedPath(root);

        path.Root.Should().BeSameAs(root);
        path.Relative.Should().BeEmpty();
        path.Full.Should().Be(RootDirectory);
    }

    [Fact]
    public void EqualityAndHashCode_CompareRootsAndRelativePathsIgnoringCase()
    {
        var root = new RootPath(RootDirectory);
        var path = root.Combine("a", "b");
        var equivalent = new RootPath(RootDirectory.ToUpperInvariant()).Combine("A", "B");
        var differentRelative = root.Combine("a", "c");
        var differentRoot = new RootPath(Path.Combine(RootDirectory, "a")).Combine("b");

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
        var root = new RootPath(RootDirectory);

        var parent = () => new RootedPath(root, "..");
        var traversal = () => new RootedPath(root, Path.Combine("..", "a"));
        var absolute = () => new RootedPath(root, Path.Combine(RootDirectory + "-other", "a"));

        parent.Should().Throw<ArgumentException>();
        traversal.Should().Throw<ArgumentException>();
        absolute.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Combine_SingleAndMultiplePathsResolveAgainstCurrentPath()
    {
        var root = new RootPath(RootDirectory);
        var current = root.Combine("a", "b");

        var single = current.Combine(Path.Combine("..", "c"));
        var multiple = current.Combine("..", "", "c");

        single.Root.Should().BeSameAs(root);
        single.Relative.Should().Be(Path.Combine("a", "c"));
        single.Full.Should().Be(Path.Combine(RootDirectory, "a", "c"));
        multiple.Should().Be(single);
        multiple.Root.Should().BeSameAs(root);
    }

    [Fact]
    public void Combine_WithoutPathsPreservesCurrentPath()
    {
        var current = new RootPath(RootDirectory).Combine("a", "b");

        var path = current.Combine();

        path.Should().Be(current);
        path.Root.Should().BeSameAs(current.Root);
        path.Full.Should().Be(current.Full);
    }

    [Fact]
    public void Combine_RejectsTraversalOutsideRoot()
    {
        var current = new RootPath(RootDirectory).Combine("a", "b");

        var combine = () => current.Combine("..", "..", "..", "c");

        combine.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parent_ReturnsParentWithinOriginalRoot()
    {
        var root = new RootPath(RootDirectory);
        var path = root.Combine("a", "b");

        path.Combine("c").Parent().Should().Be(path);
    }

    [Fact]
    public void Parent_AtRootReturnsNull()
    {
        var path = new RootedPath(new RootPath(RootDirectory));

        path.Parent().Should().BeNull();
    }
}
