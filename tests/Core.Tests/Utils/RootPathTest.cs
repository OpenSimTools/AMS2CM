using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class RootPathTest
{
    private static readonly string RootDirectory = Path.Combine(Path.GetTempPath(), nameof(RootPathTest));

    [Fact]
    public void Constructor_NormalizesRelativeRootAgainstCurrentDirectory()
    {
        var relative = Path.Combine("a", ".", "b", "..") + Path.DirectorySeparatorChar;

        var root = new RootPath(relative);

        root.Full.Should().Be(Path.Combine(Directory.GetCurrentDirectory(), "a"));
    }

    [Fact]
    public void EqualityAndHashCode_CompareCanonicalPathsIgnoringCase()
    {
        var root = new RootPath(RootDirectory);
        var equivalent = new RootPath(Path.Combine(RootDirectory.ToUpperInvariant(), "."));
        var different = new RootPath(RootDirectory + "-other");

        (root == equivalent).Should().BeTrue();
        root.Equals(different).Should().BeFalse();
        root.GetHashCode().Should().Be(equivalent.GetHashCode());
        new HashSet<RootPath> { root, equivalent, different }.Should().HaveCount(2);
        root.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Combine_SingleAndMultiplePathsNormalizeWithinRoot()
    {
        var root = new RootPath(RootDirectory);

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
        var root = new RootPath(RootDirectory);

        var path = root.Combine();

        path.Root.Should().BeSameAs(root);
        path.Relative.Should().BeEmpty();
        path.Full.Should().Be(RootDirectory);
    }
}
