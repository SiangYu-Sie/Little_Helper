namespace HostSimTester.App.Secs;

public sealed record SecsMessageTemplate(
    string Name,
    byte Stream,
    byte Function,
    bool ExpectReply,
    string AutoReply);
