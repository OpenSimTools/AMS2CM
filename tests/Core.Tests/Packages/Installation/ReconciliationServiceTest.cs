using System.Collections.ObjectModel;
using System.IO.Abstractions;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Utils;
using FluentAssertions;
using Moq.Language.Flow;

namespace Core.Tests.Packages.Installation;

[UnitTest]
public class ReconciliationServiceTest
{
    #region Setup

    private const double ProgressTolerance = 0.01;

    private class TestException : Exception;

    private readonly Mock<IReconciliationContext<ReconciliationService.IEventHandler>> context =
        new(MockBehavior.Strict);

    private readonly Mock<ReconciliationService.IEventHandler> events = new(MockBehavior.Strict);
    private readonly Mock<Action<IReadOnlyDictionary<string, PackageInstallationState>>> afterInstall =
        new(MockBehavior.Strict);

    private readonly IBackupStrategyProvider<DateTimeOffset, ReconciliationService.IEventHandler> backups =
        Mock.Of<IBackupStrategyProvider<DateTimeOffset, ReconciliationService.IEventHandler>>();

    private readonly IDirectoryInfo installDir = Mock.Of<IDirectoryInfo>();
    private readonly IPackage[] packages = [Mock.Of<IPackage>()];

    private readonly IReadOnlyDictionary<string, PackageInstallationState> previousState =
        ReadOnlyDictionary<string, PackageInstallationState>.Empty;

    private readonly Dictionary<string, PackageInstallationState> currentState = [];
    private readonly MockSequence sequence = new();

    public ReconciliationServiceTest()
    {
        context.SetupGet(c => c.CurrentState).Returns(currentState);
    }

    #endregion

    [Fact]
    public void Reconcile_EmptyPlanDoesNotCallAfterInstall()
    {
        SetUpActions(count: 0);
        events.InSequence(sequence).Setup(e => e.ReconciliationNoActions());

        Reconcile();

        events.Verify(e => e.ProgressUpdate(It.IsAny<IPercent>()), Times.Never);
        events.Verify(e => e.ReconciliationNoActions(), Times.Once);
        events.Verify(e => e.ReconciliationEnd(It.IsAny<CancellationToken>()), Times.Never);
        afterInstall.VerifyNoOtherCalls();
    }

    [Fact]
    public void Reconcile_ReportsProgressAfterEachAction()
    {
        var actions = SetUpActions(count: 3);
        using var cancellation = new CancellationTokenSource();
        events.InSequence(sequence).Setup(e => e.ReconciliationStart());
        ExpectActionExecuted(actions[0]);
        ExpectProgress(0.33);
        ExpectActionExecuted(actions[1]);
        ExpectProgress(0.66);
        ExpectActionExecuted(actions[2]);
        ExpectProgress(1.00);
        events.InSequence(sequence).Setup(e => e.ReconciliationEnd(cancellation.Token));
        ExpectStateSaved();

        Reconcile(cancellation.Token);

        events.Verify(e => e.ReconciliationEnd(cancellation.Token), Times.Once);
        VerifyStateSaved();
    }

    [Fact]
    public void Reconcile_AlreadyCancelledDoesNotExecuteActions()
    {
        var actions = SetUpActions(count: 1);
        var cancellationToken = new CancellationToken(canceled: true);
        events.InSequence(sequence).Setup(e => e.ReconciliationStart());
        events.InSequence(sequence).Setup(e => e.ReconciliationEnd(cancellationToken));
        ExpectStateSaved();

        Reconcile(cancellationToken);

        actions[0].Verify(a => a(), Times.Never);
        events.Verify(e => e.ReconciliationEnd(cancellationToken), Times.Once);
        events.Verify(e => e.ProgressUpdate(It.IsAny<IPercent>()), Times.Never);
        VerifyStateSaved();
    }

    [Fact]
    public void Reconcile_CancellationStopsBeforeNextAction()
    {
        var actions = SetUpActions(count: 5);
        using var cancellation = new CancellationTokenSource();
        events.InSequence(sequence).Setup(e => e.ReconciliationStart());
        ExpectActionExecuted(actions[0]).Callback(cancellation.Cancel);
        ExpectProgress(0.2);
        events.InSequence(sequence).Setup(e => e.ReconciliationEnd(cancellation.Token));
        ExpectStateSaved();

        Reconcile(cancellation.Token);

        actions[1].Verify(a => a(), Times.Never);
        events.Verify(e => e.ReconciliationEnd(cancellation.Token), Times.Once);
        VerifyStateSaved();
    }

    [Fact]
    public void Reconcile_ContextCreationFailureDoesNotCallAfterInstall()
    {
        var failure = new TestException();

        var reconcile = () => Reconcile(factory: _ => throw failure);

        reconcile.Should().Throw<TestException>().Which.Should().BeSameAs(failure);
        events.VerifyNoOtherCalls();
        context.VerifyNoOtherCalls();
        afterInstall.VerifyNoOtherCalls();
    }

    [Fact]
    public void Reconcile_PlanningFailureDoesNotCallAfterInstall()
    {
        var failure = new TestException();
        context.Setup(c => c.ReconciliationActions(packages, installDir, backups, events.Object)).Throws(failure);

        var reconcile = () => Reconcile();

        reconcile.Should().Throw<TestException>().Which.Should().BeSameAs(failure);
        events.VerifyNoOtherCalls();
        afterInstall.VerifyNoOtherCalls();
    }

    [Fact]
    public void Reconcile_ActionFailureSavesUpdatedState()
    {
        var actions = SetUpActions(count: 2);
        var failure = new TestException();
        events.InSequence(sequence).Setup(e => e.ReconciliationStart());
        ExpectActionExecuted(actions[0]).Callback(() =>
        {
            currentState["Partial"] = new PackageInstallationState(DateTimeOffset.UnixEpoch, 1, true, [], ["File"], []);
            throw failure;
        });
        ExpectStateSaved();

        var reconcile = () => Reconcile();

        reconcile.Should().Throw<TestException>().Which.Should().BeSameAs(failure);
        actions[1].Verify(a => a(), Times.Never);
        events.Verify(e => e.ReconciliationEnd(It.IsAny<CancellationToken>()), Times.Never);
        VerifyStateSaved();
    }

    #region Utility Methods

    private ISetup<Action> ExpectActionExecuted(Mock<Action> action) =>
        action.InSequence(sequence).Setup(a => a());

    private Mock<Action>[] SetUpActions(int count)
    {
        var actions = Enumerable.Range(0, count).Select(_ => new Mock<Action>(MockBehavior.Strict)).ToArray();
        context.Setup(c => c.ReconciliationActions(packages, installDir, backups, events.Object))
            .Returns(actions.Select(action => action.Object).ToArray());
        return actions;
    }

    private void Reconcile(CancellationToken cancellationToken = default) =>
        Reconcile(_ => context.Object, cancellationToken);

    private void Reconcile(IReconciliationContext<ReconciliationService.IEventHandler>.FactoryFunc factory,
        CancellationToken cancellationToken = default) =>
        new ReconciliationService<ReconciliationService.IEventHandler>(backups, factory)
            .Reconcile(previousState, packages, installDir, afterInstall.Object, events.Object, cancellationToken);

    private void ExpectProgress(double fraction) =>
        events.InSequence(sequence).Setup(e =>
            e.ProgressUpdate(It.Is<IPercent>(
                p => Math.Abs(p.Percent - fraction) <= ProgressTolerance)));

    private void ExpectStateSaved()
    {
        afterInstall.InSequence(sequence).Setup(a => a(It.Is<IReadOnlyDictionary<string, PackageInstallationState>>(
            state => ReferenceEquals(state, currentState))));
    }

    private void VerifyStateSaved()
    {
        afterInstall.Verify(a => a(It.Is<IReadOnlyDictionary<string, PackageInstallationState>>(
            state => ReferenceEquals(state, currentState))), Times.Once);
        afterInstall.VerifyNoOtherCalls();
    }

    #endregion
}
