using NLog;

namespace HostSimTester.App;

public static class AppSession
{
    public static string TimeStamp { get; private set; } = string.Empty;

    public static void Initialize()
    {
        TimeStamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        GlobalDiagnosticsContext.Set("TimeStamp", TimeStamp);
    }
}
