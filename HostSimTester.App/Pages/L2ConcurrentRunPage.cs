namespace HostSimTester.App.Pages;

public sealed class L2ConcurrentRunPage : BaseTestPage
{
    private readonly TextBox _txtCarrierId1;
    private readonly TextBox _txtCarrierId2;
    private readonly NumericUpDown _nudPortId1;
    private readonly NumericUpDown _nudPortId2;
    private readonly TextBox _txtRecipeId;
    private readonly TextBox _txtControlJobId;

    public L2ConcurrentRunPage(Secs.SecsConnection connection)
        : base("L2 Concurrent Run", Logging.LoggerNames.L2Concurrent, connection)
    {
        _txtCarrierId1 = new TextBox { Text = "CARR_P1_001", Width = 120 };
        _nudPortId1 = new NumericUpDown { Minimum = 1, Maximum = 4, Value = 1, Width = 56 };
        _txtCarrierId2 = new TextBox { Text = "CARR_P2_001", Width = 120 };
        _nudPortId2 = new NumericUpDown { Minimum = 1, Maximum = 4, Value = 2, Width = 56 };
        _txtRecipeId = new TextBox { Text = "Trim_2", Width = 88 };
        _txtControlJobId = new TextBox { Text = "CJ001", Width = 88 };

        ConfigureActionPanel(640, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        var tabControl = CreateTabControl();

        // Shared config bar builder
        FlowLayoutPanel MakeCfgBar(Control parent)
        {
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(6, 4, 6, 4),
                BackColor = Theme.ThemeHelper.NavyPanel
            };
            bar.Controls.Add(new Label { Text = "P1 Carrier", Width = 62, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_txtCarrierId1);
            bar.Controls.Add(new Label { Text = "P1 Port", Width = 48, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_nudPortId1);
            bar.Controls.Add(new Label { Text = "P2 Carrier", Width = 62, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_txtCarrierId2);
            bar.Controls.Add(new Label { Text = "P2 Port", Width = 48, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_nudPortId2);
            bar.Controls.Add(new Label { Text = "PPID", Width = 34, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_txtRecipeId);
            bar.Controls.Add(new Label { Text = "CJID", Width = 34, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_txtControlJobId);
            parent.Controls.Add(bar);
            bar.BringToFront();
            return bar;
        }

        // ── Tab 1: Port Loading ──────────────────────────────────────────────
        var tabLoad = CreateTab(tabControl, "Port Loading");
        MakeCfgBar(tabLoad);
        var gridLoad = CreateTwoColumnGrid();
        tabLoad.Controls.Add(gridLoad);

        var port1LoadSection = CreateSection("1. Port1 Loading");
        var port1LoadBody = CreateSectionBody(port1LoadSection);
        port1LoadBody.Controls.Add(new Label { Text = "Port1 carrier ID read → Proceed → SlotMap → Create PJ", Width = 380, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(port1LoadBody, "Port 1 Carrier ID Read Flow",
            async () =>
            {
                await WaitPrimaryAsync("L2Concurrent_WaitS6F11_Port1", 6, 11, 30).ConfigureAwait(true);
                await SendAsync("L2Concurrent_S3F17_Port1", 3, 17,
                    Secs.SecsMessageFactory.S3F17ProceedWithCarrier(_txtCarrierId1.Text.Trim(), (byte)_nudPortId1.Value)).ConfigureAwait(true);
            }, 270);
        AddActionTo(port1LoadBody, "S3F17 Proceed with Slot Map (Port 1)",
            () => SendAsync("L2Concurrent_S3F17_Port1_SlotMap", 3, 17,
                Secs.SecsMessageFactory.S3F17ProceedWithSlotMap(_txtCarrierId1.Text.Trim(), (byte)_nudPortId1.Value)), 270);
        AddActionTo(port1LoadBody, "S16F15 Create PJ (Port 1)",
            () => SendAsync("L2Concurrent_S16F15_CreatePJ_Port1", 16, 15,
                Secs.SecsMessageFactory.S16F15ProcessJobCreate("PJ001", _txtRecipeId.Text.Trim(), _txtCarrierId1.Text.Trim())), 270);

        var port2LoadSection = CreateSection("2. Port2 Loading");
        var port2LoadBody = CreateSectionBody(port2LoadSection);
        port2LoadBody.Controls.Add(new Label { Text = "Port2 carrier ID read → Proceed → SlotMap → Create PJ", Width = 380, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(port2LoadBody, "Port 2 Carrier ID Read Flow",
            async () =>
            {
                await WaitPrimaryAsync("L2Concurrent_WaitS6F11_Port2", 6, 11, 30).ConfigureAwait(true);
                await SendAsync("L2Concurrent_S3F17_Port2", 3, 17,
                    Secs.SecsMessageFactory.S3F17ProceedWithCarrier(_txtCarrierId2.Text.Trim(), (byte)_nudPortId2.Value)).ConfigureAwait(true);
            }, 270);
        AddActionTo(port2LoadBody, "S3F17 Proceed with Slot Map (Port 2)",
            () => SendAsync("L2Concurrent_S3F17_Port2_SlotMap", 3, 17,
                Secs.SecsMessageFactory.S3F17ProceedWithSlotMap(_txtCarrierId2.Text.Trim(), (byte)_nudPortId2.Value)), 270);
        AddActionTo(port2LoadBody, "S16F15 Create PJ (Port 2)",
            () => SendAsync("L2Concurrent_S16F15_CreatePJ_Port2", 16, 15,
                Secs.SecsMessageFactory.S16F15ProcessJobCreate("PJ002", _txtRecipeId.Text.Trim(), _txtCarrierId2.Text.Trim())), 270);

        gridLoad.Controls.Add(port1LoadSection, 0, 0);
        gridLoad.Controls.Add(port2LoadSection, 1, 0);

        // ── Tab 2: Job Start & Processing ────────────────────────────────────
        var tabJob = CreateTab(tabControl, "Job Start && Processing");
        MakeCfgBar(tabJob);
        var gridJob = CreateTwoColumnGrid();
        tabJob.Controls.Add(gridJob);

        var jobStartSection = CreateSection("3. Job Start");
        var jobStartBody = CreateSectionBody(jobStartSection);
        jobStartBody.Controls.Add(new Label { Text = "Create CJ → Start PJ (both ports)", Width = 380, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(jobStartBody, "S3F17 Send Both Ports",
            async () =>
            {
                await SendAsync("L2Concurrent_S3F17_Port1_Only", 3, 17,
                    Secs.SecsMessageFactory.S3F17ProceedWithCarrier(_txtCarrierId1.Text.Trim(), (byte)_nudPortId1.Value)).ConfigureAwait(true);
                await SendAsync("L2Concurrent_S3F17_Port2_Only", 3, 17,
                    Secs.SecsMessageFactory.S3F17ProceedWithCarrier(_txtCarrierId2.Text.Trim(), (byte)_nudPortId2.Value)).ConfigureAwait(true);
            }, 270);
        AddActionTo(jobStartBody, "S14F9 Create Control Job",
            () => SendAsync("L2Concurrent_S14F9_CreateCJ", 14, 9,
                Secs.SecsMessageFactory.S14F9ControlJobCreate(_txtControlJobId.Text.Trim(), _txtCarrierId1.Text.Trim())), 270);
        AddActionTo(jobStartBody, "S16F5 Start Process Job",
            () => SendAsync("L2Concurrent_S16F5_StartProcessJob", 16, 5,
                Secs.SecsMessageFactory.S16F5ProcessJobCommand("PJ001", "START")), 270);

        var jobProcessSection = CreateSection("4. Job Processing (Event Watch)");
        var jobProcessBody = CreateSectionBody(jobProcessSection);
        jobProcessBody.Controls.Add(new Label { Text = "Wait CJ/PJ start, process, end events", Width = 380, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(jobProcessBody, "Wait S6F11 (CJ/PJ Start Event)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_Start", 6, 11, 30), 270);
        AddActionTo(jobProcessBody, "Wait S6F11 (Process Event)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_Process", 6, 11, 30), 270);
        AddActionTo(jobProcessBody, "Wait S6F11 (CJ/PJ End Event)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_End", 6, 11, 30), 270);

        gridJob.Controls.Add(jobStartSection, 0, 0);
        gridJob.Controls.Add(jobProcessSection, 1, 0);
    }
}

