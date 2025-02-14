namespace Core.Utils;

public static class ReadOnlyCollectionExtensions
{
    public static (IEnumerable<T>, IEnumerable<T>) Partition<T>(
        this IReadOnlyCollection<T> self, Predicate<T> predicate) =>
        (self.Where(x => predicate(x)), self.Where(x => !predicate(x)));
}
