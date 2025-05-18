using System.Collections.Immutable;

namespace Core.Utils;

public static class DependencyResolver
{
    /// <summary>
    /// Collects values from transitive dependencies.
    /// </summary>
    /// <param name="items">Items to collect values from</param>
    /// <param name="dependenciesSelector">Select item dependencies</param>
    /// <param name="valueSelector">Select item value</param>
    /// <returns>Values from item and transitive dependencies</returns>
    public static IReadOnlyDictionary<TKey, IReadOnlySet<TValue>> CollectValues<TItem, TKey, TValue>(
        IReadOnlyDictionary<TKey, TItem> items,
        Func<TItem, IReadOnlyCollection<TKey>> dependenciesSelector,
        Func<TItem?, TValue> valueSelector) where TKey : notnull
    {
        var transitiveValuesByKey = new Dictionary<TKey, IReadOnlySet<TValue>>();

        foreach (var key in items.Keys)
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

            var item = items.GetValueOrDefault(key);
            var value = valueSelector(item);

            if (item is null)
            {
                return new HashSet<TValue> { value };
            }

            var transitiveValues = dependenciesSelector(item)
                .SelectMany(TransitiveValues)
                .Append(value).ToHashSet();
            transitiveValuesByKey.Add(key, transitiveValues);

            return transitiveValues;
        }
    }
}
