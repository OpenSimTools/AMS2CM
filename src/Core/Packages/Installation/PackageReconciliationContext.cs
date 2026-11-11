namespace Core.Packages.Installation;

public class PackageReconciliationContext<TEventHandler>(
    IReadOnlyDictionary<string, PackageInstallationState> currentState)
    : PackageReconciliationContextBase<TEventHandler>(currentState)
    where TEventHandler : IReconciliationContext.IEventHandler
{
    public static IReconciliationContext<TEventHandler>.FactoryFunc Factory() =>
        currentState => new PackageReconciliationContext<TEventHandler>(currentState);

    private protected override IReadOnlyCollection<Action> CreateActions(IEnumerable<IPackage> packages,
        ReconciliationAction<TEventHandler>.Factory actionFactory, TEventHandler eventHandler) =>
        CreatePackageActions(packages.Select(p => p.CreateInstaller()).ToArray(), CurrentState, actionFactory);
}
