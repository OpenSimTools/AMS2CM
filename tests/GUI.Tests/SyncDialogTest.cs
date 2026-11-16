using AMS2CM.GUI;
using Xunit;

namespace GUI.Tests;

public class SyncDialogTest
{
    #region Setup

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    #endregion

    [Fact]
    public async Task WaitForOpen_OpeningAllowsWorkToStart()
    {
        var showCompletion = new TaskCompletionSource();

        var canStart = await SyncDialog.WaitForOpenAsync(Task.CompletedTask, showCompletion.Task).WaitAsync(Timeout);

        Assert.True(canStart);
    }

    [Fact]
    public async Task WaitForOpen_FailureBeforeOpeningPropagates()
    {
        var opened = new TaskCompletionSource();
        var exception = new InvalidOperationException("Dialog failed to open.");
        var showTask = Task.FromException(exception);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SyncDialog.WaitForOpenAsync(opened.Task, showTask).WaitAsync(Timeout));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task WaitForOpen_CompletionBeforeOpeningPreventsWork()
    {
        var opened = new TaskCompletionSource();

        var canStart = await SyncDialog.WaitForOpenAsync(opened.Task, Task.CompletedTask).WaitAsync(Timeout);

        Assert.False(canStart);
    }
}
