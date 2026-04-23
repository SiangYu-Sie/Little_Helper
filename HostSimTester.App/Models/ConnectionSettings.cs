namespace HostSimTester.App.Models;

public sealed class ConnectionSettings
{
    public ushort DeviceId { get; set; } = 0;
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5001;
}
