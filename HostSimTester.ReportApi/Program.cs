using System.Net;
using System.Net.Mail;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ReportApiOptions>(builder.Configuration.GetSection("IssueReportApi"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "HostSimTester.ReportApi" }));

app.MapPost("/api/issue-reports", async (
    HttpRequest httpRequest,
    IssueReportRequest request,
    IConfiguration config,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("IssueReportApi");
    var apiOptions = config.GetSection("IssueReportApi").Get<ReportApiOptions>() ?? new ReportApiOptions();
    apiOptions.ApiKey = Environment.GetEnvironmentVariable("HOSTSIM_REPORT_API_KEY")?.Trim() switch
    {
        { Length: > 0 } v => v,
        _ => apiOptions.ApiKey
    };

    if (!ValidateBearerToken(httpRequest, apiOptions.ApiKey))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Description))
    {
        return Results.BadRequest(new { message = "subject and description are required." });
    }

    var targetEmail = string.IsNullOrWhiteSpace(request.TargetEmail)
        ? apiOptions.DefaultTargetEmail
        : request.TargetEmail.Trim();

    if (string.IsNullOrWhiteSpace(targetEmail))
    {
        return Results.BadRequest(new { message = "targetEmail is required." });
    }

    var smtp = config.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();
    smtp.UserName = Environment.GetEnvironmentVariable("HOSTSIM_SMTP_USERNAME")?.Trim() switch
    {
        { Length: > 0 } v => v,
        _ => smtp.UserName
    };
    smtp.Password = Environment.GetEnvironmentVariable("HOSTSIM_SMTP_PASSWORD")?.Trim() switch
    {
        { Length: > 0 } v => v,
        _ => smtp.Password
    };
    smtp.From = Environment.GetEnvironmentVariable("HOSTSIM_SMTP_FROM")?.Trim() switch
    {
        { Length: > 0 } v => v,
        _ => smtp.From
    };

    if (string.IsNullOrWhiteSpace(smtp.Host) || smtp.Port <= 0 || string.IsNullOrWhiteSpace(smtp.From))
    {
        return Results.Problem("SMTP settings are incomplete. Check Smtp section in appsettings.", statusCode: 500);
    }

    try
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(smtp.From),
            Subject = request.Subject.Trim(),
            Body = BuildBody(request),
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        mail.To.Add(targetEmail);

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = smtp.UseDefaultCredentials
        };

        if (!smtp.UseDefaultCredentials && !string.IsNullOrWhiteSpace(smtp.UserName))
        {
            client.Credentials = new NetworkCredential(smtp.UserName, smtp.Password ?? string.Empty);
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(smtp.TimeoutSeconds, 5, 120)));

        await client.SendMailAsync(mail, linkedCts.Token);
        logger.LogInformation("Issue report sent to {TargetEmail}. Subject={Subject}", targetEmail, request.Subject);
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send issue report email.");
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

app.Run();

static bool ValidateBearerToken(HttpRequest request, string configuredApiKey)
{
    if (string.IsNullOrWhiteSpace(configuredApiKey))
    {
        return false;
    }

    if (!request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        return false;
    }

    var header = authHeader.ToString();
    if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var token = header[7..].Trim();
    return string.Equals(token, configuredApiKey, StringComparison.Ordinal);
}

static string BuildBody(IssueReportRequest request)
{
    var sb = new StringBuilder();
    sb.AppendLine("HostSimTester Issue Report");
    sb.AppendLine($"ReportedAtUtc: {DateTime.UtcNow:O}");
    sb.AppendLine($"CurrentTab: {request.CurrentTab}");
    sb.AppendLine($"ConnectionState: {request.ConnectionState}");
    sb.AppendLine($"MachineName: {request.MachineName}");
    sb.AppendLine($"UserName: {request.UserName}");
    sb.AppendLine($"AppName: {request.AppName}");
    sb.AppendLine($"AppVersion: {request.AppVersion}");
    sb.AppendLine($"Secs DeviceId: {request.SecsConnection?.DeviceId}");
    sb.AppendLine($"Secs IpAddress: {request.SecsConnection?.IpAddress}");
    sb.AppendLine($"Secs Port: {request.SecsConnection?.Port}");
    sb.AppendLine();
    sb.AppendLine("Description:");
    sb.AppendLine(request.Description ?? string.Empty);

    if (!string.IsNullOrWhiteSpace(request.RecentLogs))
    {
        sb.AppendLine();
        sb.AppendLine("RecentLogs:");
        sb.AppendLine(request.RecentLogs);
    }

    return sb.ToString();
}

public sealed class ReportApiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultTargetEmail { get; set; } = "siangyu.sie@tlhome.com.tw";
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public bool UseDefaultCredentials { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;
}

public sealed class IssueReportRequest
{
    public string TargetEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public DateTimeOffset ReportedAtUtc { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string CurrentTab { get; set; } = string.Empty;
    public string ConnectionState { get; set; } = string.Empty;
    public SecsConnectionInfo? SecsConnection { get; set; }
    public string RecentLogs { get; set; } = string.Empty;
}

public sealed class SecsConnectionInfo
{
    public ushort DeviceId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
}
