using System.Collections.Immutable;

namespace Core.Utils;

public static class DependencyResolver
{
    /// <summary>
    /// Resolves individual dependencies into transitive ones.
    /// </summary>
    /// <param name="individualDependencies">Individual dependencies</param>
    /// <returns>Transitive dependencies</returns>
    public static IDictionary<string, IReadOnlySet<string>> Transitive(
        IDictionary<string, IReadOnlyCollection<string>> individualDependencies)
    {
        var transitiveDependencies = new Dictionary<string, IReadOnlySet<string>>();

        IEnumerable<string> TransitiveDependencies(string item)
        {
            if (transitiveDependencies.TryGetValue(item, out var td))
            {
                return td;
            }

            if (!individualDependencies.TryGetValue(item, out var id))
            {
                return ImmutableHashSet<string>.Empty;
            }

            td = id.Concat(id.SelectMany(TransitiveDependencies)).ToHashSet();
            transitiveDependencies.Add(item, td);
            return td;
        }

        foreach (var item in individualDependencies.Keys)
        {
            TransitiveDependencies(item);
        }
        return transitiveDependencies;
    }
}
