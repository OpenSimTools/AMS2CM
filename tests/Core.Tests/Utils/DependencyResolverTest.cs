using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class DependencyResolverTest
{
    private record Item(string Key, string[] Dependencies, string Value);

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> CollectsValues(IReadOnlyCollection<Item> items) =>
        DependencyResolver.CollectValues(
            items.ToDictionary(i => i.Key, i => i),
            i => i.Dependencies,
            i => i?.Value ?? "XXX");

    [Fact]
    public void Transitive_DefaultsMissingDependencies()
    {
        CollectsValues([
            new Item("A", ["B"], "AV")
        ]).Should().BeEquivalentTo(new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string>(["AV", "XXX"])
        });
    }

    [Fact]
    public void Transitive_FollowsDependencyTree()
    {
        CollectsValues([
            new Item("A", ["B"], "AV"),
            new Item("B", ["C"], "BV"),
            new Item("C", [], "CV")
        ]).Should().BeEquivalentTo(new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string>(["AV", "BV", "CV"]),
            ["B"] = new HashSet<string>(["BV", "CV"]),
            ["C"] = new HashSet<string>(["CV"])
        });
    }
}
