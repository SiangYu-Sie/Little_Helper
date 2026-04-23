namespace HostSimTester.App.Pages;

public sealed class L2CancelStopAbortPage : BaseTestPage
{
    private readonly TextBox _txtCarrierId;
    private readonly NumericUpDown _nudPortId;
    private readonly TextBox _txtProcessJobId;
    private readonly TextBox _txtControlJobId;
    private readonly TextBox _txtRecipeId;

    public L2CancelStopAbortPage(Secs.SecsConnection connection)
        : base("L2 Cancel/Stop/Abort", Logging.LoggerNames.L2CancelStopAbort, connection)
    {
        _txtCarrierId    = new TextBox { Text = "CANCEL_001", Width = 110 };
        _nudPortId       = new NumericUpDown { Minimum = 1, Maximum = 4, Value = 1, Width = 50 };
        _txtProcessJobId = new TextBox { Text = "PJ001", Width = 80 };
        _txtControlJobId = new TextBox { Text = "CJ001", Width = 80 };
        _txtRecipeId     = new TextBox { Text = "Trim_2", Width = 90 };

        ConfigureActionPanel(640, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        var tabControl = CreateTabControl();

        // local helpers
        FlowLayoutPanel MakeCfgBar(Control parent)
        {
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 36,
                Padding = new Padding(6, 4, 6, 4),
                BackColor = Theme.ThemeHelper.NavyPanel
            };
            void Lbl(string t, int w) => bar.Controls.Add(new Label
                { Text = t, Width = w, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            Lbl("Carrier", 50); bar.Controls.Add(_txtCarrierId);
            Lbl("Port",    32); bar.Controls.Add(_nudPortId);
            Lbl("PJID",    34); bar.Controls.Add(_txtProcessJobId);
            Lbl("CJID",    34); bar.Controls.Add(_txtControlJobId);
            Lbl("PPID",    34); bar.Controls.Add(_txtRecipeId);
            parent.Controls.Add(bar);
            bar.BringToFront();
            return bar;
        }

        string CID()  => _txtCarrierId.Text.Trim();
        byte   Port() => (byte)_nudPortId.Value;
        string PJID() => _txtProcessJobId.Text.Trim();
        string CJID() => _txtControlJobId.Text.Trim();
        string PPID() => _txtRecipeId.Text.Trim();

        // ─── full-flow section builder ───────────────────────────────────────
        // Adds the 9-step flow shared by PJ/CJ tabs; differs only in step 8-9
        void AddFullFlowSteps(
            FlowLayoutPanel body,
            string tag,
            string step8Label,
            Func<Task> step8Action,
            string step9Label,
            Func<Task> step9Action)
        {
            AddActionTo(body, "1) Testing Carrier ID Read Event",
                () => WaitPrimaryAsync($"{tag}_1_CIDReadEvent", 6, 11, 60), 270);

            // row with two buttons: Proceed With Carrier + Bypass CarrierID Event
            AddActionWithSideBtn(body, "2) Proceed With Carrier",
                () => SendAsync($"{tag}_2_ProceedCarrier", 3, 17,
                    Secs.SecsMessageFactory.S3F17ProceedWithCarrier(CID(), Port())),
                "Bypass CarrierID Event",
                () => SendAsync($"{tag}_2_Bypass", 3, 17,
                    Secs.SecsMessageFactory.S3F17ProceedWithCarrier(CID(), Port())), 220);

            AddActionTo(body, "3) SlotMap Report",
                () => WaitPrimaryAsync($"{tag}_3_SlotMapReport", 6, 11, 60), 270);

            AddActionTo(body, "4) Proceed SlotMap",
                () => SendAsync($"{tag}_4_ProceedSlotMap", 3, 17,
                    Secs.SecsMessageFactory.S3F17ProceedWithSlotMap(CID(), Port())), 270);

            AddActionTo(body, "5) Create ProcessJob & ControlJob",
                async () =>
                {
                    await SendAsync($"{tag}_5_CreatePJ", 16, 15,
                        Secs.SecsMessageFactory.S16F15ProcessJobCreate(PJID(), PPID(), CID())).ConfigureAwait(true);
                    await SendAsync($"{tag}_5_CreateCJ", 14, 9,
                        Secs.SecsMessageFactory.S14F9ControlJobCreate(CJID(), CID())).ConfigureAwait(true);
                }, 270);
            body.Controls.Add(new Label
            {
                Text = "PS. PJ/CJ Auto Start", AutoSize = false, Width = 300, Height = 16,
                ForeColor = Color.Gray, Font = new Font("Microsoft JhengHei UI", 7.5F, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, 0, 12, 2)
            });

            AddActionTo(body, "6) Control Job Start",
                () => WaitPrimaryAsync($"{tag}_6_CJStart", 6, 11, 60), 270);

            AddActionTo(body, "7) Process Job Start",
                () => WaitPrimaryAsync($"{tag}_7_PJStart", 6, 11, 60), 270);

            AddActionTo(body, step8Label, step8Action, 270);
            AddActionTo(body, step9Label, step9Action, 270);
        }

        // ── Tab 1: Cancel Carrier ────────────────────────────────────────────
        var tabCancel = CreateTab(tabControl, "Cancel Carrier");
        MakeCfgBar(tabCancel);
        var gridCancel = CreateTwoColumnGrid();
        tabCancel.Controls.Add(gridCancel);

        // Section (1) CIDReadFail/CancelCarrier
        var cidS = CreateSection("(1) CIDReadFail/CancelCarrier");
        var cidB = CreateSectionBody(cidS);
        AddActionTo(cidB, "1) Testing Carrier ID Read Event",
            () => WaitPrimaryAsync("L2Cancel_CID_1_Wait", 6, 11, 60), 270);
        AddActionTo(cidB, "2) Cancel Carrier",
            () => SendAsync("L2Cancel_CID_2_Cancel", 3, 17,
                Secs.SecsMessageFactory.S3F17CancelCarrier(CID(), Port())), 270);
        AddActionTo(cidB, "3) ReadyToUnload Report",
            () => WaitPrimaryAsync("L2Cancel_CID_3_ReadyUnload", 6, 11, 60), 270);
        AddActionTo(cidB, "4) Unload Complete Report",
            () => WaitPrimaryAsync("L2Cancel_CID_4_UnloadComplete", 6, 11, 60), 270);
        AddActionTo(cidB, "5) ReadyToLoad Report",
            () => WaitPrimaryAsync("L2Cancel_CID_5_ReadyLoad", 6, 11, 60), 270);

        // Section (2) SlotMapFail/CancelCarrier
        var slotS = CreateSection("(2) SlotMapFail/CancelCarrier");
        var slotB = CreateSectionBody(slotS);
        AddActionTo(slotB, "1) Testing Carrier ID Read Event",
            () => WaitPrimaryAsync("L2Cancel_Slot_1_Wait", 6, 11, 60), 270);
        AddActionWithSideBtn(slotB, "2) Proceed With Carrier",
            () => SendAsync("L2Cancel_Slot_2_Proceed", 3, 17,
                Secs.SecsMessageFactory.S3F17ProceedWithCarrier(CID(), Port())),
            "Bypass CarrierID Event",
            () => SendAsync("L2Cancel_Slot_2_Bypass", 3, 17,
                Secs.SecsMessageFactory.S3F17ProceedWithCarrier(CID(), Port())), 220);
        AddActionTo(slotB, "3) SlotMap Report",
            () => WaitPrimaryAsync("L2Cancel_Slot_3_SlotMap", 6, 11, 60), 270);
        AddActionTo(slotB, "4) Cancel Carrier",
            () => SendAsync("L2Cancel_Slot_4_Cancel", 3, 17,
                Secs.SecsMessageFactory.S3F17CancelCarrier(CID(), Port())), 270);
        AddActionTo(slotB, "5) ReadyToUnload Report",
            () => WaitPrimaryAsync("L2Cancel_Slot_5_ReadyUnload", 6, 11, 60), 270);

        gridCancel.Controls.Add(cidS, 0, 0);
        gridCancel.Controls.Add(slotS, 1, 0);

        // ── Tab 2: Process Job Stop/Abort ────────────────────────────────────
        var tabPJ = CreateTab(tabControl, "Process Job Stop/Abort");
        MakeCfgBar(tabPJ);
        var gridPJ = CreateTwoColumnGrid();
        tabPJ.Controls.Add(gridPJ);

        var pjAbortS = CreateSection("(1) ProcessJob Abort");
        var pjAbortB = CreateSectionBody(pjAbortS);
        AddFullFlowSteps(pjAbortB, "PJAbort",
            "8) S16F5 PJ Command ABORT",
            () => SendAsync("L2Cancel_PJAbort_8_Abort", 16, 5,
                Secs.SecsMessageFactory.S16F5ProcessJobCommand(PJID(), "ABORT")),
            "9) Process Job Aborting Report",
            () => WaitPrimaryAsync("L2Cancel_PJAbort_9_Report", 6, 11, 60));

        var pjStopS = CreateSection("(2) ProcessJob Stop");
        var pjStopB = CreateSectionBody(pjStopS);
        AddFullFlowSteps(pjStopB, "PJStop",
            "8) S16F5 PJ Command STOP",
            () => SendAsync("L2Cancel_PJStop_8_Stop", 16, 5,
                Secs.SecsMessageFactory.S16F5ProcessJobCommand(PJID(), "STOP")),
            "9) Process Job Stopping Report",
            () => WaitPrimaryAsync("L2Cancel_PJStop_9_Report", 6, 11, 60));

        gridPJ.Controls.Add(pjAbortS, 0, 0);
        gridPJ.Controls.Add(pjStopS, 1, 0);

        // ── Tab 3: Control Job Stop/Abort ────────────────────────────────────
        var tabCJ = CreateTab(tabControl, "Control Job Stop/Abort");
        MakeCfgBar(tabCJ);
        var gridCJ = CreateTwoColumnGrid();
        tabCJ.Controls.Add(gridCJ);

        var cjAbortS = CreateSection("(1) ControlJob Abort");
        var cjAbortB = CreateSectionBody(cjAbortS);
        AddFullFlowSteps(cjAbortB, "CJAbort",
            "8) S16F27 CJ Command ABORT",
            () => SendAsync("L2Cancel_CJAbort_8_Abort", 16, 27,
                Secs.SecsMessageFactory.S16F27ControlJobCommand(CJID(), 7)),
            "9) Control Job Completed (ABORT) Report",
            () => WaitPrimaryAsync("L2Cancel_CJAbort_9_Report", 6, 11, 60));

        var cjStopS = CreateSection("(2) ControlJob Stop");
        var cjStopB = CreateSectionBody(cjStopS);
        AddFullFlowSteps(cjStopB, "CJStop",
            "8) S16F27 CJ Command STOP",
            () => SendAsync("L2Cancel_CJStop_8_Stop", 16, 27,
                Secs.SecsMessageFactory.S16F27ControlJobCommand(CJID(), 6)),
            "9) Control Job Completed (STOP) Report",
            () => WaitPrimaryAsync("L2Cancel_CJStop_9_Report", 6, 11, 60));

        gridCJ.Controls.Add(cjAbortS, 0, 0);
        gridCJ.Controls.Add(cjStopS, 1, 0);
    }

    /// <summary>Adds a row with lamp + primary action button + secondary (bypass) button.</summary>
    private void AddActionWithSideBtn(
        Control host, string primaryText, Func<Task> primaryAction,
        string sideText, Func<Task> sideAction, int primaryWidth = 196)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = 38,
            Margin = new Padding(6, 4, 6, 4),
            Padding = new Padding(0, 2, 0, 0),
            BackColor = Theme.ThemeHelper.IceSurface,
            WrapContents = false
        };

        var lamp = new Panel
        {
            Width = 16, Height = 16,
            Margin = new Padding(3, 8, 8, 0),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(160, 170, 180)
        };

        var primaryBtn = new Button
        {
            Text = primaryText, Height = 30, Width = primaryWidth,
            Margin = new Padding(0, 0, 4, 0),
            Padding = new Padding(8, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.ThemeHelper.CobaltBlue,
            ForeColor = Color.White,
            Font = new Font("Microsoft JhengHei UI", 8.5F)
        };
        primaryBtn.FlatAppearance.BorderColor = Theme.ThemeHelper.DeepBlue;

        var sideBtn = new Button
        {
            Text = sideText, Height = 30,
            Margin = new Padding(0),
            Padding = new Padding(8, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.ThemeHelper.CobaltBlue,
            ForeColor = Color.White,
            Font = new Font("Microsoft JhengHei UI", 8.5F),
            AutoSize = true
        };
        sideBtn.FlatAppearance.BorderColor = Theme.ThemeHelper.DeepBlue;

        primaryBtn.Click += async (_, _) =>
        {
            lamp.BackColor = Theme.ThemeHelper.LogWarn;
            try { await primaryAction().ConfigureAwait(true); lamp.BackColor = Color.FromArgb(78, 180, 95); }
            catch (Exception ex) { lamp.BackColor = Theme.ThemeHelper.DangerRed; AppendResult($"[ERROR] {ex.Message}"); }
        };

        sideBtn.Click += async (_, _) =>
        {
            lamp.BackColor = Theme.ThemeHelper.LogWarn;
            try { await sideAction().ConfigureAwait(true); lamp.BackColor = Color.FromArgb(78, 180, 95); }
            catch (Exception ex) { lamp.BackColor = Theme.ThemeHelper.DangerRed; AppendResult($"[ERROR] {ex.Message}"); }
        };

        row.Controls.Add(lamp);
        row.Controls.Add(primaryBtn);
        row.Controls.Add(sideBtn);
        host.Controls.Add(row);
    }
}

