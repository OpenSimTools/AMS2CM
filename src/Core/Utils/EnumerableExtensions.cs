namespace Core.Utils;

public static class EnumerableExtensions
{
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self) =>
        self.Select((item, index) => (item, index));

    public static IEnumerable<TOut> SelectNotNull<TIn, TOut>(this IEnumerable<TIn> self, Func<TIn, TOut?> selector) =>
        self.SelectMany(_ => {
            var i = selector(_);
            return i is null ? Enumerable.Empty<TOut>() : Enumerable.Repeat(i, 1);
        });
}