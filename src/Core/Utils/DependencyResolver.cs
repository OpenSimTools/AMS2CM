using System.Collections.Immutable;

namespace Core.Utils;

public static class DependencyResolver
{
    /// <summary>
    /// Collects values from transitive dependencies.
    /// </summary>
    /// <param name="items">Items to collect values from</param>
    /// <param name="keySelector">Select item key</param>
    /// <param name="dependenciesSelector">Select item dependencies</param>
    /// <param name="valueSelector">Select item value</param>
    /// <returns>Values from item and transitive dependencies</returns>
    public static IDictionary<TKey, IReadOnlySet<TValue>> CollectValues<TItem, TKey, TValue>(
        IReadOnlyCollection<TItem> items,
        Func<TItem, TKey> keySelector,
        Func<TItem, ICollection<TKey>> dependenciesSelector,
        Func<TItem, ICollection<TValue>> valueSelector) where TKey : notnull
    {
        var itemsByKey = items.ToDictionary(keySelector, i => i);
        var transitiveValuesByKey = new Dictionary<TKey, IReadOnlySet<TValue>>();

        foreach (var key in items.Select(keySelector))
        {
            TransitiveValues(key);
        }
        return transitiveValuesByKey;

        IReadOnlySet<TValue> TransitiveValues(TKey key)
        {
            if (transitiveValuesByKey.TryGetValue(key, out var tv))
            {
                return tv;
            }

            if (!itemsByKey.TryGetValue(key, out var item))
            {
                return ImmutableHashSet<TValue>.Empty;
            }

            var transitiveValues = dependenciesSelector(item)
                .SelectMany(TransitiveValues)
                .Concat(valueSelector(item)).ToHashSet();
            transitiveValuesByKey.Add(key, transitiveValues);

            return transitiveValues;
        }
    }
}
