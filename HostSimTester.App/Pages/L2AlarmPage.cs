namespace HostSimTester.App.Pages;

public sealed class L2AlarmPage : BaseTestPage
{
    private readonly Label _lblAlarmCount;
    private readonly Label _lblEnabledAlarmCount;

    public L2AlarmPage(Secs.SecsConnection connection)
        : base("L2 Alarm", Logging.LoggerNames.L2Alarm, connection)
    {
        _lblAlarmCount        = DisplayLabel(80);
        _lblEnabledAlarmCount = DisplayLabel(80);

        ConfigureActionPanel(640, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        var tabControl = CreateTabControl();

        // ── Single Tab: Alarm Management ────────────────────────────────────
        var tabAlarm = CreateTab(tabControl, "Alarm Management");
        var grid = CreateTwoColumnGrid();
        tabAlarm.Controls.Add(grid);

        // Left col: Enable / Disable
        var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(0) };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var enableSection = CreateSection("1. Enable All Alarms");
        var enableBody = CreateSectionBody(enableSection);
        AddActionTo(enableBody, "(1.1) Enable All Alarms",
            () => SendAsync("L2Alarm_S5F3_Enable", 5, 3, Secs.SecsMessageFactory.S5F3EnableDisableAlarm(true)), 260);

        var disableSection = CreateSection("2. Diable All Alarms");
        var disableBody = CreateSectionBody(disableSection);
        AddActionTo(disableBody, "(2.1) Disable All Alarms",
            () => SendAsync("L2Alarm_S5F3_Disable", 5, 3, Secs.SecsMessageFactory.S5F3EnableDisableAlarm(false)), 260);

        leftPanel.Controls.Add(enableSection, 0, 0);
        leftPanel.Controls.Add(disableSection, 0, 1);
        grid.Controls.Add(leftPanel, 0, 0);

        // Right col: Query
        var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(0) };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var alarmListSection = CreateSection("3. Query Alarm Information");
        var alarmListBody = CreateSectionBody(alarmListSection);
        AddActionTo(alarmListBody, "(3.1) Query Alarm List (host send S5,F5)", QueryAlarmListAsync, 300);
        alarmListBody.Controls.Add(CountRow("(3.2) Total Count Of Alarms", _lblAlarmCount));

        var enabledAlarmSection = CreateSection("4. Query Enabled Alarm Information");
        var enabledAlarmBody = CreateSectionBody(enabledAlarmSection);
        AddActionTo(enabledAlarmBody, "(4.1) Query Enable Alarm List (host send S5,F7)", QueryEnabledAlarmListAsync, 300);
        enabledAlarmBody.Controls.Add(CountRow("(4.2) Total Count Of Enable Alarms", _lblEnabledAlarmCount));

        rightPanel.Controls.Add(alarmListSection, 0, 0);
        rightPanel.Controls.Add(enabledAlarmSection, 0, 1);
        grid.Controls.Add(rightPanel, 1, 0);
    }

    private async Task QueryAlarmListAsync()
    {
        var reply = await Connection.SendAsync("L2Alarm_S5F5_QueryAlarmList", 5, 5,
            Secs.SecsMessageFactory.S5F5QueryAlarmList()).ConfigureAwait(true);
        var count = reply?.SecsItem?.Count ?? 0;
        _lblAlarmCount.Text = count.ToString();
        AppendResult($"> L2Alarm_S5F5_QueryAlarmList  Total Count = {count}");
        AppendResult($"< Raw: {reply?.SecsItem}");
    }

    private async Task QueryEnabledAlarmListAsync()
    {
        var reply = await Connection.SendAsync("L2Alarm_S5F7_QueryEnabledAlarmList", 5, 7).ConfigureAwait(true);
        var count = reply?.SecsItem?.Count ?? 0;
        _lblEnabledAlarmCount.Text = count.ToString();
        AppendResult($"> L2Alarm_S5F7_QueryEnabledAlarmList  Total Count = {count}");
        AppendResult($"< Raw: {reply?.SecsItem}");
    }

    private static Label DisplayLabel(int width) => new()
    {
        Width = width, Height = 22,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.FromArgb(170, 230, 240),
        ForeColor = Theme.ThemeHelper.TextDark,
        TextAlign = ContentAlignment.MiddleCenter,
        Margin = new Padding(2, 3, 4, 2)
    };

    private static FlowLayoutPanel CountRow(string caption, Label display)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(6, 4, 6, 2) };
        row.Controls.Add(new Label { Text = caption, Width = 190, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 3, 4, 0) });
        row.Controls.Add(display);
        return row;
    }
}

