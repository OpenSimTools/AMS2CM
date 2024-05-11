using Core.Utils;
using Core;

namespace AMS2CM.CLI;

internal class ConsoleEventLogger : BaseEventLogger
{
    public override void ProgressUpdate(IPercent? value)
    {
    }
    protected override void LogMessage(string message) => Console.WriteLine(message);
}