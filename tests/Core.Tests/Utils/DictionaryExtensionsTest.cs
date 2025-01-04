using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class DictionaryExtensionsTest
{
    private const int Key = 3;

    [Fact]
    public void Upsert_AddsIfNotExisting()
    {
        var dict = new Dictionary<int, int>();

        dict.Upsert(Key, _ => throw new Exception("Should not have been called"), () => 42);

        dict[Key].Should().Be(42);
    }

    [Fact]
    public void Upsert_UpdatesIfExisting()
    {
        var dict = new Dictionary<int, int>()
        {
            [Key] = 2
        };

        dict.Upsert(Key, _ => _ + 40, () => throw new Exception("Should not have been called"));

        dict[Key].Should().Be(42);
    }
}
