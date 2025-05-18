using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class DependencyResolverTest
{
    private record Item(string Key, string[] Dependencies, string[] Values);

    private IDictionary<string, IReadOnlySet<string>> CollectsValues(IReadOnlyCollection<Item> items) =>
        DependencyResolver.CollectValues(items, i => i.Key, i => i.Dependencies, i => i.Values);

    [Fact]
    public void Transitive_IgnoresMissingDependencies()
    {
        CollectsValues([
            new Item("A", ["B"], ["A1"])
        ]).Should().BeEquivalentTo(new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string>(["A1"])
        });
    }

    [Fact]
    public void Transitive_FollowsDependencyTree()
    {
        CollectsValues([
            new Item("A", ["B"], ["AV"]),
            new Item("B", ["C", "D"], ["BV"]),
            new Item("C", ["E"], []),
            new Item("D", ["E"], ["DV"]),
            new Item("E", [], ["EV"])
        ]).Should().BeEquivalentTo(new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string>(["AV", "BV", "DV", "EV"]),
            ["B"] = new HashSet<string>(["BV", "DV", "EV"]),
            ["C"] = new HashSet<string>(["EV"]),
            ["D"] = new HashSet<string>(["DV", "EV"]),
            ["E"] = new HashSet<string>(["EV"])
        });
    }
}
