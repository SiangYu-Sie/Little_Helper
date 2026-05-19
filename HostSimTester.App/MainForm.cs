using HostSimTester.App.Dialogs;
using HostSimTester.App.Logging;
using HostSimTester.App.Models;
using HostSimTester.App.Pages;
using HostSimTester.App.Services;
using HostSimTester.App.Theme;
using NLog;

namespace HostSimTester.App;

public sealed class MainForm : Form
{
    private readonly Logger _uiLogger = LogManager.GetLogger(LoggerNames.Ui);
    private readonly Secs.SecsConnection _secsConnection = new();
    private readonly TabControl _mainTab = new();

    private readonly ToolStripStatusLabel _lblStatus = new();
    private readonly RichTextBox _logBox = new();
    private readonly IssueReportClient _issueReportClient;

    private ConnectionSettings _settings = new();

    public MainForm()
    {
        Text = "HostSimTester (NET8)";
        Width = 1440;
        Height = 920;
        StartPosition = FormStartPosition.CenterScreen;

        _issueReportClient = new IssueReportClient(_uiLogger);
        BuildUi();
        WireEvents();
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        UiLogTarget.LogReceived -= OnUiLogReceived;
        await _secsConnection.DisposeAsync().ConfigureAwait(true);
        base.OnFormClosed(e);
    }

    private void BuildUi()
    {
        ThemeHelper.ApplyTheme(this);

        var root = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.IceSurface
        };

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = ThemeHelper.NavyPanel,
            Padding = new Padding(8)
        };

        var btnSettings = new Button { Text = "ProvideRequireInfo" };
        var btnConnect = new Button { Text = "SecsConnect" };
        var btnDisconnect = new Button { Text = "Disconnect" };
        var btnHostComm = new Button { Text = "Host S1F13" };
        var btnIssueReport = new Button { Text = "Issue" };
        btnSettings.Click += (_, _) => ShowSettingsDialog();
        btnConnect.Click += async (_, _) => await ConnectAsync().ConfigureAwait(true);
        btnDisconnect.Click += async (_, _) => await DisconnectAsync().ConfigureAwait(true);
        btnHostComm.Click += async (_, _) => await EstablishCommunicationAsync(true).ConfigureAwait(true);
        btnIssueReport.Click += async (_, _) => await ReportIssueAsync().ConfigureAwait(true);

        var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 640 };
        actionPanel.Controls.Add(btnSettings);
        actionPanel.Controls.Add(btnConnect);
        actionPanel.Controls.Add(btnDisconnect);
        actionPanel.Controls.Add(btnHostComm);
        actionPanel.Controls.Add(btnIssueReport);
        ThemeHelper.ApplyButtonTheme(actionPanel);

        topPanel.Controls.Add(actionPanel);

        _mainTab.Dock = DockStyle.Fill;
        AddTab(_mainTab, "L1 Initial", new L1InitialTestPage(_secsConnection));
        AddTab(_mainTab, "L1 Normal Scenario", new L1NormalScenarioPage(_secsConnection));
        AddTab(_mainTab, "L2 Concurrent", new L2ConcurrentRunPage(_secsConnection));
        AddTab(_mainTab, "L2 CancelStopAbort", new L2CancelStopAbortPage(_secsConnection));
        AddTab(_mainTab, "L2 Alarm", new L2AlarmPage(_secsConnection));
        AddTab(_mainTab, "L2 Recipe", new L2RecipePage(_secsConnection));
        AddTab(_mainTab, "L2 Trace", new L2TraceDataCollectionPage(_secsConnection));
        AddTab(_mainTab, "L2 ECVID", new L2EcVidPage(_secsConnection));

        var left = new Panel { Dock = DockStyle.Fill };
        left.Controls.Add(_mainTab);
        left.Controls.Add(topPanel);

        _logBox.Dock = DockStyle.Fill;
        _logBox.ReadOnly = true;
        _logBox.BackColor = ThemeHelper.LogBg;
        _logBox.ForeColor = ThemeHelper.LogText;
        _logBox.Font = new Font("Consolas", 9F);

        root.Controls.Add(left);

        var status = new StatusStrip { BackColor = ThemeHelper.NavyPanel };
        _lblStatus.Text = "Disconnected";
        _lblStatus.ForeColor = ThemeHelper.StatusText;
        status.Items.Add(_lblStatus);

        Controls.Add(root);
        Controls.Add(status);
    }

    private void WireEvents()
    {
        UiLogTarget.LogReceived += OnUiLogReceived;

        _secsConnection.ConnectionStateChanged += state =>
        {
            BeginInvoke(async () =>
            {
                _lblStatus.Text = $"State: {state}";
                if (string.Equals(state, "Selected", StringComparison.OrdinalIgnoreCase))
                {
                    await EstablishCommunicationAsync(false).ConfigureAwait(true);
                }
            });
        };
        _secsConnection.PrimaryMessageReceived += msg =>
        {
            _uiLogger.Info($"Received primary: {msg.PrimaryMessage}");
        };

        Load += (_, _) =>
        {
            ShowSettingsDialog();
            _uiLogger.Info("Application startup");
        };
    }

    private void ShowSettingsDialog()
    {
        using var dialog = new RequireInfoDialog(_settings);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Result is not null)
        {
            _settings = dialog.Result;
            _uiLogger.Info($"ProvideRequireInfo OK ip={_settings.IpAddress} port={_settings.Port} deviceId={_settings.DeviceId}");
        }
        else
        {
            _uiLogger.Info("ProvideRequireInfo Cancel");
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            await _secsConnection.ConnectAsync(_settings).ConfigureAwait(true);
            _lblStatus.Text = "Connecting";
            _uiLogger.Info("SecsConnect command executed");
        }
        catch (Exception ex)
        {
            _uiLogger.Error(ex, "Connect failed");
            _lblStatus.Text = "Connect Failed";
        }
    }

    private async Task DisconnectAsync()
    {
        await _secsConnection.DisconnectAsync().ConfigureAwait(true);
        _lblStatus.Text = "Disconnected";
    }

    private async Task EstablishCommunicationAsync(bool manual)
    {
        try
        {
            var ok = await _secsConnection.EstablishCommunicationAsync().ConfigureAwait(true);
            if (ok)
            {
                _uiLogger.Info(manual
                    ? "Manual Host S1F13 communication established."
                    : "Auto Host S1F13 communication established.");
            }
            else
            {
                _uiLogger.Warn(manual
                    ? "Manual Host S1F13 communication not established."
                    : "Auto Host S1F13 communication not established.");
            }
        }
        catch (Exception ex)
        {
            _uiLogger.Error(ex, manual
                ? "Manual Host S1F13 failed"
                : "Auto Host S1F13 failed");
        }
    }

    private async Task ReportIssueAsync()
    {
        using var dialog = new IssueReportDialog(
            _issueReportClient.Settings.ApiUrl,
            _mainTab.SelectedTab?.Text ?? "Unknown");

        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is null)
        {
            return;
        }

        var logExcerpt = dialog.Result.IncludeRecentLogs
            ? TakeTailLines(_logBox.Text, 220)
            : string.Empty;

        var payload = new IssueReportPayload
        {
            Subject = dialog.Result.Subject ?? string.Empty,
            Description = dialog.Result.Description ?? string.Empty,
            CurrentTab = _mainTab.SelectedTab?.Text ?? "Unknown",
            ConnectionState = _lblStatus.Text ?? string.Empty,
            DeviceId = _settings.DeviceId,
            IpAddress = _settings.IpAddress ?? string.Empty,
            Port = _settings.Port,
            RecentLogs = logExcerpt
        };

        var result = await _issueReportClient.SendAsync(payload).ConfigureAwait(true);
        if (result.Success)
        {
            MessageBox.Show(
                this,
                "Issue report submitted successfully.",
                "Issue Report",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _uiLogger.Info("Issue report submitted: subject={subject}", payload.Subject);
            return;
        }

        MessageBox.Show(
            this,
            $"Issue report failed: {result.Message}",
            "Issue Report",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        _uiLogger.Warn("Issue report failed: {message}", result.Message);
    }

    private static string TakeTailLines(string text, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length <= maxLines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        return string.Join(Environment.NewLine, lines.Skip(lines.Length - maxLines));
    }

    private void OnUiLogReceived(LogLevel level, string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnUiLogReceived(level, text));
            return;
        }

        var color = level.Ordinal switch
        {
            >= 4 => ThemeHelper.LogError,
            3 => ThemeHelper.LogWarn,
            _ => ThemeHelper.LogText
        };

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor = color;
        _logBox.AppendText(text + Environment.NewLine);
        _logBox.SelectionColor = _logBox.ForeColor;
        _logBox.ScrollToCaret();
    }

    private static void AddTab(TabControl tabControl, string title, Control content)
    {
        var page = new TabPage(title)
        {
            BackColor = ThemeHelper.IceSurface
        };
        page.Controls.Add(content);
        tabControl.TabPages.Add(page);
    }
}
