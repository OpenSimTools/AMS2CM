namespace Core.Packages.Installation.Installers;

public readonly struct ProcessingCallbacks<T>
{
    public ProcessingCallbacks()
    {
    }

    private static readonly Predicate<T> EmptyPredicate = _ => true;
    private static readonly Action<T> EmptyAction = _ => { };

    /// <summary>
    /// Decide if an entry should be processed.
    /// </summary>
    public Predicate<T> Accept { get; init; } = EmptyPredicate;

    /// <summary>
    /// Called before processing an entry.
    /// </summary>
    public Action<T> Before { get; init; } = EmptyAction;

    /// <summary>
    /// Called after processing an entry.
    /// </summary>
    public Action<T> After { get; init; } = EmptyAction;

    /// <summary>
    /// Called if not processing an entry.
    /// </summary>
    public Action<T> NotAccepted { get; init; } = EmptyAction;

    public ProcessingCallbacks<T> AndAccept(Predicate<T> additional) =>
        this with
        {
            Accept = Combine(Accept, additional),
        };

    public ProcessingCallbacks<T> AndBefore(Action<T> additional) =>
        this with
        {
            Before = Combine(Before, additional)
        };

    public ProcessingCallbacks<T> AndAfter(Action<T> additional) =>
        this with
        {
            After = Combine(After, additional)
        };

    public ProcessingCallbacks<T> AndNotAccepted(Action<T> additional) =>
        this with
        {
            NotAccepted = Combine(NotAccepted, additional)
        };

    public ProcessingCallbacks<T> AndFinally(Action<T> additional) =>
        this with
        {
            After = Combine(After, additional),
            NotAccepted = Combine(NotAccepted, additional)
        };

    public ProcessingCallbacks<T> And(ProcessingCallbacks<T> additional) =>
        new()
        {
            Accept = Combine(Accept, additional.Accept),
            Before = Combine(Before, additional.Before),
            After = Combine(After, additional.After),
            NotAccepted = Combine(NotAccepted, additional.NotAccepted)
        };

    private static Predicate<T> Combine(Predicate<T> p1, Predicate<T> p2) =>
        p1 == EmptyPredicate ? p2 : p2 == EmptyPredicate ? p1 : key => p1(key) && p2(key);

    private static Action<T> Combine(Action<T> a1, Action<T> a2) =>
        a1 == EmptyAction ? a2 : a2 == EmptyAction ? a1 : key => { a1(key); a2(key); };
}
