using Core.API;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.API;

[UnitTest]
public class BaseEventLoggerTest
{
    #region Setup

    private sealed class TestEventLogger : BaseEventLogger
    {
        public List<string> Messages { get; } = [];

        public override void ProgressUpdate(IPercent? progress)
        {
        }

        protected override void LogMessage(string message) => Messages.Add(message);
    }

    private readonly TestEventLogger logger = new();

    #endregion

    [Fact]
    public void ReconciliationEnd_LogsCompletion()
    {
        logger.ReconciliationEnd(CancellationToken.None);

        logger.Messages.Should().ContainSingle().Which.Should().Be("Synchronization completed.");
    }

    [Fact]
    public void ReconciliationEnd_LogsCancellation()
    {
        logger.ReconciliationEnd(new CancellationToken(canceled: true));

        logger.Messages.Should().ContainSingle().Which.Should().Be("Synchronization aborted.");
    }
}
