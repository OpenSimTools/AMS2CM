using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class ReadOnlyCollectionExtensionsTest
{
    [Fact]
    public void Partition()
    {
        var (t, f) = new []{ 1, 2, 3 }.Partition(i => i % 2 == 0);

        t.Should().ContainInOrder(2);
        f.Should().ContainInOrder(1, 3);
    }
}
