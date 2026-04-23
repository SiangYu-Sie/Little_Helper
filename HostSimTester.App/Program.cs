using HostSimTester.App.Logging;
using NLog;

namespace HostSimTester.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        AppSession.Initialize();
        LogManager.Setup().LoadConfigurationFromFile("NLog.config");

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}