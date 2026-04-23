using NLog;
using Secs4Net;

namespace HostSimTester.App.Secs;

public sealed class SecsGemNLogAdapter(Logger logger) : ISecsGemLogger
{
    public void MessageIn(SecsMessage secsMessage, int id) => logger.Info($"Received {secsMessage.S}{secsMessage.F} / ID={id} {secsMessage}");

    public void MessageOut(SecsMessage secsMessage, int id) => logger.Info($"Send {secsMessage.S}{secsMessage.F} / ID={id} {secsMessage}");

    public void Debug(string msg) => logger.Debug(msg);

    public void Info(string msg) => logger.Info(msg);

    public void Warning(string msg) => logger.Warn(msg);

    public void Error(string msg) => logger.Error(msg);

    public void Error(string msg, Exception? ex) => logger.Error(ex, msg);

    public void Error(string msg, SecsMessage? secsMessage, Exception? ex) => logger.Error(ex, $"{msg} / {secsMessage}");
}
