namespace HostSimTester.App.Pages;

public sealed class L2ConcurrentRunPage : BaseTestPage
{
    private static readonly string[] JobProcessingSequence =
    [
        "Recipe Start\n(with ChamberID/LotID/SlotID/RecipeID/PJID linked)",
        "Recipe Step Start\n(with RecipeID/Recipe Step Number/PJID linked)",
        "Chamber Start\n(with ChamberID/LotID/SlotID linked)",
        "E90 Wafer Start\n(with LotID/SlotID/RecipeID/PJID linked)",
        "E90 Substrate Location Changed\n(Occupied event with LotID/SlotID/LocationID linked)",
        "E90 Substrate Location Changed\n(UnOccupied event with LotID/SlotID/LocationID linked)",
        "Recipe Step End\n(with RecipeID/Recipe Step Number/PJID linked)",
        "E90 Wafer End\n(with LotID/SlotID/RecipeID/PJID linked)",
        "Chamber End\n(with ChamberID/LotID/SlotID linked)",
        "Recipe End\n(with ChamberID/LotID/SlotID/RecipeID/PJID linked)",
        "Process Job 2 End",
        "Control Job 2 End",
        "Process Job 1 End",
        "Control Job 1 End"
    ];

    private readonly TextBox _txtCarrierId1;
    private readonly TextBox _txtCarrierId2;
    private readonly NumericUpDown _nudPortId1;
    private readonly NumericUpDown _nudPortId2;
    private readonly Dictionary<string, Panel> _eventLamps = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _armedAutoPassEvents = new(StringComparer.OrdinalIgnoreCase);
    private string _lastCarrierId1 = string.Empty;
    private string _lastCarrierId2 = string.Empty;
    private string _lastLocationId1 = string.Empty;
    private string _lastLocationId2 = string.Empty;
    private bool _hasCarrierRead1;
    private bool _hasCarrierRead2;
    private IReadOnlyList<string> _lastContentMapSlotIds1 = Array.Empty<string>();
    private IReadOnlyList<string> _lastContentMapSlotIds2 = Array.Empty<string>();
    private string _lastPj1Id = "PJ001";
    private string _lastPj2Id = "PJ002";
    private string _lastCjId1 = "CJ001";
    private string _lastCjId2 = "CJ002";
    private string _lastRecipeId1 = "Trim_2";
    private string _lastRecipeId2 = "Trim_2";
    private int _jobProcessingIndex;

    public L2ConcurrentRunPage(Secs.SecsConnection connection)
        : base("L2 Concurrent Run", Logging.LoggerNames.L2Concurrent, connection)
    {
        _txtCarrierId1 = new TextBox { Text = "CARR_P1_001", Width = 110 };
        _nudPortId1 = new NumericUpDown { Minimum = 1, Maximum = 4, Value = 1, Width = 50 };
        _txtCarrierId2 = new TextBox { Text = "CARR_P2_001", Width = 110 };
        _nudPortId2 = new NumericUpDown { Minimum = 1, Maximum = 4, Value = 2, Width = 50 };

        Connection.PrimaryMessageReceived += OnPrimaryMessageReceived;
        Disposed += (_, _) => Connection.PrimaryMessageReceived -= OnPrimaryMessageReceived;

        ConfigureActionPanel(680, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        var tabControl = CreateTabControl();

        // ── Tab 1: Loading & Job Start (3 columns) ──────────────────────────
        var tab = CreateTab(tabControl, "Loading && Job Start");

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tab.Controls.Add(grid);

        void AddReportRow(Control host, string text, Func<Task> action)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 30,
                Margin = new Padding(6, 2, 6, 2),
                Padding = new Padding(0, 1, 0, 0),
                BackColor = Theme.ThemeHelper.TableBg,
                WrapContents = false
            };

            var lamp = new Panel
            {
                Width = 16,
                Height = 16,
                Margin = new Padding(3, 5, 8, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            _eventLamps[text] = lamp;

            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0),
                Cursor = Cursors.Hand,
                Padding = new Padding(2, 0, 2, 0),
                BorderStyle = BorderStyle.FixedSingle
            };

            async Task RunAsync()
            {
                ArmAutoPass(text);
                lamp.BackColor = Theme.ThemeHelper.LogWarn;
                try
                {
                    await action().ConfigureAwait(true);
                    lamp.BackColor = Color.FromArgb(78, 180, 95);
                }
                catch (Exception ex)
                {
                    lamp.BackColor = Theme.ThemeHelper.DangerRed;
                    AppendResult($"[ERROR] {ex.Message}");
                }
            }

            lbl.Click += async (_, _) => await RunAsync();
            row.Click += async (_, _) => await RunAsync();
            row.Controls.Add(lamp);
            row.Controls.Add(lbl);
            host.Controls.Add(row);
        }

        // 不可點擊的事件列（純顯示）
        void AddPassiveRow(Control host, string text)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 30,
                Margin = new Padding(6, 2, 6, 2),
                Padding = new Padding(0, 1, 0, 0),
                BackColor = Theme.ThemeHelper.TableBg,
                WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16,
                Height = 16,
                Margin = new Padding(3, 5, 8, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            _eventLamps[text] = lamp;
            row.Controls.Add(lamp);
            row.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 0, 0)
            });
            host.Controls.Add(row);
        }

        // 不可點擊事件列 + 旁邊一顆小按鈕
        void AddPassiveRowWithSideButton(Control host, string text, string btnText, Func<Task> btnAction)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 30,
                Margin = new Padding(6, 2, 6, 2),
                Padding = new Padding(0, 1, 0, 0),
                BackColor = Theme.ThemeHelper.TableBg,
                WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16,
                Height = 16,
                Margin = new Padding(3, 5, 8, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            _eventLamps[text] = lamp;
            row.Controls.Add(lamp);
            row.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 6, 0)
            });
            var btn = new Button
            {
                Text = btnText,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 24,
                Margin = new Padding(0, 2, 0, 0),
                Padding = new Padding(8, 0, 8, 0)
            };
            btn.Click += async (_, _) =>
            {
                try { await btnAction().ConfigureAwait(true); }
                catch (Exception ex)
                {
                    lamp.BackColor = Theme.ThemeHelper.DangerRed;
                    AppendResult($"[ERROR] {ex.Message}");
                }
            };
            row.Controls.Add(btn);
            host.Controls.Add(row);
            Theme.ThemeHelper.ApplyButtonTheme(row);
        }

        void RegisterActionLampAsEvent(string text)
        {
            if (TryGetActionLamp(text, out var lamp))
            {
                _eventLamps[text] = lamp;
            }
        }

        // ── Col 0: Port1 Loading + Port2 Loading (stacked) ─────────────────
        var port1Section = CreateSection("1. Port1 Loading");
        var port1Body = CreateSectionBody(port1Section);
        AddActionTo(port1Body, "1) Testing Port1 Carrier ID Read Event",
            () =>
            {
                ArmAutoPass("1) Testing Port1 Carrier ID Read Event");
                ArmAutoPass("3) SlotMap Event");
                return WaitCarrierIdReadAndProceedAsync(
                    1,
                    _txtCarrierId1,
                    _nudPortId1,
                    "L2Concurrent_Wait_S6F11_P1_CarrierIDRead",
                    "L2Concurrent_S3F17_P1_ProceedWithCarrier",
                    "2) Proceed With Carrier");
            }, 260);
            RegisterActionLampAsEvent("1) Testing Port1 Carrier ID Read Event");
        AddPassiveRowWithSideButton(port1Body, "2) Proceed With Carrier", "Send Proceed With Carrier",
            () => SendProceedWithCarrierAsync(
                "L2Concurrent_S3F17_P1_ProceedWithCarrier",
                _txtCarrierId1.Text.Trim(),
                (byte)_nudPortId1.Value,
                "2) Proceed With Carrier"));
        AddPassiveRow(port1Body, "3) SlotMap Event");
        AddActionTo(port1Body, "4) Proceed SlotMap",
            async () =>
            {
                using var dlg = new Dialogs.S3F17ProceedDialog(_txtCarrierId1.Text.Trim(), (byte)_nudPortId1.Value);
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                _txtCarrierId1.Text = dlg.CarrierId;
                _nudPortId1.Value = dlg.PortId;
                _lastContentMapSlotIds1 = NormalizeSlotIds(dlg.OccupiedSlotIds);
                await SendAsync("L2Concurrent_S3F17_P1_ProceedSlotMap", 3, 17,
                    BuildProceedWithCarrierPayload(dlg)).ConfigureAwait(true);
            }, 260);
        AddActionTo(port1Body, "5) Create ProcessJob (PJ1) & ControlJob (CJ1)",
            async () =>
            {
                using var prepDlg = new Dialogs.PpSelectStartPjCjDialog(
                    _txtCarrierId1.Text.Trim(), _lastPj1Id, _lastRecipeId1, _lastCjId1, _lastContentMapSlotIds1);
                if (prepDlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                _txtCarrierId1.Text = prepDlg.CarrierId;
                var pjId = string.IsNullOrWhiteSpace(prepDlg.ProcessJobId) ? _lastPj1Id : prepDlg.ProcessJobId;
                var ppid = string.IsNullOrWhiteSpace(prepDlg.Ppid) ? _lastRecipeId1 : prepDlg.Ppid;
                var cjId = string.IsNullOrWhiteSpace(prepDlg.ControlJobId) ? _lastCjId1 : prepDlg.ControlJobId;
                var slotIds = prepDlg.GetSlotIds();
                _lastPj1Id = pjId;
                _lastCjId1 = cjId;
                _lastRecipeId1 = ppid;
                AppendResult($"[INFO] Prepare PJ/CJ Port1: CarrierID={prepDlg.CarrierId}, PJID={pjId}, CJID={cjId}, RecipeID={ppid}, AutoStart=False, SlotIDs=[{string.Join(", ", slotIds)}]");
                ArmAutoPass("ControlJob 1 Start");
                ArmAutoPass("Process Job 1 Waiting For Start Event");
                await SendAsync("L2Concurrent_S16F15_CreatePJ1", 16, 15,
                    Secs.SecsMessageFactory.S16F15ProcessJobCreate(pjId, ppid, prepDlg.CarrierId, slotIds, autoStart: false)).ConfigureAwait(true);
                var processOrderMgmt = byte.TryParse(prepDlg.ProcessOrderMgmt, out var pom) ? pom : (byte)2;
                await SendAsync("L2Concurrent_S14F9_CreateCJ1", 14, 9,
                    Secs.SecsMessageFactory.S14F9ControlJobCreate(cjId, prepDlg.CarrierId, [pjId], processOrderMgmt)).ConfigureAwait(true);
            }, 260);
        port1Body.Controls.Add(new Label
        {
            Text = "PS. PJ 1 Manual Start",
            Width = 230,
            ForeColor = Theme.ThemeHelper.TextMid,
            Font = new Font("Microsoft JhengHei UI", 8.5F, FontStyle.Italic),
            Margin = new Padding(6, 2, 6, 2)
        });

        var port2Section = CreateSection("2. Port2 Loading");
        var port2Body = CreateSectionBody(port2Section);
        AddActionTo(port2Body, "6) Testing Port2 Carrier ID Read Event",
            () =>
            {
                ArmAutoPass("6) Testing Port2 Carrier ID Read Event");
                ArmAutoPass("8) SlotMap Event");
                return WaitCarrierIdReadAndProceedAsync(
                    2,
                    _txtCarrierId2,
                    _nudPortId2,
                    "L2Concurrent_Wait_S6F11_P2_CarrierIDRead",
                    "L2Concurrent_S3F17_P2_ProceedWithCarrier",
                    "7) Proceed With Carrier");
            }, 260);
            RegisterActionLampAsEvent("6) Testing Port2 Carrier ID Read Event");
        AddPassiveRowWithSideButton(port2Body, "7) Proceed With Carrier", "Send Proceed With Carrier",
            () => SendProceedWithCarrierAsync(
                "L2Concurrent_S3F17_P2_ProceedWithCarrier",
                _txtCarrierId2.Text.Trim(),
                (byte)_nudPortId2.Value,
                "7) Proceed With Carrier"));
        AddPassiveRow(port2Body, "8) SlotMap Event");
        AddActionTo(port2Body, "9) Proceed SlotMap",
            async () =>
            {
                using var dlg = new Dialogs.S3F17ProceedDialog(_txtCarrierId2.Text.Trim(), (byte)_nudPortId2.Value);
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                _txtCarrierId2.Text = dlg.CarrierId;
                _nudPortId2.Value = dlg.PortId;
                _lastContentMapSlotIds2 = NormalizeSlotIds(dlg.OccupiedSlotIds);
                await SendAsync("L2Concurrent_S3F17_P2_ProceedSlotMap", 3, 17,
                    BuildProceedWithCarrierPayload(dlg)).ConfigureAwait(true);
            }, 260);
        AddActionTo(port2Body, "10) Create ProcessJob (PJ2) & ControlJob (CJ2)",
            async () =>
            {
                using var prepDlg = new Dialogs.PpSelectStartPjCjDialog(
                    _txtCarrierId2.Text.Trim(), _lastPj2Id, _lastRecipeId2, _lastCjId2, _lastContentMapSlotIds2);
                if (prepDlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                _txtCarrierId2.Text = prepDlg.CarrierId;
                var pjId = string.IsNullOrWhiteSpace(prepDlg.ProcessJobId) ? _lastPj2Id : prepDlg.ProcessJobId;
                var ppid = string.IsNullOrWhiteSpace(prepDlg.Ppid) ? _lastRecipeId2 : prepDlg.Ppid;
                var cjId = string.IsNullOrWhiteSpace(prepDlg.ControlJobId) ? _lastCjId2 : prepDlg.ControlJobId;
                var slotIds = prepDlg.GetSlotIds();
                _lastPj2Id = pjId;
                _lastCjId2 = cjId;
                _lastRecipeId2 = ppid;
                AppendResult($"[INFO] Prepare PJ/CJ Port2: CarrierID={prepDlg.CarrierId}, PJID={pjId}, CJID={cjId}, RecipeID={ppid}, AutoStart=True, SlotIDs=[{string.Join(", ", slotIds)}]");
                ArmAutoPass("ControlJob 2 Start");
                ArmAutoPass("ProcessJob 2 Start (PJ Auto Start)");
                ArmJobProcessingEvents();
                await SendAsync("L2Concurrent_S16F15_CreatePJ2", 16, 15,
                    Secs.SecsMessageFactory.S16F15ProcessJobCreate(pjId, ppid, prepDlg.CarrierId, slotIds, autoStart: true)).ConfigureAwait(true);
                var processOrderMgmt = byte.TryParse(prepDlg.ProcessOrderMgmt, out var pom) ? pom : (byte)2;
                await SendAsync("L2Concurrent_S14F9_CreateCJ2", 14, 9,
                    Secs.SecsMessageFactory.S14F9ControlJobCreate(cjId, prepDlg.CarrierId, [pjId], processOrderMgmt)).ConfigureAwait(true);
            }, 260);
        port2Body.Controls.Add(new Label
        {
            Text = "PS. PJ 2 Auto Start",
            Width = 230,
            ForeColor = Theme.ThemeHelper.TextMid,
            Font = new Font("Microsoft JhengHei UI", 8.5F, FontStyle.Italic),
            Margin = new Padding(6, 2, 6, 2)
        });

        // ── Col 2: Job Start ────────────────────────────────────────────────
        var jobStartSection = CreateSection("3. Job Start");
        var jobStartBody = CreateSectionBody(jobStartSection);
        AddReportRow(jobStartBody, "ControlJob 1 Start",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_CJ1Start", 6, 11, 30));
        AddReportRow(jobStartBody, "Process Job 1 Waiting For Start Event",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_PJ1WaitStart", 6, 11, 30));
        AddReportRow(jobStartBody, "ControlJob 2 Queued",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_CJ2Queued", 6, 11, 30));
        AddReportRow(jobStartBody, "ControlJob 2 Start",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_CJ2Start", 6, 11, 30));
        AddReportRow(jobStartBody, "ProcessJob 2 Start (PJ Auto Start)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_PJ2AutoStart", 6, 11, 30));
        AddActionTo(jobStartBody, "Start Process Job (PJ1)",
            async () =>
            {
                using var dlg = new Dialogs.ProcessJobCommandDialog(_lastPj1Id, "START");
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                _lastPj1Id = dlg.ProcessJobId;
                ArmAutoPass("ProcessJob 1 Start (PJ Manual Start)");
                ArmJobProcessingEvents();
                await SendAsync("L2Concurrent_S16F5_StartPJ1", 16, 5,
                    Secs.SecsMessageFactory.S16F5ProcessJobCommand(dlg.ProcessJobId, dlg.Command)).ConfigureAwait(true);
            }, 220);
        AddReportRow(jobStartBody, "ProcessJob 1 Start (PJ Manual Start)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_PJ1ManualStart", 6, 11, 30));

        // ── Col 2: Job Processing ───────────────────────────────────────────
        var jobProcSection = CreateSection("4. Job Processing");
        var jobProcBody = CreateSectionBody(jobProcSection);
        AddReportRow(jobProcBody, "Recipe Start\n(with ChamberID/LotID/SlotID/RecipeID/PJID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_RecipeStart", 6, 11, 30));
        AddReportRow(jobProcBody, "Recipe Step Start\n(with RecipeID/Recipe Step Number/PJID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_RecipeStepStart", 6, 11, 30));
        AddReportRow(jobProcBody, "Chamber Start\n(with ChamberID/LotID/SlotID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_ChamberStart", 6, 11, 30));
        AddReportRow(jobProcBody, "E90 Wafer Start\n(with LotID/SlotID/RecipeID/PJID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_E90WaferStart", 6, 11, 30));

        // ── Col 3: Job Processing (continued) ──────────────────────────────
        var jobProcContSection = CreateSection("4. Job Processing (continued)");
        var jobProcContBody = CreateSectionBody(jobProcContSection);
        AddReportRow(jobProcContBody, "E90 Substrate Location Changed\n(Occupied event with LotID/SlotID/LocationID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_E90SubLocOccupied", 6, 11, 30));
        AddReportRow(jobProcContBody, "E90 Substrate Location Changed\n(UnOccupied event with LotID/SlotID/LocationID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_E90SubLocUnoccupied", 6, 11, 30));
        AddReportRow(jobProcContBody, "Recipe Step End\n(with RecipeID/Recipe Step Number/PJID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_RecipeStepEnd", 6, 11, 30));
        AddReportRow(jobProcContBody, "E90 Wafer End\n(with LotID/SlotID/RecipeID/PJID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_E90WaferEnd", 6, 11, 30));
        AddReportRow(jobProcContBody, "Chamber End\n(with ChamberID/LotID/SlotID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_ChamberEnd", 6, 11, 30));
        AddReportRow(jobProcContBody, "Recipe End\n(with ChamberID/LotID/SlotID/RecipeID/PJID linked)",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_RecipeEnd", 6, 11, 30));
        AddReportRow(jobProcContBody, "Process Job 2 End",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_PJ2End", 6, 11, 30));
        AddReportRow(jobProcContBody, "Control Job 2 End",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_CJ2End", 6, 11, 30));
        AddReportRow(jobProcContBody, "Process Job 1 End",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_PJ1End", 6, 11, 30));
        AddReportRow(jobProcContBody, "Control Job 1 End",
            () => WaitPrimaryAsync("L2Concurrent_Wait_S6F11_CJ1End", 6, 11, 30));

        grid.Controls.Add(port1Section, 0, 0);
        grid.Controls.Add(port2Section, 1, 0);
        grid.Controls.Add(jobStartSection, 2, 0);

        // ── Tab 2: Job Processing (2 columns) ──────────────────────────────
        var tab2 = CreateTab(tabControl, "Job Processing");

        var grid2 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        grid2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid2.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tab2.Controls.Add(grid2);

        grid2.Controls.Add(jobProcSection, 0, 0);
        grid2.Controls.Add(jobProcContSection, 1, 0);
    }

    private async Task WaitCarrierIdReadAndProceedAsync(
        byte expectedPortId,
        TextBox carrierIdTextBox,
        NumericUpDown portIdInput,
        string waitOperationName,
        string proceedOperationName,
        string proceedActionText)
    {
        if (await TryUseCachedCarrierReadAndProceedAsync(expectedPortId, carrierIdTextBox, portIdInput, proceedOperationName, proceedActionText).ConfigureAwait(true))
        {
            return;
        }

        var deadline = DateTime.UtcNow.AddSeconds(30);
        AppendResult($"> Waiting {waitOperationName} S6F11 CarrierIDRead, timeout=30s");

        while (true)
        {
            if (await TryUseCachedCarrierReadAndProceedAsync(expectedPortId, carrierIdTextBox, portIdInput, proceedOperationName, proceedActionText).ConfigureAwait(true))
            {
                return;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException($"Timed out waiting for CarrierIDRead on Port {expectedPortId}.");
            }

            var waitSlice = remaining > TimeSpan.FromSeconds(1)
                ? TimeSpan.FromSeconds(1)
                : remaining;

            Secs4Net.PrimaryMessageWrapper primary;
            try
            {
                primary = await Connection.WaitForPrimaryAsync(6, 11, waitSlice).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                continue;
            }

            var interpreted = Secs.SecsReplyInterpreter.DescribePrimary(primary);
            foreach (var line in interpreted)
            {
                AppendResult($"< {line}");
                Logger.Info("Primary detail: {detail}", line);
            }

            if (!TryExtractCarrierReadInfo(primary.PrimaryMessage, out var portId, out var carrierId, out var locationId))
            {
                AppendResult("[INFO] S6F11 received, but it is not CarrierIDRead. Continue waiting.");
                continue;
            }

            if (portId != expectedPortId)
            {
                AppendResult($"[INFO] CarrierIDRead for Port {portId} ignored while waiting Port {expectedPortId}.");
                continue;
            }

            ApplyCarrierReadInfo(portId, carrierId, locationId, "wait");

            var locationText = string.IsNullOrWhiteSpace(locationId) ? string.Empty : $", LocationID={locationId}";
            AppendResult($"[INFO] CarrierIDRead applied: PortID={portId}, CarrierID={carrierId}{locationText}");

            await TrySendProceedWithCarrierAsync(proceedOperationName, carrierId, portId, proceedActionText).ConfigureAwait(true);

            return;
        }
    }

    private async Task<bool> TryUseCachedCarrierReadAndProceedAsync(
        byte expectedPortId,
        TextBox carrierIdTextBox,
        NumericUpDown portIdInput,
        string proceedOperationName,
        string proceedActionText)
    {
        if (!TryGetCachedCarrierRead(expectedPortId, out var cachedCarrierId, out var cachedLocationId))
        {
            return false;
        }

        carrierIdTextBox.Text = cachedCarrierId;
        portIdInput.Value = expectedPortId;
        var cachedLocationText = string.IsNullOrWhiteSpace(cachedLocationId) ? string.Empty : $", LocationID={cachedLocationId}";
        AppendResult($"[INFO] Use cached CarrierIDRead: PortID={expectedPortId}, CarrierID={cachedCarrierId}{cachedLocationText}");
        await TrySendProceedWithCarrierAsync(proceedOperationName, cachedCarrierId, expectedPortId, proceedActionText).ConfigureAwait(true);
        return true;
    }

    private async Task TrySendProceedWithCarrierAsync(string operationName, string carrierId, byte portId, string actionText)
    {
        try
        {
            await SendProceedWithCarrierAsync(operationName, carrierId, portId, actionText).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendResult($"[WARN] CarrierIDRead captured, but auto S3F17 ProceedWithCarrier failed: {ex.Message}");
            throw;
        }
    }

    private async Task SendProceedWithCarrierAsync(string operationName, string carrierId, byte portId, string actionText)
    {
        ArmAutoPass(actionText);
        await SendAsync(operationName, 3, 17,
            Secs.SecsMessageFactory.S3F17ProceedWithCarrier(carrierId, portId)).ConfigureAwait(true);
        MarkL2EventPass(actionText, "S3F17 ProceedWithCarrier sent");
    }

    private static Secs4Net.Item BuildProceedWithCarrierPayload(Dialogs.S3F17ProceedDialog dialog)
    {
        if (dialog.IncludeContentMap && dialog.SlotEntries.Count > 0)
        {
            return Secs.SecsMessageFactory.S3F17ProceedWithCarrierContentMap(
                dialog.CarrierId,
                dialog.PortId,
                dialog.ContentMapCattrid,
                dialog.SlotEntries);
        }

        return Secs.SecsMessageFactory.S3F17ProceedWithCarrier(dialog.CarrierId, dialog.PortId);
    }

    private static IReadOnlyList<string> NormalizeSlotIds(IReadOnlyList<string> slotIds)
    {
        var normalizedSlotIds = slotIds
            .Select(slotId => slotId.Trim())
            .Where(slotId => !string.IsNullOrWhiteSpace(slotId))
            .Distinct()
            .ToArray();

        return normalizedSlotIds;
    }

    private void OnPrimaryMessageReceived(Secs4Net.PrimaryMessageWrapper wrapper)
    {
        var message = wrapper.PrimaryMessage;
        if (message.S != 6 || message.F != 11)
        {
            return;
        }

        var raw = message.ToString();
        var body = Secs.SecsItemFormatter.Format(message.SecsItem);
        var text = raw + Environment.NewLine + body;
        var hasCarrierRead = TryExtractCarrierReadInfo(message, out var portId, out var carrierId, out var locationId);

        BeginInvoke(() =>
        {
            if (hasCarrierRead)
            {
                ApplyCarrierReadInfo(portId, carrierId, locationId, "receive");
            }

            HandleL2S6F11Report(message, text);
        });
    }

    private void HandleL2S6F11Report(Secs4Net.SecsMessage message, string text)
    {
        _ = Secs.SecsPayload.TryGetS6F11Ceid(message, out var ceid);
        var upper = text.ToUpperInvariant();

        if (ceid == 1000)
        {
            return;
        }

        if (IsSlotMapReport(upper))
        {
            MarkL2EventPass("3) SlotMap Event", $"S6F11 CEID={ceid}");
            MarkL2EventPass("8) SlotMap Event", $"S6F11 CEID={ceid}");
            return;
        }

        if (TryDescribeJobTransition(upper, ceid, out var jobTransition))
        {
            AppendResult($"[INFO] {jobTransition}");
        }

        if (TryResolveJobStartAction(upper, out var jobStartAction))
        {
            MarkL2EventPass(jobStartAction, $"S6F11 CEID={ceid}");
            return;
        }

        if (TryResolveJobProcessingAction(upper, out var processingAction))
        {
            MarkL2EventPass(processingAction, $"S6F11 CEID={ceid}");
        }
    }

    private bool TryDescribeJobTransition(string upper, uint ceid, out string description)
    {
        description = string.Empty;

        var isJobRelated = upper.Contains("CJ", StringComparison.Ordinal) ||
            upper.Contains("CONTROLJOB", StringComparison.Ordinal) ||
            upper.Contains("PJ", StringComparison.Ordinal) ||
            upper.Contains("PRJOB", StringComparison.Ordinal) ||
            upper.Contains("PROCESSJOB", StringComparison.Ordinal);
        if (!isJobRelated)
        {
            return false;
        }

        var portText = ResolveJobPortText(upper);
        var cjId = ResolveKnownIdentifier(upper, _lastCjId1, _lastCjId2);
        var pjId = ResolveKnownIdentifier(upper, _lastPj1Id, _lastPj2Id);
        var status = ResolveJobStatusText(upper);
        description = $"Job event: CEID={ceid}, Port={portText}, CJID={cjId}, PJID={pjId}, Status={status}";
        return true;
    }

    private string ResolveJobPortText(string upper)
    {
        if (upper.Contains("LOADPORT1", StringComparison.Ordinal) || upper.Contains("PORTID */\n                    <U1 [1] 1", StringComparison.Ordinal))
        {
            return "1";
        }

        if (upper.Contains("LOADPORT2", StringComparison.Ordinal) || upper.Contains("PORTID */\n                    <U1 [1] 2", StringComparison.Ordinal))
        {
            return "2";
        }

        if (upper.Contains(_lastPj1Id.ToUpperInvariant(), StringComparison.Ordinal) || upper.Contains(_lastCjId1.ToUpperInvariant(), StringComparison.Ordinal))
        {
            return "1";
        }

        if (upper.Contains(_lastPj2Id.ToUpperInvariant(), StringComparison.Ordinal) || upper.Contains(_lastCjId2.ToUpperInvariant(), StringComparison.Ordinal))
        {
            return "2";
        }

        return "?";
    }

    private static string ResolveKnownIdentifier(string upper, string firstId, string secondId)
    {
        if (!string.IsNullOrWhiteSpace(firstId) && upper.Contains(firstId.ToUpperInvariant(), StringComparison.Ordinal))
        {
            return firstId;
        }

        if (!string.IsNullOrWhiteSpace(secondId) && upper.Contains(secondId.ToUpperInvariant(), StringComparison.Ordinal))
        {
            return secondId;
        }

        return "?";
    }

    private static string ResolveJobStatusText(string upper)
    {
        if (upper.Contains("WAIT", StringComparison.Ordinal))
        {
            return "WaitingForStart";
        }

        if (upper.Contains("QUEUED", StringComparison.Ordinal))
        {
            return "Queued";
        }

        if (upper.Contains("MANUAL", StringComparison.Ordinal) && upper.Contains("START", StringComparison.Ordinal))
        {
            return "ManualStart";
        }

        if (upper.Contains("AUTO", StringComparison.Ordinal) && upper.Contains("START", StringComparison.Ordinal))
        {
            return "AutoStart";
        }

        if (upper.Contains("SETTINGUP", StringComparison.Ordinal) || upper.Contains("SETTING UP", StringComparison.Ordinal))
        {
            return "SettingUp";
        }

        if (upper.Contains("POOLED", StringComparison.Ordinal))
        {
            return "Pooled";
        }

        if (upper.Contains("START", StringComparison.Ordinal))
        {
            return "Start";
        }

        if (upper.Contains("END", StringComparison.Ordinal) || upper.Contains("COMPLETE", StringComparison.Ordinal))
        {
            return "Complete";
        }

        return "Unknown";
    }

    private bool TryResolveJobStartAction(string upper, out string actionText)
    {
        actionText = string.Empty;

        if (upper.Contains("QUEUED", StringComparison.Ordinal) &&
            (upper.Contains("CJ2", StringComparison.Ordinal) || upper.Contains("CONTROLJOB 2", StringComparison.Ordinal) || upper.Contains("CONTROLJOB2", StringComparison.Ordinal)))
        {
            actionText = "ControlJob 2 Queued";
            return true;
        }

        if (upper.Contains("CJ1", StringComparison.Ordinal) || upper.Contains("CONTROLJOB 1", StringComparison.Ordinal) || upper.Contains("CONTROLJOB1", StringComparison.Ordinal))
        {
            actionText = "ControlJob 1 Start";
            return true;
        }

        if (upper.Contains("CJ2", StringComparison.Ordinal) || upper.Contains("CONTROLJOB 2", StringComparison.Ordinal) || upper.Contains("CONTROLJOB2", StringComparison.Ordinal))
        {
            actionText = "ControlJob 2 Start";
            return true;
        }

        if (upper.Contains("PJ2", StringComparison.Ordinal))
        {
            actionText = "ProcessJob 2 Start (PJ Auto Start)";
            return true;
        }

        if (upper.Contains("PJ1", StringComparison.Ordinal))
        {
            actionText = upper.Contains("WAIT", StringComparison.Ordinal)
                ? "Process Job 1 Waiting For Start Event"
                : "ProcessJob 1 Start (PJ Manual Start)";
            return true;
        }

        if (upper.Contains("CONTROL", StringComparison.Ordinal) && upper.Contains("START", StringComparison.Ordinal))
        {
            actionText = IsEventPassed("ControlJob 1 Start") ? "ControlJob 2 Start" : "ControlJob 1 Start";
            return true;
        }

        if ((upper.Contains("PRJOB", StringComparison.Ordinal) || upper.Contains("PROCESS JOB", StringComparison.Ordinal) || upper.Contains("PROCESSJOB", StringComparison.Ordinal)) &&
            (upper.Contains("START", StringComparison.Ordinal) || upper.Contains("WAIT", StringComparison.Ordinal) || upper.Contains("PROCESSING", StringComparison.Ordinal)))
        {
            if (!IsEventPassed("Process Job 1 Waiting For Start Event"))
            {
                actionText = "Process Job 1 Waiting For Start Event";
                return true;
            }

            if (!IsEventPassed("ProcessJob 2 Start (PJ Auto Start)"))
            {
                actionText = "ProcessJob 2 Start (PJ Auto Start)";
                return true;
            }

            actionText = "ProcessJob 1 Start (PJ Manual Start)";
            return true;
        }

        return false;
    }

    private bool TryResolveJobProcessingAction(string upper, out string actionText)
    {
        actionText = string.Empty;

        if (upper.Contains("RECIPE", StringComparison.Ordinal) && upper.Contains("STEP", StringComparison.Ordinal) && upper.Contains("END", StringComparison.Ordinal))
        {
            actionText = "Recipe Step End\n(with RecipeID/Recipe Step Number/PJID linked)";
            return true;
        }

        if (upper.Contains("RECIPE", StringComparison.Ordinal) && upper.Contains("STEP", StringComparison.Ordinal) && upper.Contains("START", StringComparison.Ordinal))
        {
            actionText = "Recipe Step Start\n(with RecipeID/Recipe Step Number/PJID linked)";
            return true;
        }

        if (upper.Contains("RECIPE", StringComparison.Ordinal) && upper.Contains("END", StringComparison.Ordinal))
        {
            actionText = "Recipe End\n(with ChamberID/LotID/SlotID/RecipeID/PJID linked)";
            return true;
        }

        if (upper.Contains("RECIPE", StringComparison.Ordinal) && upper.Contains("START", StringComparison.Ordinal))
        {
            actionText = "Recipe Start\n(with ChamberID/LotID/SlotID/RecipeID/PJID linked)";
            return true;
        }

        if ((upper.Contains("CHAMBER", StringComparison.Ordinal) || upper.Contains("CHMB", StringComparison.Ordinal)) && upper.Contains("END", StringComparison.Ordinal))
        {
            actionText = "Chamber End\n(with ChamberID/LotID/SlotID linked)";
            return true;
        }

        if ((upper.Contains("CHAMBER", StringComparison.Ordinal) || upper.Contains("CHMB", StringComparison.Ordinal)) && upper.Contains("START", StringComparison.Ordinal))
        {
            actionText = "Chamber Start\n(with ChamberID/LotID/SlotID linked)";
            return true;
        }

        if ((upper.Contains("WAFER", StringComparison.Ordinal) || upper.Contains("SUBSTRATE", StringComparison.Ordinal)) && upper.Contains("END", StringComparison.Ordinal))
        {
            actionText = "E90 Wafer End\n(with LotID/SlotID/RecipeID/PJID linked)";
            return true;
        }

        if ((upper.Contains("WAFER", StringComparison.Ordinal) || upper.Contains("SUBSTRATE", StringComparison.Ordinal)) && upper.Contains("START", StringComparison.Ordinal))
        {
            actionText = "E90 Wafer Start\n(with LotID/SlotID/RecipeID/PJID linked)";
            return true;
        }

        if (upper.Contains("LOCATION", StringComparison.Ordinal) && (upper.Contains("UNOCCUP", StringComparison.Ordinal) || upper.Contains("UN OCCUP", StringComparison.Ordinal)))
        {
            actionText = "E90 Substrate Location Changed\n(UnOccupied event with LotID/SlotID/LocationID linked)";
            return true;
        }

        if (upper.Contains("LOCATION", StringComparison.Ordinal) && upper.Contains("OCCUP", StringComparison.Ordinal))
        {
            actionText = "E90 Substrate Location Changed\n(Occupied event with LotID/SlotID/LocationID linked)";
            return true;
        }

        if ((upper.Contains("COMPLETE", StringComparison.Ordinal) || upper.Contains("END", StringComparison.Ordinal)) &&
            (upper.Contains("PRJOB", StringComparison.Ordinal) || upper.Contains("PROCESS JOB", StringComparison.Ordinal) || upper.Contains("PROCESSJOB", StringComparison.Ordinal)))
        {
            actionText = upper.Contains("PJ1", StringComparison.Ordinal) ? "Process Job 1 End" : "Process Job 2 End";
            return true;
        }

        if ((upper.Contains("COMPLETE", StringComparison.Ordinal) || upper.Contains("END", StringComparison.Ordinal)) &&
            (upper.Contains("CONTROL", StringComparison.Ordinal) || upper.Contains("CJ", StringComparison.Ordinal)))
        {
            actionText = upper.Contains("CJ1", StringComparison.Ordinal) ? "Control Job 1 End" : "Control Job 2 End";
            return true;
        }

        if (_jobProcessingIndex < JobProcessingSequence.Length && LooksLikeProcessingEvent(upper))
        {
            actionText = JobProcessingSequence[_jobProcessingIndex++];
            return true;
        }

        return false;
    }

    private static bool IsSlotMapReport(string upper)
        => upper.Contains("SLOTMAP", StringComparison.Ordinal) ||
            upper.Contains("SLOT MAP", StringComparison.Ordinal) ||
            upper.Contains("CONTENTMAP", StringComparison.Ordinal);

    private static bool LooksLikeProcessingEvent(string upper)
        => upper.Contains("RECIPE", StringComparison.Ordinal) ||
            upper.Contains("CHAMBER", StringComparison.Ordinal) ||
            upper.Contains("WAFER", StringComparison.Ordinal) ||
            upper.Contains("SUBSTRATE", StringComparison.Ordinal) ||
            upper.Contains("PRJOB", StringComparison.Ordinal) ||
            upper.Contains("PROCESSJOB", StringComparison.Ordinal) ||
            upper.Contains("CONTROLJOB", StringComparison.Ordinal);

    private bool IsEventPassed(string actionText)
        => _eventLamps.TryGetValue(actionText, out var lamp) && lamp.BackColor == Color.FromArgb(78, 180, 95);

    private void ArmAutoPass(string actionText)
    {
        _armedAutoPassEvents.Add(actionText);
    }

    private void ArmJobProcessingEvents()
    {
        foreach (var actionText in JobProcessingSequence)
        {
            ArmAutoPass(actionText);
        }
    }

    private void MarkL2EventPass(string actionText, string source)
    {
        if (!_armedAutoPassEvents.Contains(actionText))
        {
            return;
        }

        if (!_eventLamps.TryGetValue(actionText, out var lamp))
        {
            return;
        }

        if (lamp.BackColor == Color.FromArgb(78, 180, 95))
        {
            return;
        }

        lamp.BackColor = Color.FromArgb(78, 180, 95);
        AppendResult($"[INFO] Auto PASS: {actionText.Replace(Environment.NewLine, " ")} ({source})");
    }

    private void ApplyCarrierReadInfo(byte portId, string carrierId, string locationId, string source)
    {
        switch (portId)
        {
            case 1:
                _txtCarrierId1.Text = carrierId;
                _nudPortId1.Value = 1;
                _lastCarrierId1 = carrierId;
                _lastLocationId1 = locationId;
                _hasCarrierRead1 = true;
                break;
            case 2:
                _txtCarrierId2.Text = carrierId;
                _nudPortId2.Value = 2;
                _lastCarrierId2 = carrierId;
                _lastLocationId2 = locationId;
                _hasCarrierRead2 = true;
                break;
            default:
                AppendResult($"[INFO] CarrierIDRead ignored: unsupported PortID={portId}, CarrierID={carrierId}");
                return;
        }

        var locationText = string.IsNullOrWhiteSpace(locationId) ? string.Empty : $", LocationID={locationId}";
        AppendResult($"[INFO] CarrierIDRead captured from {source}: PortID={portId}, CarrierID={carrierId}{locationText}");
        var carrierReadAction = portId == 1
            ? "1) Testing Port1 Carrier ID Read Event"
            : "6) Testing Port2 Carrier ID Read Event";
        if (_armedAutoPassEvents.Contains(carrierReadAction))
        {
            SetActionLampToPass(carrierReadAction);
        }
    }

    private bool TryGetCachedCarrierRead(byte expectedPortId, out string carrierId, out string locationId)
    {
        carrierId = string.Empty;
        locationId = string.Empty;

        if (expectedPortId == 1 && _hasCarrierRead1 && !string.IsNullOrWhiteSpace(_lastCarrierId1))
        {
            carrierId = _lastCarrierId1;
            locationId = _lastLocationId1;
            return true;
        }

        if (expectedPortId == 2 && _hasCarrierRead2 && !string.IsNullOrWhiteSpace(_lastCarrierId2))
        {
            carrierId = _lastCarrierId2;
            locationId = _lastLocationId2;
            return true;
        }

        return false;
    }

    private static bool TryExtractCarrierReadInfo(
        Secs4Net.SecsMessage message,
        out byte portId,
        out string carrierId,
        out string locationId)
    {
        return Secs.SecsPayload.TryExtractCarrierReadInfo(message, out portId, out carrierId, out locationId);
    }

    private static bool TryExtractPortIdFromLocation(string value, out byte portId)
    {
        portId = 0;
        var match = System.Text.RegularExpressions.Regex.Match(
            value.Trim(),
            @"^(?:LO|LOAD)?PORT\s*([1-4])$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success || !byte.TryParse(match.Groups[1].Value, out var parsedPortId))
        {
            return false;
        }

        portId = parsedPortId;
        return true;
    }

    private static bool IsLikelyCarrierId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (TryExtractPortIdFromLocation(normalized, out _))
        {
            return false;
        }

        if (IsLikelyClockValue(normalized))
        {
            return false;
        }

        if (normalized.Equals("CarrierID", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("PortID", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("LocationID", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("CLOCK", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ContentMap", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[A-Za-z0-9._-]{3,64}$");
    }

    private static bool IsLikelyClockValue(string value)
    {
        var normalized = value.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^20\d{12,15}$"))
        {
            return false;
        }

        var year = int.Parse(normalized[..4]);
        var month = int.Parse(normalized.Substring(4, 2));
        var day = int.Parse(normalized.Substring(6, 2));
        var hour = int.Parse(normalized.Substring(8, 2));
        var minute = int.Parse(normalized.Substring(10, 2));
        var second = int.Parse(normalized.Substring(12, 2));

        return year is >= 2000 and <= 2099 &&
            month is >= 1 and <= 12 &&
            day is >= 1 and <= 31 &&
            hour is >= 0 and <= 23 &&
            minute is >= 0 and <= 59 &&
            second is >= 0 and <= 59;
    }

    private static bool TryReadByteValue(Secs4Net.Item item, out byte value)
    {
        value = 0;

        if (!Secs.SecsPayload.TryReadUInt(item, out var unsignedValue) || unsignedValue > byte.MaxValue)
        {
            return false;
        }

        value = (byte)unsignedValue;
        return true;
    }

    private static bool TryReadAsciiValue(Secs4Net.Item item, out string value)
    {
        value = string.Empty;

        if (item is null || item.Format == Secs4Net.SecsFormat.List || item.Count == 0)
        {
            return false;
        }

        try
        {
            value = item.GetString().Trim();
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }
}
