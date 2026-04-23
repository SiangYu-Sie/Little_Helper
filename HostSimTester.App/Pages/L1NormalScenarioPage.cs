namespace HostSimTester.App.Pages;

public sealed class L1NormalScenarioPage : BaseTestPage
{
    private readonly NumericUpDown _nudSvid;
    private readonly TextBox _txtCarrierId;
    private readonly NumericUpDown _nudPortId;
    private readonly TextBox _txtProcessJobId;
    private readonly TextBox _txtRecipeId;
    private readonly TextBox _txtControlJobId;

    public L1NormalScenarioPage(Secs.SecsConnection connection)
        : base("L1 Normal Scenario", Logging.LoggerNames.L1Normal, connection)
    {
        _nudSvid = new NumericUpDown { Minimum = 1, Maximum = uint.MaxValue, Value = 1, Width = 80 };
        _txtCarrierId = new TextBox { Text = "TEST12345", Width = 120 };
        _nudPortId = new NumericUpDown { Minimum = 1, Maximum = 4, Value = 1, Width = 60 };
        _txtProcessJobId = new TextBox { Text = "PJ001", Width = 100 };
        _txtRecipeId = new TextBox { Text = "Trim_2", Width = 100 };
        _txtControlJobId = new TextBox { Text = "CJ001", Width = 100 };

        ConfigureActionPanel(640, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        var tabControl = CreateTabControl();

        // ── Tab 1: Normal Scenario ──────────────────────────────────────────
        var tabNormal = CreateTab(tabControl, "Normal Scenario");
        var gridNormal = CreateTwoColumnGrid();
        tabNormal.Controls.Add(gridNormal);

        // ── Config bar (inline in tab) ──────────────────────────────────────
        var cfgBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(6, 4, 6, 4),
            BackColor = Theme.ThemeHelper.NavyPanel
        };
        foreach (var (lbl, ctrl) in new (string, Control)[]
        {
            ("SVID", _nudSvid), ("Carrier", _txtCarrierId),
            ("Port",  _nudPortId), ("PJID", _txtProcessJobId),
            ("PPID",  _txtRecipeId), ("CJID", _txtControlJobId)
        })
        {
            cfgBar.Controls.Add(new Label { Text = lbl, Width = 34, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            cfgBar.Controls.Add(ctrl);
        }
        tabNormal.Controls.Add(cfgBar);
        cfgBar.BringToFront();

        // Left: 1. Loading Scenario
        var loadingSection = CreateSection("1. Loading Scenario");
        var loadingBody = CreateSectionBody(loadingSection);
        loadingBody.Controls.Add(new Label { Text = "Query port / proceed carrier / slot map", Width = 380, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(loadingBody, "S1F3 Query Port Status (by SVID)",
            () => SendAsync("L1Normal_S1F3_QueryPortTransferState", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest((uint)_nudSvid.Value)), 280);
        AddActionTo(loadingBody, "S3F17 Proceed with Carrier",
            () => SendAsync("L1Normal_S3F17_ProceedWithCarrier", 3, 17,
                Secs.SecsMessageFactory.S3F17ProceedWithCarrier(_txtCarrierId.Text.Trim(), (byte)_nudPortId.Value)), 280);
        AddActionTo(loadingBody, "S3F17 Proceed with Slot Map",
            () => SendAsync("L1Normal_S3F17_ProceedWithSlotMap", 3, 17,
                Secs.SecsMessageFactory.S3F17ProceedWithSlotMap(_txtCarrierId.Text.Trim(), (byte)_nudPortId.Value)), 280);
        AddActionTo(loadingBody, "Wait S6F11 (Carrier/SlotMap Event)",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_Load", 6, 11, 30), 280);

        // Right: 2. Processing Scenario
        var processingSection = CreateSection("2. Processing Scenario");
        var processingBody = CreateSectionBody(processingSection);
        processingBody.Controls.Add(new Label { Text = "PP select / job create / start", Width = 380, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(processingBody, "S2F41 PP Select",
            () => SendAsync("L1Normal_S2F41_PPSelect", 2, 41,
                Secs.SecsMessageFactory.S2F41PPSelect((byte)_nudPortId.Value, _txtRecipeId.Text.Trim())), 280);
        AddActionTo(processingBody, "S2F41 PP Start",
            () => SendAsync("L1Normal_S2F41_PPStart", 2, 41,
                Secs.SecsMessageFactory.S2F41PPStart((byte)_nudPortId.Value)), 280);
        AddActionTo(processingBody, "S16F15 Create Process Job",
            () => SendAsync("L1Normal_S16F15_ProcessJobCreate", 16, 15,
                Secs.SecsMessageFactory.S16F15ProcessJobCreate(
                    _txtProcessJobId.Text.Trim(), _txtRecipeId.Text.Trim(), _txtCarrierId.Text.Trim())), 280);
        AddActionTo(processingBody, "S14F9 Create Control Job",
            () => SendAsync("L1Normal_S14F9_ControlJobCreate", 14, 9,
                Secs.SecsMessageFactory.S14F9ControlJobCreate(_txtControlJobId.Text.Trim(), _txtCarrierId.Text.Trim())), 280);
        AddActionTo(processingBody, "Wait S6F11 (Job Start/End Event)",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_Process", 6, 11, 30), 280);

        gridNormal.Controls.Add(loadingSection, 0, 0);
        gridNormal.Controls.Add(processingSection, 1, 0);

        // ── Tab 2: Unloading & Query ────────────────────────────────────────
        var tabUnload = CreateTab(tabControl, "Unloading && Query");
        var gridUnload = CreateTwoColumnGrid();
        tabUnload.Controls.Add(gridUnload);

        // Left: 3. Unloading Scenario
        var unloadingSection = CreateSection("3. Unloading Scenario");
        var unloadingBody = CreateSectionBody(unloadingSection);
        unloadingBody.Controls.Add(new Label { Text = "Undock / Carrier Release / wait unload events", Width = 380, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(unloadingBody, "S3F17 Carrier Release",
            () => SendAsync("L1Normal_S3F17_CarrierRelease", 3, 17,
                Secs.SecsMessageFactory.S3F17CarrierRelease(_txtCarrierId.Text.Trim(), (byte)_nudPortId.Value)), 280);
        AddActionTo(unloadingBody, "Wait S6F11 (Unload Event)",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_Unload", 6, 11, 30), 280);

        // Right: 4. Job Status Query
        var jobQuerySection = CreateSection("4. Job Status Query");
        var jobQueryBody = CreateSectionBody(jobQuerySection);
        jobQueryBody.Controls.Add(new Label { Text = "Query process job state / list / space", Width = 380, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(jobQueryBody, "S1F3 Query Process Job State",
            () => SendAsync("L1Normal_S1F3_QueryProcessJobState", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(20)), 280);
        AddActionTo(jobQueryBody, "S16F19 Query Process Job List",
            () => SendAsync("L1Normal_S16F19_QueryProcessJobList", 16, 19), 280);
        AddActionTo(jobQueryBody, "S16F21 Query Process Job Create Limit",
            () => SendAsync("L1Normal_S16F21_QueryProcessJobCreateLimit", 16, 21), 280);

        gridUnload.Controls.Add(unloadingSection, 0, 0);
        gridUnload.Controls.Add(jobQuerySection, 1, 0);
    }
}

