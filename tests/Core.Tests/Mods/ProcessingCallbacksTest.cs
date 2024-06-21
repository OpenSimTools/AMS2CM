using Core.Mods;
using Moq;

namespace Core.Tests.Mods;

public class ProcessingCallbacksTest
{
    private static readonly int SomeValue = 42;

    [Fact]
    public void Accept_AcceptsByDefault()
    {
        var callbacks = new ProcessingCallbacks<int>();

        Assert.NotNull(callbacks.Accept);
        Assert.True(callbacks.Accept(SomeValue));
    }

    [Fact]
    public void Accept_StopsChainOnFirstRejection()
    {
        var callbacks = new ProcessingCallbacks<int>();
        var mp1 = new Mock<Predicate<int>>();
        var mp2 = new Mock<Predicate<int>>();
        var mp3 = new Mock<Predicate<int>>();

        mp1.Setup(p => p.Invoke(SomeValue)).Returns(true);
        mp2.Setup(p => p.Invoke(SomeValue)).Returns(false);

        callbacks
            .AndAccept(mp1.Object)
            .AndAccept(mp2.Object)
            .AndAccept(mp3.Object)
            .Accept(SomeValue);

        mp1.Verify(a => a.Invoke(SomeValue), Times.Once);
        mp2.Verify(a => a.Invoke(SomeValue), Times.Once);
        mp3.Verify(a => a.Invoke(SomeValue), Times.Never);
    }

    [Fact]
    public void Before_ExecutesAllActionsInChain()
    {
        var callbacks = new ProcessingCallbacks<int>();
        var ma1 = new Mock<Action<int>>();
        var ma2 = new Mock<Action<int>>();

        Assert.NotNull(callbacks.Before);

        callbacks
            .AndBefore(ma1.Object)
            .AndBefore(ma2.Object)
            .Before(SomeValue);

        ma1.Verify(a => a.Invoke(SomeValue), Times.Once);
        ma2.Verify(a => a.Invoke(SomeValue), Times.Once);
    }

    [Fact]
    public void After_ExecutesAllActionsInChain()
    {
        var callbacks = new ProcessingCallbacks<int>();
        var ma1 = new Mock<Action<int>>();
        var ma2 = new Mock<Action<int>>();

        Assert.NotNull(callbacks.After);

        callbacks
            .AndAfter(ma1.Object)
            .AndAfter(ma2.Object)
            .After(SomeValue);

        ma1.Verify(a => a.Invoke(SomeValue), Times.Once);
        ma2.Verify(a => a.Invoke(SomeValue), Times.Once);
    }
}
