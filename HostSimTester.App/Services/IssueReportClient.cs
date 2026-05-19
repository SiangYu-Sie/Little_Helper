using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HostSimTester.App.Models;
using NLog;

namespace HostSimTester.App.Services;

public sealed class IssueReportClient
{
    private const string SettingsFileName = "reporting.settings.json";
    private readonly Logger _logger;

    public string SettingsPath { get; }
    public IssueReportSettings Settings { get; }

    public IssueReportClient(Logger logger)
    {
        _logger = logger;
        SettingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        Settings = LoadSettings();
    }

    public async Task<IssueReportSendResult> SendAsync(IssueReportPayload payload, CancellationToken cancellationToken = default)
    {
        if (!Settings.Enabled)
        {
            return IssueReportSendResult.Fail($"Issue reporting is disabled. Set Enabled=true in {SettingsPath}.");
        }

        if (string.IsNullOrWhiteSpace(Settings.ApiUrl))
        {
            return IssueReportSendResult.Fail($"Issue reporting API URL is not configured. Update ApiUrl in {SettingsPath}.");
        }

        if (Settings.ApiUrl.Contains("your-company-api.example.com", StringComparison.OrdinalIgnoreCase))
        {
            return IssueReportSendResult.Fail($"Issue reporting API URL is still placeholder. Update ApiUrl in {SettingsPath}.");
        }

        if (!Uri.TryCreate(Settings.ApiUrl, UriKind.Absolute, out var endpoint))
        {
            return IssueReportSendResult.Fail("Issue reporting API URL format is invalid.");
        }

        var timeoutSeconds = Math.Clamp(Settings.TimeoutSeconds, 5, 120);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

        if (!string.IsNullOrWhiteSpace(Settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey);
        }

        var body = new
        {
            targetEmail = Settings.TargetEmail,
            subject = payload.Subject,
            description = payload.Description,
            appName = "HostSimTester.App",
            appVersion = typeof(IssueReportClient).Assembly.GetName().Version?.ToString() ?? "unknown",
            reportedAtUtc = DateTime.UtcNow,
            machineName = Environment.MachineName,
            userName = Environment.UserName,
            currentTab = payload.CurrentTab,
            connectionState = payload.ConnectionState,
            secsConnection = new
            {
                payload.DeviceId,
                payload.IpAddress,
                payload.Port
            },
            recentLogs = payload.RecentLogs
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return IssueReportSendResult.Ok();
            }

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {responseText}";
            return IssueReportSendResult.Fail(message);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Issue report send failed");
            return IssueReportSendResult.Fail(ex.Message);
        }
    }

    private IssueReportSettings LoadSettings()
    {
        try
        {
            IssueReportSettings settings;
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                settings = JsonSerializer.Deserialize<IssueReportSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new IssueReportSettings();
            }
            else
            {
                settings = new IssueReportSettings();
            }

            settings.ApiUrl = Environment.GetEnvironmentVariable("HOSTSIM_REPORT_API_URL")?.Trim() switch
            {
                { Length: > 0 } v => v,
                _ => settings.ApiUrl
            };
            settings.ApiKey = Environment.GetEnvironmentVariable("HOSTSIM_REPORT_API_KEY")?.Trim() switch
            {
                { Length: > 0 } v => v,
                _ => settings.ApiKey
            };

            _logger.Info("Issue reporting settings loaded. Enabled={enabled}, ApiConfigured={configured}",
                settings.Enabled,
                !string.IsNullOrWhiteSpace(settings.ApiUrl));

            return settings;
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Failed to load issue reporting settings; using defaults.");
            return new IssueReportSettings();
        }
    }
}

public sealed class IssueReportPayload
{
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string CurrentTab { get; init; } = string.Empty;
    public string ConnectionState { get; init; } = string.Empty;
    public ushort DeviceId { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public int Port { get; init; }
    public string RecentLogs { get; init; } = string.Empty;
}

public sealed class IssueReportSendResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static IssueReportSendResult Ok() => new() { Success = true };
    public static IssueReportSendResult Fail(string message) => new() { Success = false, Message = message };
}
