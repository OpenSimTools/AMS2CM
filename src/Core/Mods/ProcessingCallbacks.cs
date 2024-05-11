namespace Core.Mods;

public struct ProcessingCallbacks<T>
{
    private static readonly Predicate<T> EmptyPredicate = _ => true;
    private static readonly Action<T> EmptyAction = _ => { };

    private Predicate<T>? accept;
    /// <summary>
    /// Decide if an entry should be processed.
    /// </summary>
    public Predicate<T> Accept
    {
        get => accept ?? EmptyPredicate;
        set => accept = value;
    }

    private Action<T>? before;
    /// <summary>
    /// Called before processing an entry.
    /// </summary>
    public Action<T> Before
    {
        get => before ?? EmptyAction;
        set => before = value;
    }

    private Action<T>? after;
    /// <summary>
    /// Called after processing an entry.
    /// </summary>
    public Action<T> After
    {
        get => after ?? EmptyAction;
        set => after = value;
    }

    private Action<T>? notAccepted;
    /// <summary>
    /// Called if not processing an entry.
    /// </summary>
    public Action<T> NotAccepted
    {
        get => notAccepted ?? EmptyAction;
        set => notAccepted = value;
    }

    public ProcessingCallbacks<T> AndAccept(Predicate<T> additional) =>
        new()
        {
            accept = Combine(accept, additional),
            before = before,
            after = after,
            notAccepted = notAccepted
        };

    public ProcessingCallbacks<T> AndBefore(Action<T> additional) =>
        new()
        {
            accept = accept,
            before = Combine(before, additional),
            after = after,
            notAccepted = notAccepted
        };

    public ProcessingCallbacks<T> AndAfter(Action<T> additional) =>
        new()
        {
            accept = accept,
            before = before,
            after = Combine(after, additional),
            notAccepted = notAccepted
        };

    public ProcessingCallbacks<T> AndNotAccepted(Action<T> additional) =>
    new()
    {
        accept = accept,
        before = before,
        after = after,
        notAccepted = Combine(notAccepted, additional)
    };

    private static Predicate<T>? Combine(Predicate<T>? p1, Predicate<T> p2) =>
        p1 is null ? p2 : key => p1(key) && p2(key);

    private static Action<T>? Combine(Action<T>? a1, Action<T> a2) =>
        a1 is null ? a2 : key => { a1(key); a2(key); };
}