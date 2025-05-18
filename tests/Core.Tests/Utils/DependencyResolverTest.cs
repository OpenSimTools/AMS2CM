using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Utils;

[UnitTest]
public class DependencyResolverTest
{
    [Fact]
    public void Transitive_IgnoresMissingDependencies()
    {
        DependencyResolver
            .Transitive(new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["A"] = new HashSet<string>(["A1"])
            }).Should().BeEquivalentTo(new Dictionary<string, IReadOnlySet<string>>
            {
                ["A"] = new HashSet<string>(["A1"])
            });
    }

    [Fact]
    public void Transitive_FollowsDependencyTree()
    {
        DependencyResolver
            .Transitive(new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["A"] = new HashSet<string>(["B"]),
                ["B"] = new HashSet<string>(["C", "D"]),
                ["C"] = new HashSet<string>(["E"]),
                ["D"] = new HashSet<string>(["E"])
            }).Should().BeEquivalentTo(new Dictionary<string, IReadOnlySet<string>>
            {
                ["A"] = new HashSet<string>(["B", "C", "D", "E"]),
                ["B"] = new HashSet<string>(["C", "D", "E"]),
                ["C"] = new HashSet<string>(["E"]),
                ["D"] = new HashSet<string>(["E"])
            });
    }
}
