using NLog;
using Secs4Net;

namespace HostSimTester.App.Pages;

public abstract class BaseTestPage : UserControl
{
    private readonly FlowLayoutPanel _actionPanel;
    private readonly Panel _statusLamp;
    private readonly Label _statusLabel;
    private readonly Label _statsLabel;
    private readonly RichTextBox _resultBox;
    private readonly SplitContainer _pageSplit;
    private readonly List<Panel> _actionLamps = new();
    private readonly Dictionary<string, Panel> _actionLampsByText = new(StringComparer.OrdinalIgnoreCase);
    private int _passCount;
    private int _failCount;
    private int _successStreak;
    private string _lastError = "-";
    protected readonly Logger Logger;
    protected readonly Secs.SecsConnection Connection;
    protected FlowLayoutPanel ActionPanel => _actionPanel;

    protected BaseTestPage(string pageTitle, string loggerName, Secs.SecsConnection connection)
    {
        Connection = connection;
        Logger = LogManager.GetLogger(loggerName);

        Dock = DockStyle.Fill;
        BackColor = Theme.ThemeHelper.LogBg;

        var title = new Label
        {
            Text = pageTitle,
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Theme.ThemeHelper.NavyPanel
        };

        _actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 132,
            Padding = new Padding(8),
            BackColor = Theme.ThemeHelper.LogBg
        };

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(10, 6, 8, 6),
            BackColor = Theme.ThemeHelper.NavyPanel
        };

        statusPanel.Controls.Add(new Label
        {
            Text = "Test Status",
            Width = 72,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.ThemeHelper.StatusText
        });

        _statusLamp = new Panel
        {
            Width = 16,
            Height = 16,
            BackColor = Color.FromArgb(160, 170, 180),
            Margin = new Padding(4, 6, 8, 0),
            BorderStyle = BorderStyle.FixedSingle
        };

        _statusLabel = new Label
        {
            Text = "IDLE",
            Width = 78,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.ThemeHelper.StatusText
        };

        _statsLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.ThemeHelper.StatusText
        };

        var btnResetStats = new Button
        {
            Text = "Reset Stats",
            Width = 92,
            Height = 24,
            Margin = new Padding(10, 2, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.ThemeHelper.CobaltBlue,
            ForeColor = Color.White
        };
        btnResetStats.FlatAppearance.BorderColor = Theme.ThemeHelper.DeepBlue;
        btnResetStats.Click += (_, _) => ResetStats();

        statusPanel.Controls.Add(_statusLamp);
        statusPanel.Controls.Add(_statusLabel);
        statusPanel.Controls.Add(_statsLabel);
        statusPanel.Controls.Add(btnResetStats);

        _resultBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Theme.ThemeHelper.TableBg,
            ForeColor = Theme.ThemeHelper.TextMid,
            Font = new Font("Consolas", 9F)
        };

        _pageSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2,
            BackColor = Theme.ThemeHelper.IceSurface
        };

        var leftPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.ThemeHelper.LogBg
        };

        leftPanel.Controls.Add(statusPanel);
        leftPanel.Controls.Add(_actionPanel);
        leftPanel.Controls.Add(title);

        _pageSplit.Panel1.Controls.Add(leftPanel);
        _pageSplit.Panel2.Controls.Add(_resultBox);

        Controls.Add(_pageSplit);
        _pageSplit.SizeChanged += (_, _) => UpdatePageSplitDistance();
        Load += (_, _) => UpdatePageSplitDistance();
        UpdatePageSplitDistance();
        UpdateStatsText();

        Connection.PrimaryMessageReceived += msg =>
        {
            BeginInvoke(() =>
            {
                var interpreted = Secs.SecsReplyInterpreter.DescribePrimary(msg);
                foreach (var line in interpreted)
                {
                    AppendResult($"[Primary] {line}");
                }
            });
        };
    }

    protected void AddAction(string text, Func<Task> action)
    {
        AddActionTo(_actionPanel, text, action);
    }

    protected void AddActionTo(Control host, string text, Func<Task> action, int buttonWidth = 196, bool showLamp = true)
    {
        var itemPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = 38,
            Margin = new Padding(6, 4, 6, 4),
            Padding = new Padding(0, 2, 0, 0),
            BackColor = Theme.ThemeHelper.IceSurface,
            WrapContents = false
        };

        var actionLamp = new Panel
        {
            Width = 16,
            Height = 16,
            Margin = new Padding(3, 8, 8, 0),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(160, 170, 180),
            Visible = showLamp
        };

        _actionLamps.Add(actionLamp);
        _actionLampsByText[text] = actionLamp;

        var btn = new Button
        {
            Text = text,
            Height = 30,
            Margin = new Padding(0),
            Padding = new Padding(12, 0, 12, 0)
        };

        btn.Click += async (_, _) =>
        {
            SetTestStatus("RUNNING", Theme.ThemeHelper.LogWarn);
            actionLamp.BackColor = Theme.ThemeHelper.LogWarn;
            try
            {
                await action().ConfigureAwait(true);
                _passCount++;
                _successStreak++;
                SetTestStatus("PASS", Color.FromArgb(78, 180, 95));
                actionLamp.BackColor = Color.FromArgb(78, 180, 95);
                UpdateStatsText();
            }
            catch (Exception ex)
            {
                _failCount++;
                _successStreak = 0;
                _lastError = ex.Message;
                Logger.Error(ex, "Action failed");
                AppendResult($"[ERROR] {ex.Message}");
                SetTestStatus("FAIL", Theme.ThemeHelper.DangerRed);
                actionLamp.BackColor = Theme.ThemeHelper.DangerRed;
                UpdateStatsText();
            }
        };

        itemPanel.Controls.Add(actionLamp);
        itemPanel.Controls.Add(btn);
        host.Controls.Add(itemPanel);
        Theme.ThemeHelper.ApplyButtonTheme(itemPanel);
        btn.AutoSize = true;
        btn.AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    protected void ConfigureActionPanel(int height, bool wrapContents = true)
    {
        _actionPanel.Height = height;
        _actionPanel.WrapContents = wrapContents;
    }

    protected void ClearActionItems()
    {
        _actionLamps.Clear();
        _actionLampsByText.Clear();
        _actionPanel.Controls.Clear();
    }

    protected void SetActionLampToPass(string actionText)
    {
        if (_actionLampsByText.TryGetValue(actionText, out var lamp))
        {
            lamp.BackColor = Color.FromArgb(78, 180, 95);
            SetTestStatus("PASS", Color.FromArgb(78, 180, 95));
        }
    }

    protected bool TryGetActionLamp(string actionText, out Panel lamp)
    {
        return _actionLampsByText.TryGetValue(actionText, out lamp!);
    }

    protected async Task SendAsync(string operationName, byte stream, byte function, Item? payload = null, bool expectReply = true)
    {
        var reply = await Connection.SendAsync(operationName, stream, function, payload, expectReply).ConfigureAwait(true);
        Logger.Info("Operation {operation} done", operationName);
        AppendResult($"> {operationName} S{stream}F{function}");
        var interpreted = Secs.SecsReplyInterpreter.Describe(reply);
        foreach (var line in interpreted)
        {
            AppendResult($"< {line}");
            Logger.Info("Reply detail: {detail}", line);
        }

        if (reply is not null && interpreted.Any(x => x.Contains("NAK/ERROR", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{operationName} returned NAK/ERROR.");
        }
    }

    protected async Task<bool> SendAllowNakAsync(string operationName, byte stream, byte function, Item? payload = null, bool expectReply = true)
    {
        var reply = await Connection.SendAsync(operationName, stream, function, payload, expectReply).ConfigureAwait(true);
        Logger.Info("Operation {operation} done", operationName);
        AppendResult($"> {operationName} S{stream}F{function}");
        var interpreted = Secs.SecsReplyInterpreter.Describe(reply);
        foreach (var line in interpreted)
        {
            AppendResult($"< {line}");
            Logger.Info("Reply detail: {detail}", line);
        }

        return reply is null || !interpreted.Any(x => x.Contains("NAK/ERROR", StringComparison.OrdinalIgnoreCase));
    }

    protected async Task WaitPrimaryAsync(string operationName, byte stream, byte function, int timeoutSeconds = 30)
    {
        AppendResult($"> Waiting {operationName} S{stream}F{function}, timeout={timeoutSeconds}s");
        var primary = await Connection.WaitForPrimaryAsync(stream, function, TimeSpan.FromSeconds(timeoutSeconds)).ConfigureAwait(true);
        var interpreted = Secs.SecsReplyInterpreter.DescribePrimary(primary);
        foreach (var line in interpreted)
        {
            AppendResult($"< {line}");
            Logger.Info("Primary detail: {detail}", line);
        }
    }

    protected void AppendResult(string text)
    {
        _resultBox.AppendText($"{DateTime.Now:HH:mm:ss} {text}{Environment.NewLine}");
        _resultBox.ScrollToCaret();
    }

    private void SetTestStatus(string text, Color color)
    {
        _statusLamp.BackColor = color;
        _statusLabel.Text = text;
    }

    private void UpdateStatsText()
    {
        var shortError = _lastError.Length > 48 ? _lastError[..48] + "..." : _lastError;
        _statsLabel.Text = $"PASS={_passCount}  FAIL={_failCount}  STREAK={_successStreak}  LastErr={shortError}";
    }

    private void ResetStats()
    {
        _passCount = 0;
        _failCount = 0;
        _successStreak = 0;
        _lastError = "-";
        SetTestStatus("IDLE", Color.FromArgb(160, 170, 180));
        foreach (var lamp in _actionLamps)
        {
            lamp.BackColor = Color.FromArgb(160, 170, 180);
        }
        UpdateStatsText();
        AppendResult("[INFO] Test stats reset.");
    }

    private void UpdatePageSplitDistance()
    {
        if (_pageSplit.IsDisposed)
        {
            return;
        }

        var width = _pageSplit.ClientSize.Width;
        var minPanel1 = _pageSplit.Panel1MinSize;
        const int minPanel2Width = 280;

        if (width <= minPanel1 + minPanel2Width)
        {
            return;
        }

        // 左側佔 70%，右側 LOG 佔 30%，但最少 280px
        var targetDistance = Math.Max(minPanel1, width - Math.Max(minPanel2Width, (int)(width * 0.30)));
        targetDistance = Math.Min(targetDistance, width - minPanel2Width);
        if (_pageSplit.SplitterDistance != targetDistance)
        {
            _pageSplit.SplitterDistance = targetDistance;
        }
    }

    protected static GroupBox CreateSection(string title)
    {
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = Theme.ThemeHelper.TextDark,
            BackColor = Theme.ThemeHelper.IceSurface,
            Padding = new Padding(6)
        };
    }

    protected static FlowLayoutPanel CreateSectionBody(GroupBox section)
    {
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Theme.ThemeHelper.IceSurface,
            Padding = new Padding(2)
        };
        section.Controls.Add(body);
        return body;
    }

    protected static TableLayoutPanel CreateTwoColumnGrid(int width = 940, int height = 530)
    {
        var grid = new TableLayoutPanel
        {
            Width = width,
            Height = height,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return grid;
    }

    protected static TabPage CreateTab(TabControl tabControl, string name)
    {
        var tab = new TabPage(name)
        {
            BackColor = Theme.ThemeHelper.IceSurface
        };
        tabControl.TabPages.Add(tab);
        return tab;
    }

    protected TabControl CreateTabControl()
    {
        var tabControl = new TabControl
        {
            Width = 1150,
            Height = 600,
            Margin = new Padding(0),
            Padding = new Point(12, 6)
        };
        ActionPanel.Controls.Add(tabControl);
        return tabControl;
    }
}
