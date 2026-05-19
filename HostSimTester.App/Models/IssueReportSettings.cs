namespace HostSimTester.App.Models;

public sealed class IssueReportSettings
{
    public bool Enabled { get; set; }
    public string ApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string TargetEmail { get; set; } = "siangyu.sie@tlhome.com.tw";
    public int TimeoutSeconds { get; set; } = 20;
}
