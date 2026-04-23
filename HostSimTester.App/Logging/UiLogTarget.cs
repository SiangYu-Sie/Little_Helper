using NLog;
using NLog.Targets;

namespace HostSimTester.App.Logging;

[Target("UiLogTarget")]
public sealed class UiLogTarget : TargetWithLayout
{
    public static event Action<LogLevel, string>? LogReceived;

    protected override void Write(LogEventInfo logEvent)
    {
        var text = Layout.Render(logEvent);
        LogReceived?.Invoke(logEvent.Level, text);
    }
}
