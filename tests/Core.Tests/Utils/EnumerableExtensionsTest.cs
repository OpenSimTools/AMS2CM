using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class EnumerableExtensionsTest
{
    [Fact]
    public void WithIndex()
    {
        new []{ "A", "B", "C" }
            .WithIndex()
            .Should().ContainInOrder(("A", 0), ("B", 1), ("C", 2));
    }

    [Fact]
    public void SelectNotNull()
    {
        new[] { 1, 2, 3 }
            .SelectNotNull(i => i % 2 == 0 ? null : i.ToString())
            .Should().ContainInOrder("1", "3");
    }
}
