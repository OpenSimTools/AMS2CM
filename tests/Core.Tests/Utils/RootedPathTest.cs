using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class RootedPathTest
{
    [Fact]
    public void Equals_ComparesFullPathIgnoringCase()
    {
        var path = new RootedPath("root", "dir\\file.txt");

        path.Equals(new RootedPath("root\\dir", "file.txt")).Should().BeTrue();
        path.Equals(new RootedPath("ROOT", "DIR\\FILE.TXT")).Should().BeTrue();

        path.Equals(new RootedPath("root", "dir\\other.txt")).Should().BeFalse();
        path.Equals(new RootedPath("root", "dir\\..\\dir\\file.txt")).Should().BeFalse();
        path.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_EqualPaths_ReturnsSameHashCode()
    {
        var path = new RootedPath("root", "dir\\file.txt");

        path.GetHashCode().Should().Be(new RootedPath("root\\dir", "file.txt").GetHashCode());
        path.GetHashCode().Should().Be(new RootedPath("ROOT", "DIR\\FILE.TXT").GetHashCode());

        path.GetHashCode().Should().NotBe(new RootedPath("root", "dir\\other.txt").GetHashCode());
        path.GetHashCode().Should().NotBe(new RootedPath("root", "dir\\..\\dir\\file.txt").GetHashCode());
    }
}
