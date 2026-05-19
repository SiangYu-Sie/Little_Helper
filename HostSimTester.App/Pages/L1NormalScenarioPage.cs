using System.Text.RegularExpressions;
using System.Text;

namespace HostSimTester.App.Pages;

public sealed class L1NormalScenarioPage : BaseTestPage
{
    private enum ReportScope
    {
        Normal,
        MultiLot
    }

    private static readonly Dictionary<uint, string> NormalReportByCeid = new()
    {
        // Observed from user equipment: S6F11 CEID=1006 is Auto Clamp Event.
        [1006] = "3) Auto Clamp Event",
        [1000] = "4) Auto Read Carrier ID",
        [93] = "11) Control Job Start Event",
        [96] = "12) Process Job Start Event",
        [41] = "12) Process Job Start Event",
        [5799] = "13) Wafer Process Start Event",
        [5790] = "14) Wafer Process End Event",
        [195] = "6) Auto Docking Event",
        [6199] = "11) Control Job Start Event"
    };

    private static readonly Dictionary<uint, string> MultiLotReportByCeid = new()
    {
        [1000] = "1) Testing Port1 Carrier ID Read Event",
        [5799] = "11) Wafer Process Start Event",
        [5790] = "12) Wafer Process End Event",
        [6199] = "8) Control Job CJ1 Start Event"
    };

    private static readonly HashSet<string> StrictCeidOnlyActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "13) Wafer Process Start Event",
        "14) Wafer Process End Event",
        "11) Wafer Process Start Event",
        "12) Wafer Process End Event"
    };

    private static readonly Dictionary<string, string> NextReportByAction = new(StringComparer.OrdinalIgnoreCase)
    {
        ["2) Carrier Placement (Load Complete)"] = "3) Auto Clamp Event",
        ["3) Auto Clamp Event"] = "4) Auto Read Carrier ID",
        ["5) Proceed With Carrier"] = "6) Auto Docking Event",
        ["6) Auto Docking Event"] = "7) Auto Open Door Event",
        ["7) Auto Open Door Event"] = "8) Slot Mapping Data",
        ["11) Control Job Start Event"] = "12) Process Job Start Event",
        ["12) Process Job Start Event"] = "13) Wafer Process Start Event",
        ["13) Wafer Process Start Event"] = "14) Wafer Process End Event",
        ["14) Wafer Process End Event"] = "15) Process Job Completed Event",
        ["15) Process Job Completed Event"] = "16) Control Job Completed Event",
        ["16) Control Job Completed Event"] = "17) Auto Door Closed Event",
        ["19) Undocking Event"] = "20) Unclamp Event",
        ["20) Unclamp Event"] = "21) Ready to Unload Event",
        ["25) Carrier Removed Event"] = "26) Ready to Load Event",
        ["1) Testing Port1 Carrier ID Read Event"] = "3) SlotMap Event",
        ["8) Control Job CJ1 Start Event"] = "9) Process Job PJ1 Start Event",
        ["9) Process Job PJ1 Start Event"] = "10) Process Job PJ2 Start Event",
        ["10) Process Job PJ2 Start Event"] = "11) Wafer Process Start Event",
        ["11) Wafer Process Start Event"] = "12) Wafer Process End Event",
        ["12) Wafer Process End Event"] = "13) Process Job PJ1 Completed Event",
        ["13) Process Job PJ1 Completed Event"] = "14) Process Job PJ2 Completed Event",
        ["14) Process Job PJ2 Completed Event"] = "15) Control Job CJ1 Completed Event"
    };

    private static readonly string[] CarrierIdSupplementEventKeywords =
    [
        "CARRIERDOCKED",
        "CARRIEROPENED",
        "SLOTMAPREPORT"
    ];

    private static readonly HashSet<uint> SlotMapRptids = [26];

    private readonly NumericUpDown _nudSvid;
    private readonly NumericUpDown _nudUnloadSvid;
    private readonly TextBox _txtCarrierId;
    private readonly NumericUpDown _nudPortId;
    private readonly TextBox _txtProcessJobId;
    private readonly TextBox _txtRecipeId;
    private readonly TextBox _txtControlJobId;
    private readonly TextBox _txtLoadingExecutionResult;
    private readonly TextBox _txtSlotMapData;
    private readonly Label _lblWaferStartSlotProgress;
    private readonly Label _lblWaferEndSlotProgress;
    private readonly Label _lblMultiLotPj1StartHint;
    private readonly Label _lblMultiLotPj2StartHint;
    private readonly Label _lblMultiLotPj1CompletedHint;
    private readonly Label _lblMultiLotPj2CompletedHint;
    private readonly Label _lblMultiLotWaferStartSlotProgress;
    private readonly Label _lblMultiLotWaferEndSlotProgress;
    private readonly Dictionary<(ReportScope Scope, string ActionText), Panel> _reportLamps = new();
    private readonly Dictionary<(ReportScope Scope, uint Ceid), string> _reportActionByCeid = new();
    private readonly HashSet<(ReportScope Scope, string ActionText)> _armedReportActions = new();
    private IReadOnlyList<string> _multiLotContentMapSlotIds = Array.Empty<string>();
    private IReadOnlyList<string> _multiLotPj1SlotIds = Array.Empty<string>();
    private IReadOnlyList<string> _multiLotPj2SlotIds = Array.Empty<string>();
    private string _multiLotPj1Id = "PJ1";
    private string _multiLotPj2Id = "PJ2";
    private string _currentNormalProcessJobId = string.Empty;
    private string _currentNormalControlJobId = string.Empty;
    private ReportScope _activeReportScope = ReportScope.Normal;
    private readonly Dictionary<ReportScope, string> _pendingReportActionByScope = new();
    private DateTime _pendingReportActionAt;
    private bool _loadingProceedSent;
    private int _expectedWaferSlotCount = 1;
    private int _waferEndCount;
    private int _multiLotExpectedWaferSlotCount = 1;
    private int _multiLotWaferEndCount;
    private bool _carrierRemovalEventsEnabled;
    private bool _normalMeasurementKickSent;

    private string? PendingReportAction
    {
        get => _pendingReportActionByScope.TryGetValue(_activeReportScope, out var actionText) ? actionText : null;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _pendingReportActionByScope.Remove(_activeReportScope);
                return;
            }

            _pendingReportActionByScope[_activeReportScope] = value;
        }
    }

    public L1NormalScenarioPage(Secs.SecsConnection connection)
        : base("L1 Normal Scenario", Logging.LoggerNames.L1Normal, connection)
    {
        Connection.PrimaryMessageReceived += OnPrimaryMessageReceived;
        Disposed += (_, _) => Connection.PrimaryMessageReceived -= OnPrimaryMessageReceived;

        var reportBuildScope = ReportScope.Normal;

        Label AddInlineHint(Control host, string text)
        {
            // Hint appears as a compact indented row directly below the associated step button
            var label = new Label
            {
                Text = text,
                AutoSize = false,
                Width = 170,
                Height = 18,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.ThemeHelper.TextDark,
                BackColor = Theme.ThemeHelper.GroupHeaderBg,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft JhengHei UI", 8F),
                Margin = new Padding(28, 0, 0, 2)
            };
            host.Controls.Add(label);
            return label;
        }

        void AddStepLegend(Control host, int width)
        {
            host.Controls.Add(new Label
            {
                Text = "藍色按鈕：主機主動命令 / 灰色列：等待設備事件",
                Width = width,
                ForeColor = Color.FromArgb(80, 96, 112),
                Margin = new Padding(6, 0, 6, 2)
            });
        }

        void AddReportRow(Control host, string text, Func<Task> action)
        {
            _ = action;
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
            _reportLamps[(reportBuildScope, text)] = lamp;

            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0),
                Cursor = Cursors.Default
            };
            lbl.Padding = new Padding(2, 0, 2, 0);
            lbl.BorderStyle = BorderStyle.None;
            row.Controls.Add(lamp);
            row.Controls.Add(lbl);
            host.Controls.Add(row);
        }

        void AddReportStartButton(Control host, string text, Action action, int buttonWidth = 260)
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
                Width = 16,
                Height = 16,
                Margin = new Padding(3, 8, 8, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            _reportLamps[(reportBuildScope, text)] = lamp;

            var btn = new Button
            {
                Text = text,
                Width = buttonWidth,
                Height = 30,
                Margin = new Padding(0),
                Padding = new Padding(12, 0, 12, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.ThemeHelper.CobaltBlue,
                ForeColor = Color.White
            };
            btn.FlatAppearance.BorderColor = Theme.ThemeHelper.DeepBlue;
            btn.Click += (_, _) =>
            {
                lamp.BackColor = Theme.ThemeHelper.LogWarn;
                action();
            };

            row.Controls.Add(lamp);
            row.Controls.Add(btn);
            host.Controls.Add(row);
        }

        _nudSvid = new NumericUpDown { Minimum = 1, Maximum = uint.MaxValue, Value = 1, Width = 80 };
        _nudUnloadSvid = new NumericUpDown { Minimum = 1, Maximum = uint.MaxValue, Value = 1, Width = 80 };
        _txtCarrierId = new TextBox { Text = "", Width = 120 };
        _nudPortId = new NumericUpDown { Minimum = 1, Maximum = 4, Value = 1, Width = 60 };
        _txtProcessJobId = new TextBox { Text = "PJ001", Width = 100 };
        _txtRecipeId = new TextBox { Text = "Trim_2", Width = 100 };
        _txtControlJobId = new TextBox { Text = "CJ001", Width = 100 };
        _txtSlotMapData = new TextBox
        {
            Width = 260,
            Height = 24,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.ThemeHelper.TableBg,
            Margin = new Padding(30, 0, 6, 4)
        };
        ConfigureActionPanel(640, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        var tabControl = CreateTabControl();

        var tabNormal = CreateTab(tabControl, "Normal Scenario Check");

        var gridNormal = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        gridNormal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        gridNormal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        gridNormal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        gridNormal.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabNormal.Controls.Add(gridNormal);

        var loadingSection = CreateSection("1. Loading Scenario");
        var loadingBody = CreateSectionBody(loadingSection);
        loadingBody.Controls.Add(new Label
        {
            Text = "Click the button below to start testing the loading scenario",
            Width = 300,
            ForeColor = Theme.ThemeHelper.TextMid,
            Margin = new Padding(6, 2, 6, 2)
        });
        AddStepLegend(loadingBody, 300);
        var svidRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.ThemeHelper.GroupHeaderBg,
            Padding = new Padding(4, 2, 4, 2),
            Margin = new Padding(6, 2, 6, 2)
        };
        svidRow.Controls.Add(new Label { Text = "SVID:", Width = 44, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.ThemeHelper.TextDark });
        svidRow.Controls.Add(_nudSvid);
        loadingBody.Controls.Add(svidRow);

        AddActionTo(loadingBody, "1) Send S1F3 to Query Port Status",
            async () =>
            {
                TrackExpectedReport("2) Carrier Placement (Load Complete)");
                await SendAsync("L1Normal_S1F3_QueryPortTransferState", 1, 3,
                    Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest((uint)_nudSvid.Value)).ConfigureAwait(true);
            }, 260);
        AddReportRow(loadingBody, "2) Carrier Placement (Load Complete)",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_LoadComplete", 6, 11, 30));
        AddReportRow(loadingBody, "3) Auto Clamp Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_AutoClamp", 6, 11, 30));
        AddReportRow(loadingBody, "4) Auto Read Carrier ID",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_CarrierIdRead", 6, 11, 30));
        AddInlineHint(loadingBody, "(Carrier ID)");

        var carrierPortRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.ThemeHelper.GroupHeaderBg,
            Padding = new Padding(4, 2, 4, 2),
            Margin = new Padding(6, 2, 6, 2)
        };
        carrierPortRow.Controls.Add(new Label { Text = "Carrier ID:", Width = 62, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.ThemeHelper.TextDark });
        carrierPortRow.Controls.Add(_txtCarrierId);
        carrierPortRow.Controls.Add(new Label { Text = "Port:", Width = 34, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.ThemeHelper.TextDark, Margin = new Padding(6, 0, 0, 0) });
        carrierPortRow.Controls.Add(_nudPortId);
        loadingBody.Controls.Add(carrierPortRow);

        AddActionTo(loadingBody, "Bypass: Manually Enter Port ID & Carrier ID",
            () =>
            {
                AppendResult($"> Bypass mode: Carrier={_txtCarrierId.Text.Trim()}, Port={(byte)_nudPortId.Value}");
                return Task.CompletedTask;
            }, 260, showLamp: false);
        AddReportRow(loadingBody, "5) Proceed With Carrier",
            () => Task.CompletedTask);
        loadingBody.Controls.Add(new Label
        {
            Text = "Execution Result :",
            Width = 300,
            ForeColor = Theme.ThemeHelper.TextMid,
            Margin = new Padding(6, 0, 6, 2)
        });
        _txtLoadingExecutionResult = new TextBox
        {
            Width = 260,
            Height = 24,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.ThemeHelper.TableBg,
            Margin = new Padding(30, 0, 6, 4)
        };
        loadingBody.Controls.Add(_txtLoadingExecutionResult);
        AddReportRow(loadingBody, "6) Auto Docking Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_AutoDocking", 6, 11, 30));
        AddReportRow(loadingBody, "7) Auto Open Door Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_AutoOpenDoor", 6, 11, 30));
        AddReportRow(loadingBody, "8) Slot Mapping Data",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_SlotMappingData", 6, 11, 30));
        AddInlineHint(loadingBody, "(Slot Map)");
        loadingBody.Controls.Add(_txtSlotMapData);

        var processingSection = CreateSection("2. Processing Scenario");
        var processingBody = CreateSectionBody(processingSection);
        processingBody.Controls.Add(new Label
        {
            Text = "Click the button below to start testing the processing scenario",
            Width = 300,
            ForeColor = Theme.ThemeHelper.TextMid,
            Margin = new Padding(6, 2, 6, 2)
        });
        AddStepLegend(processingBody, 300);
        AddActionTo(processingBody, "9) Proceed With Carrier",
            async () =>
            {
                using var dlg = new Dialogs.S3F17ProceedDialog(_txtCarrierId.Text.Trim(), (byte)_nudPortId.Value);
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                _txtCarrierId.Text = dlg.CarrierId;
                _nudPortId.Value = dlg.PortId;

                var occupiedSlotIds = NormalizeSlotIds(dlg.OccupiedSlotIds);
                using var prepDlg = new Dialogs.PpSelectStartPjCjDialog(
                    _txtCarrierId.Text.Trim(),
                    _txtProcessJobId.Text.Trim(),
                    _txtRecipeId.Text.Trim(),
                    _txtControlJobId.Text.Trim(),
                    occupiedSlotIds);
                if (prepDlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                _txtCarrierId.Text = prepDlg.CarrierId;
                _txtProcessJobId.Text = string.IsNullOrWhiteSpace(prepDlg.ProcessJobId) ? _txtProcessJobId.Text : prepDlg.ProcessJobId;
                _txtRecipeId.Text = string.IsNullOrWhiteSpace(prepDlg.Ppid) ? _txtRecipeId.Text : prepDlg.Ppid;
                _txtControlJobId.Text = string.IsNullOrWhiteSpace(prepDlg.ControlJobId) ? _txtControlJobId.Text : prepDlg.ControlJobId;

                var ppSelectParameters = prepDlg.PpSelectCpItems
                    .Select(x => new KeyValuePair<string, string>(x.Name, x.Value))
                    .ToArray();
                if (ppSelectParameters.Length > 0)
                {
                    await SendAsync("L1Normal_S2F41_PPSelect_FromDialog", 2, 41,
                        Secs.SecsMessageFactory.S2F41HostCommandByParameters(prepDlg.PpSelectRcmd, ppSelectParameters)).ConfigureAwait(true);
                }

                var ppStartParameters = prepDlg.PpStartCpItems
                    .Select(x => new KeyValuePair<string, string>(x.Name, x.Value))
                    .ToArray();
                if (ppStartParameters.Length > 0)
                {
                    await SendAsync("L1Normal_S2F41_PPStart_FromDialog", 2, 41,
                        Secs.SecsMessageFactory.S2F41HostCommandByParameters(prepDlg.PpStartRcmd, ppStartParameters)).ConfigureAwait(true);
                }

                // If user did not fill Slot entries in S3F17 dialog, use SlotID list from PJ/CJ dialog as default wafer placeholders.
                var slotIds = prepDlg.GetSlotIds();
                ResetWaferSlotProgress(slotIds.Count);
                var slotEntries = dlg.SlotEntries;
                if (dlg.IncludeContentMap && slotEntries.All(x => string.IsNullOrWhiteSpace(x.LotId) && string.IsNullOrWhiteSpace(x.WaferId)) && slotIds.Count > 0)
                {
                    slotEntries = slotIds
                        .Select(id => (LotId: string.Empty, WaferId: id))
                        .ToArray();
                }

                var payload = dlg.IncludeContentMap
                    ? Secs.SecsMessageFactory.S3F17ProceedWithCarrierContentMap(
                        dlg.CarrierId, dlg.PortId, dlg.ContentMapCattrid, slotEntries)
                    : Secs.SecsMessageFactory.S3F17ProceedWithCarrier(dlg.CarrierId, dlg.PortId);

                await SendAsync("L1Normal_S3F17_ProceedWithCarrier_Process", 3, 17, payload).ConfigureAwait(true);

                // E40: Send S16F15 PRJobMultiCreate if PJ info is provided
                var pjIdSource = string.IsNullOrWhiteSpace(prepDlg.ProcessJobId) ? _txtProcessJobId.Text.Trim() : prepDlg.ProcessJobId.Trim();
                var pjId = pjIdSource;
                var ppid = string.IsNullOrWhiteSpace(prepDlg.Ppid) ? _txtRecipeId.Text.Trim() : prepDlg.Ppid;
                var carrierId2 = prepDlg.CarrierId;
                if (!string.IsNullOrWhiteSpace(pjId))
                {
                    _normalMeasurementKickSent = false;
                    _currentNormalProcessJobId = pjId;
                    await SendAsync("L1Normal_S16F15_ProcessJobCreate", 16, 15,
                        Secs.SecsMessageFactory.S16F15ProcessJobCreate(pjId, ppid, carrierId2, slotIds)).ConfigureAwait(true);
                }

                // E94: Send S14F9 ControlJobCreate if CJ info is provided
                var cjIdSource = string.IsNullOrWhiteSpace(prepDlg.ControlJobId) ? _txtControlJobId.Text.Trim() : prepDlg.ControlJobId.Trim();
                var cjId = cjIdSource;
                if (!string.IsNullOrWhiteSpace(cjId))
                {
                    _currentNormalControlJobId = cjId;
                    // Arm CJ start tracking before create/start sequence because some tools emit CEID 93/96 immediately.
                    TrackExpectedReport("11) Control Job Start Event");
                    const byte processOrderMgmt = 2;
                    await SendControlJobCreateWithFallbackAsync(
                        "L1Normal_S14F9_ControlJobCreate",
                        cjId,
                        carrierId2,
                        [pjId],
                        processOrderMgmt,
                        ppid,
                        slotIds).ConfigureAwait(true);
                }

                MarkReportPass("10) PPSelect & Start OR Create Process Job & Control Job (E40 & E94)", "S2F41/S16F15/S14F9 sent");
            }, 260);

        var jobRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.ThemeHelper.GroupHeaderBg,
            Padding = new Padding(4, 2, 4, 2),
            Margin = new Padding(6, 2, 6, 2)
        };
        jobRow.Controls.Add(new Label { Text = "PJID:", Width = 38, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.ThemeHelper.TextDark });
        jobRow.Controls.Add(_txtProcessJobId);
        jobRow.Controls.Add(new Label { Text = "PPID:", Width = 38, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.ThemeHelper.TextDark, Margin = new Padding(6, 0, 0, 0) });
        jobRow.Controls.Add(_txtRecipeId);
        jobRow.Controls.Add(new Label { Text = "CJID:", Width = 38, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.ThemeHelper.TextDark, Margin = new Padding(6, 0, 0, 0) });
        jobRow.Controls.Add(_txtControlJobId);
        processingBody.Controls.Add(jobRow);

        AddReportRow(processingBody, "10) PPSelect & Start OR Create Process Job & Control Job (E40 & E94)",
            () => Task.CompletedTask);
        AddReportRow(processingBody, "11) Control Job Start Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_CJStart", 6, 11, 30));
        AddReportRow(processingBody, "12) Process Job Start Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_PJStart", 6, 11, 30));
        AddInlineHint(processingBody, "(PJ ID)");
        AddReportRow(processingBody, "13) Wafer Process Start Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_WaferStart", 6, 11, 30));
        _lblWaferStartSlotProgress = AddInlineHint(processingBody, "Slot ID: 0/1 completed");
        AddReportRow(processingBody, "14) Wafer Process End Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_WaferEnd", 6, 11, 30));
        _lblWaferEndSlotProgress = AddInlineHint(processingBody, "Slot ID: 0/1 completed");
        AddReportRow(processingBody, "15) Process Job Completed Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_PJComplete", 6, 11, 30));
        AddInlineHint(processingBody, "(PJ ID)");
        AddReportRow(processingBody, "16) Control Job Completed Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_CJComplete", 6, 11, 30));
        AddReportRow(processingBody, "17) Auto Door Closed Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_AutoDoorClosed", 6, 11, 30));

        var unloadingSection = CreateSection("3. Unloading Scenario");
        var unloadingBody = CreateSectionBody(unloadingSection);
        unloadingBody.Controls.Add(new Label
        {
            Text = "Click the button below to start testing the unloading scenario",
            Width = 300,
            ForeColor = Theme.ThemeHelper.TextMid,
            Margin = new Padding(6, 2, 6, 2)
        });
        AddStepLegend(unloadingBody, 300);
        AddActionTo(unloadingBody, "18) Undock/Unclamp OR Carrier Release Command",
            async () =>
            {
                using var dlg = new Dialogs.UnloadCarrierDialog(_txtCarrierId.Text.Trim(), (byte)_nudPortId.Value);
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                _txtCarrierId.Text = dlg.CarrierId;
                _nudPortId.Value = dlg.PortId;
                TrackExpectedReport("19) Undocking Event");
                _carrierRemovalEventsEnabled = false;

                if (dlg.CommandKind == Dialogs.UnloadCarrierDialog.UnloadCommandKind.CarrierRelease)
                {
                    await SendAsync("L1Normal_S3F17_CarrierRelease", 3, 17,
                        Secs.SecsMessageFactory.S3F17CarrierRelease(dlg.CarrierId, dlg.PortId)).ConfigureAwait(true);
                    return;
                }

                var parameters = string.IsNullOrWhiteSpace(dlg.CpName)
                    ? Array.Empty<(string Name, string Type, string Value)>()
                    : [(dlg.CpName, dlg.CpType, dlg.CpValue)];

                await SendAsync("L1Normal_S2F41_UnloadCarrier", 2, 41,
                    Secs.SecsMessageFactory.S2F41HostCommandByTypedParameters(dlg.RemoteCommand, parameters)).ConfigureAwait(true);
            }, 260);
        AddReportRow(unloadingBody, "19) Undocking Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_Undocking", 6, 11, 30));
        AddReportRow(unloadingBody, "20) Unclamp Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_Unclamp", 6, 11, 30));
        AddReportRow(unloadingBody, "21) Ready to Unload Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_ReadyToUnload", 6, 11, 30));

        var unloadSvidRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.ThemeHelper.GroupHeaderBg,
            Padding = new Padding(4, 2, 4, 2),
            Margin = new Padding(6, 2, 6, 2)
        };
        unloadSvidRow.Controls.Add(new Label { Text = "SVID:", Width = 44, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.ThemeHelper.TextDark });
        unloadSvidRow.Controls.Add(_nudUnloadSvid);
        unloadingBody.Controls.Add(unloadSvidRow);

        AddActionTo(unloadingBody, "22) Send S1F3 to Query Process Job State",
            async () =>
            {
                await SendAsync("L1Normal_S1F3_QueryProcessJobState", 1, 3,
                    Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest((uint)_nudUnloadSvid.Value)).ConfigureAwait(true);
                await SendAsync("L1Normal_S16F19_QueryProcessJobList", 16, 19).ConfigureAwait(true);
                MarkReportPass("23) Send S16,F19 PRGetAllJobs", "S16F19 sent");
                await SendAsync("L1Normal_S16F21_QueryProcessJobCreateLimit", 16, 21).ConfigureAwait(true);
                MarkReportPass("24) Send S16,F21 PRGetSpace", "S16F21 sent");
                _carrierRemovalEventsEnabled = true;
                TrackExpectedReport("25) Carrier Removed Event");
            }, 260);
        AddReportRow(unloadingBody, "23) Send S16,F19 PRGetAllJobs",
            () => Task.CompletedTask);
        AddReportRow(unloadingBody, "24) Send S16,F21 PRGetSpace",
            () => Task.CompletedTask);
        unloadingBody.Controls.Add(new Label
        {
            Text = "Please remove carrier from loadport",
            Width = 300,
            ForeColor = Theme.ThemeHelper.TextMid,
            Margin = new Padding(6, 2, 6, 2)
        });
        AddReportRow(unloadingBody, "25) Carrier Removed Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_CarrierRemoved", 6, 11, 30));
        AddReportRow(unloadingBody, "26) Ready to Load Event",
            () => WaitPrimaryAsync("L1Normal_Wait_S6F11_ReadyToLoad", 6, 11, 30));

        gridNormal.Controls.Add(loadingSection, 0, 0);
        gridNormal.Controls.Add(processingSection, 1, 0);
        gridNormal.Controls.Add(unloadingSection, 2, 0);

        // Tab 2: Multi Lot Testing
        reportBuildScope = ReportScope.MultiLot;
        var tabMultiLot = CreateTab(tabControl, "Multi Lot Testing");
        tabControl.SelectedIndexChanged += (_, _) =>
        {
            _activeReportScope = ReferenceEquals(tabControl.SelectedTab, tabMultiLot)
                ? ReportScope.MultiLot
                : ReportScope.Normal;
        };

        var gridMultiLot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        gridMultiLot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        gridMultiLot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        gridMultiLot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabMultiLot.Controls.Add(gridMultiLot);

        var multiLotNote = new Label
        {
            Text = "  ⓘ  Carrier ID, Port, PPID, CJID values are shared from the Normal Scenario Check tab.",
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.ThemeHelper.StatusText,
            BackColor = Theme.ThemeHelper.NavyPanel,
            Font = new Font("Microsoft JhengHei UI", 8.5F)
        };
        tabMultiLot.Controls.Add(multiLotNote);
        multiLotNote.BringToFront();

        var leftMultiLot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        leftMultiLot.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        leftMultiLot.RowStyles.Add(new RowStyle(SizeType.Percent, 62));

        var multiLoadSection = CreateSection("1. Loading Multi Lot Testing");
        var multiLoadBody = CreateSectionBody(multiLoadSection);
        multiLoadBody.Controls.Add(new Label
        {
            Text = "Click the button below to start testing the loading scenario",
            Width = 380,
            ForeColor = Theme.ThemeHelper.TextMid,
            Margin = new Padding(6, 2, 6, 2)
        });
        AddStepLegend(multiLoadBody, 380);
        AddReportStartButton(multiLoadBody, "1) Testing Port1 Carrier ID Read Event",
            () =>
            {
                TrackExpectedReport("1) Testing Port1 Carrier ID Read Event");
                AppendResult("> Multi Lot waiting for Port1 Carrier ID Read Event (S6F11 CEID=1000).");
            });
        AddInlineHint(multiLoadBody, "(Carrier ID)");
        AddActionTo(multiLoadBody, "2) Proceed With Carrier  Bypass CarrierID Event",
            async () =>
            {
                TrackExpectedReport("3) SlotMap Event");
                await SendAsync("L1Normal_Multi_S3F17_ProceedWithCarrier", 3, 17,
                    Secs.SecsMessageFactory.S3F17ProceedWithCarrier(_txtCarrierId.Text.Trim(), (byte)_nudPortId.Value)).ConfigureAwait(true);
            }, 260);
        AddReportRow(multiLoadBody, "3) SlotMap Event",
            () => WaitPrimaryAsync("L1Normal_Multi_Wait_S6F11_SlotMap", 6, 11, 30));

        var multiJobStartSection = CreateSection("2. Job Starting for Multi Lot Testing");
        var multiJobStartBody = CreateSectionBody(multiJobStartSection);
        multiJobStartBody.Controls.Add(new Label
        {
            Text = "Click the button below to start testing the processing scenario",
            Width = 380,
            ForeColor = Theme.ThemeHelper.TextMid,
            Margin = new Padding(6, 2, 6, 2)
        });
        AddStepLegend(multiJobStartBody, 380);
        AddActionTo(multiJobStartBody, "4) Port1 Proceed SlotMap",
            async () =>
            {
                using var dlg = new Dialogs.S3F17ProceedDialog(_txtCarrierId.Text.Trim(), (byte)_nudPortId.Value);
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                _txtCarrierId.Text = dlg.CarrierId;
                _nudPortId.Value = dlg.PortId;

                _multiLotContentMapSlotIds = NormalizeSlotIds(dlg.OccupiedSlotIds);
                ResetMultiLotWaferSlotProgress(_multiLotContentMapSlotIds.Count);

                var payload = dlg.IncludeContentMap
                    ? Secs.SecsMessageFactory.S3F17ProceedWithCarrierContentMap(
                        dlg.CarrierId,
                        dlg.PortId,
                        dlg.ContentMapCattrid,
                        dlg.SlotEntries)
                    : Secs.SecsMessageFactory.S3F17ProceedWithCarrier(dlg.CarrierId, dlg.PortId);

                await SendAsync("L1Normal_Multi_S3F17_Port1_ProceedWithCarrier", 3, 17, payload).ConfigureAwait(true);
            }, 260);
        multiJobStartBody.Controls.Add(new Label
        {
            Text = "Note: Enter at least 2 wafers in Content Map to test PJ1 and PJ2",
            Width = 380,
            ForeColor = Theme.ThemeHelper.TextMid,
            Margin = new Padding(6, 0, 6, 2)
        });
        AddActionTo(multiJobStartBody, "5) Create ProcessJob (PJ1)",
            async () =>
            {
                var defaultSlots = PickDefaultMultiLotSlots(firstJob: true);
                using var dlg = new Dialogs.ProcessJobCreateDialog(
                    _multiLotPj1Id,
                    _txtRecipeId.Text.Trim(),
                    _txtCarrierId.Text.Trim(),
                    defaultSlots,
                    GetMultiLotContentMapSlotIds());
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                _multiLotPj1Id = dlg.ProcessJobId;
                _multiLotPj1SlotIds = dlg.GetSlotIds();
                _txtRecipeId.Text = dlg.Ppid;
                _txtCarrierId.Text = dlg.CarrierId;
                UpdateMultiLotPjHints();

                await SendAsync("L1Normal_Multi_S16F15_CreatePJ1", 16, 15,
                    Secs.SecsMessageFactory.S16F15ProcessJobCreate(dlg.ProcessJobId, dlg.Ppid, dlg.CarrierId, _multiLotPj1SlotIds)).ConfigureAwait(true);
            }, 260);
        AddActionTo(multiJobStartBody, "6) Create ProcessJob (PJ2)",
            async () =>
            {
                var defaultSlots = PickDefaultMultiLotSlots(firstJob: false);
                using var dlg = new Dialogs.ProcessJobCreateDialog(
                    _multiLotPj2Id,
                    _txtRecipeId.Text.Trim(),
                    _txtCarrierId.Text.Trim(),
                    defaultSlots,
                    GetMultiLotContentMapSlotIds());
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                _multiLotPj2Id = dlg.ProcessJobId;
                _multiLotPj2SlotIds = dlg.GetSlotIds();
                _txtRecipeId.Text = dlg.Ppid;
                _txtCarrierId.Text = dlg.CarrierId;
                UpdateMultiLotPjHints();

                await SendAsync("L1Normal_Multi_S16F15_CreatePJ2", 16, 15,
                    Secs.SecsMessageFactory.S16F15ProcessJobCreate(dlg.ProcessJobId, dlg.Ppid, dlg.CarrierId, _multiLotPj2SlotIds)).ConfigureAwait(true);
            }, 260);
        AddActionTo(multiJobStartBody, "7) Create ControlJob (CJ1)",
            async () =>
            {
                using var dlg = new Dialogs.ControlJobCreateDialog(
                    _txtControlJobId.Text.Trim(),
                    [_multiLotPj1Id, _multiLotPj2Id],
                    _txtCarrierId.Text.Trim());
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                _txtControlJobId.Text = dlg.ControlJobId;
                _txtCarrierId.Text = dlg.CarrierId;
                _currentNormalControlJobId = dlg.ControlJobId;
                const byte processOrderMgmt = 2;

                var processJobs = dlg.GetProcessJobIds()
                    .Select<string, (string ProcessJobId, IEnumerable<string> SlotIds)>(id => (
                        id,
                        string.Equals(id, _multiLotPj1Id, StringComparison.OrdinalIgnoreCase)
                            ? _multiLotPj1SlotIds
                            : string.Equals(id, _multiLotPj2Id, StringComparison.OrdinalIgnoreCase)
                                ? _multiLotPj2SlotIds
                                : Array.Empty<string>()))
                    .ToArray();

                var totalSlotCount = processJobs
                    .SelectMany(job => job.SlotIds)
                    .Where(slotId => !string.IsNullOrWhiteSpace(slotId))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                ResetMultiLotWaferSlotProgress(totalSlotCount > 0 ? totalSlotCount : GetMultiLotContentMapSlotIds().Count);

                TrackExpectedReport("8) Control Job CJ1 Start Event");
                await SendControlJobCreateWithFallbackAsync(
                    "L1Normal_Multi_S14F9_CreateCJ1",
                    dlg.ControlJobId,
                    dlg.CarrierId,
                    processJobs,
                    processOrderMgmt).ConfigureAwait(true);
            }, 260);

        leftMultiLot.Controls.Add(multiLoadSection, 0, 0);
        leftMultiLot.Controls.Add(multiJobStartSection, 0, 1);

        var multiProcessSection = CreateSection("3. Job Processing for Multi Lot Testing");
        var multiProcessBody = CreateSectionBody(multiProcessSection);
        AddReportRow(multiProcessBody, "8) Control Job CJ1 Start Event",
            () => WaitPrimaryAsync("L1Normal_Multi_Wait_S6F11_CJ1Start", 6, 11, 30));
        AddReportRow(multiProcessBody, "9) Process Job PJ1 Start Event",
            () => WaitPrimaryAsync("L1Normal_Multi_Wait_S6F11_PJ1Start", 6, 11, 30));
        _lblMultiLotPj1StartHint = AddInlineHint(multiProcessBody, $"PJ ID: {_multiLotPj1Id}");
        AddReportRow(multiProcessBody, "10) Process Job PJ2 Start Event",
            () => WaitPrimaryAsync("L1Normal_Multi_Wait_S6F11_PJ2Start", 6, 11, 30));
        _lblMultiLotPj2StartHint = AddInlineHint(multiProcessBody, $"PJ ID: {_multiLotPj2Id}");
        AddReportRow(multiProcessBody, "11) Wafer Process Start Event",
            () => WaitPrimaryAsync("L1Normal_Multi_Wait_S6F11_WaferStart", 6, 11, 30));
        _lblMultiLotWaferStartSlotProgress = AddInlineHint(multiProcessBody, "Slot ID: 0/1 completed");
        AddReportRow(multiProcessBody, "12) Wafer Process End Event",
            () => WaitPrimaryAsync("L1Normal_Multi_Wait_S6F11_WaferEnd", 6, 11, 30));
        _lblMultiLotWaferEndSlotProgress = AddInlineHint(multiProcessBody, "Slot ID: 0/1 completed");
        AddReportRow(multiProcessBody, "13) Process Job PJ1 Completed Event",
            () => WaitPrimaryAsync("L1Normal_Multi_Wait_S6F11_PJ1Complete", 6, 11, 30));
        _lblMultiLotPj1CompletedHint = AddInlineHint(multiProcessBody, $"PJ ID: {_multiLotPj1Id}");
        AddReportRow(multiProcessBody, "14) Process Job PJ2 Completed Event",
            () => WaitPrimaryAsync("L1Normal_Multi_Wait_S6F11_PJ2Complete", 6, 11, 30));
        _lblMultiLotPj2CompletedHint = AddInlineHint(multiProcessBody, $"PJ ID: {_multiLotPj2Id}");
        AddReportRow(multiProcessBody, "15) Control Job CJ1 Completed Event",
            () => WaitPrimaryAsync("L1Normal_Multi_Wait_S6F11_CJ1Complete", 6, 11, 30));

        gridMultiLot.Controls.Add(leftMultiLot, 0, 0);
        gridMultiLot.Controls.Add(multiProcessSection, 1, 0);
    }

    private IReadOnlyList<string> GetMultiLotContentMapSlotIds()
    {
        return _multiLotContentMapSlotIds;
    }

    private static IReadOnlyList<string> NormalizeSlotIds(IReadOnlyList<string> slotIds)
    {
        return slotIds
            .Select(slotId => slotId.Trim())
            .Where(slotId => !string.IsNullOrWhiteSpace(slotId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> PickDefaultMultiLotSlots(bool firstJob)
    {
        var slots = GetMultiLotContentMapSlotIds();
        if (slots.Count <= 1)
        {
            return slots;
        }

        var splitIndex = Math.Max(1, slots.Count / 2);
        return firstJob
            ? slots.Take(splitIndex).ToArray()
            : slots.Skip(splitIndex).ToArray();
    }

    private void TrackExpectedReport(string reportText)
    {
        PendingReportAction = reportText;
        _armedReportActions.Add((_activeReportScope, reportText));
        _pendingReportActionAt = DateTime.Now;
    }

    private void OnPrimaryMessageReceived(Secs4Net.PrimaryMessageWrapper wrapper)
    {
        if (wrapper.PrimaryMessage.S != 6 || wrapper.PrimaryMessage.F != 11)
        {
            return;
        }

        // Snapshot data immediately on receive thread. Avoid parsing wrapper later on UI thread.
        var raw = wrapper.PrimaryMessage.ToString();
        var interpreted = HostSimTester.App.Secs.SecsReplyInterpreter.DescribePrimary(wrapper);
        var interpretedText = string.Join(" | ", interpreted);
        var combinedText = raw + "\n" + interpretedText;

        var hasCeid = HostSimTester.App.Secs.SecsPayload.TryGetS6F11Ceid(wrapper.PrimaryMessage, out var ceid);
        if (!hasCeid)
        {
            hasCeid = TryExtractCeid(interpretedText, out ceid) || TryExtractCeid(raw, out ceid);
        }

        var hasRptid = HostSimTester.App.Secs.SecsPayload.TryGetS6F11FirstRptid(wrapper.PrimaryMessage, out var rptid);

        // Only CarrierIDRead event (CEID=1000) is allowed to update Carrier ID.
        string? carrierIdFromEvent = null;
        string? carrierIdSource = null;
        string? carrierExtractDebug = null;
        if (hasCeid && ceid == 1000 &&
            TryExtractCarrierIdForCarrierRead(wrapper.PrimaryMessage, raw, combinedText, out var extractedCarrierId))
        {
            carrierIdFromEvent = extractedCarrierId;
            carrierIdSource = "CEID=1000";
        }
        else if (hasCeid && ceid == 1000)
        {
            carrierExtractDebug = BuildCarrierExtractDebug(wrapper.PrimaryMessage, raw, combinedText);
        }
        else if (TryShouldSupplementCarrierId(hasCeid ? ceid : null, hasRptid ? rptid : null, combinedText) &&
                 TryExtractCarrierIdFromSupplementEvent(wrapper.PrimaryMessage, raw, combinedText, out var supplementalCarrierId))
        {
            carrierIdFromEvent = supplementalCarrierId;
            carrierIdSource = hasCeid
                ? $"CEID={ceid} supplement"
                : "S6F11 supplement";
        }

        // Final fallback: allow any S6F11 payload that clearly contains a valid Carrier ID.
        // Some equipment reports CarrierID on CEIDs other than 1000 and without explicit event keywords.
        if (string.IsNullOrWhiteSpace(carrierIdFromEvent) &&
            !(hasCeid && IsJobLifecycleCeid(ceid)) &&
            TryExtractCarrierId(wrapper.PrimaryMessage, combinedText, out var fallbackCarrierId))
        {
            carrierIdFromEvent = fallbackCarrierId;
            carrierIdSource = hasCeid
                ? $"CEID={ceid} generic"
                : "S6F11 generic";
        }

        var snapshot = new S6F11Snapshot(
            raw,
            interpretedText,
            combinedText,
            wrapper.PrimaryMessage,
            hasCeid ? ceid : null,
            hasRptid ? rptid : null,
            carrierIdFromEvent,
            carrierIdSource,
            carrierExtractDebug);

        BeginInvoke(() => HandleS6F11Report(snapshot));
    }

    private void HandleS6F11Report(S6F11Snapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.CarrierIdFromEvent) && IsCarrierIdUpdateAllowed())
        {
            _txtCarrierId.Text = snapshot.CarrierIdFromEvent;
            var source = string.IsNullOrWhiteSpace(snapshot.CarrierIdSource) ? "S6F11" : snapshot.CarrierIdSource;
            AppendResult($"[INFO] Carrier ID updated from {source}: {snapshot.CarrierIdFromEvent}");
        }

        if (IsSlotMapDataUpdateAllowed() && TryExtractSlotMapData(snapshot.Message, out var slotMapData))
        {
            if (!string.Equals(_txtSlotMapData.Text, slotMapData, StringComparison.Ordinal))
            {
                _txtSlotMapData.Text = slotMapData;
                AppendResult($"[INFO] SlotMap updated: {slotMapData}");
            }
        }

        if (!snapshot.Ceid.HasValue)
        {
            if (TryExtractReportByPayload(snapshot, out var actionByPayloadWithoutCeid))
            {
                if (!IsReportActionArmed(actionByPayloadWithoutCeid))
                {
                    PushS6F11Monitor(snapshot.Raw, $"ignored payload -> {actionByPayloadWithoutCeid}; step not started");
                    return;
                }

                if (!IsReportForCurrentJob(actionByPayloadWithoutCeid, snapshot, out var payloadRejectReason))
                {
                    PushS6F11Monitor(snapshot.Raw, $"ignored payload -> {actionByPayloadWithoutCeid}; {payloadRejectReason}");
                    return;
                }

                PushS6F11Monitor(snapshot.Raw, $"payload -> {actionByPayloadWithoutCeid}");
                MarkReportPass(actionByPayloadWithoutCeid, "payload");
                return;
            }

            if (TryExtractReportByKeyword(snapshot.CombinedText, out var actionByKeywordWithoutCeid))
            {
                if (!IsReportActionArmed(actionByKeywordWithoutCeid))
                {
                    PushS6F11Monitor(snapshot.Raw, $"ignored keyword -> {actionByKeywordWithoutCeid}; step not started");
                    return;
                }

                if (!IsReportForCurrentJob(actionByKeywordWithoutCeid, snapshot, out var keywordRejectReason))
                {
                    PushS6F11Monitor(snapshot.Raw, $"ignored keyword -> {actionByKeywordWithoutCeid}; {keywordRejectReason}");
                    return;
                }

                PushS6F11Monitor(snapshot.Raw, $"keyword -> {actionByKeywordWithoutCeid}");
                MarkReportPass(actionByKeywordWithoutCeid, "keyword");
                return;
            }

            PushS6F11Monitor(snapshot.Raw, $"CEID parse failed | raw={Shorten(snapshot.Raw, 80)} | interpreted={Shorten(snapshot.InterpretedText, 80)}");
            return;
        }

        var ceid = snapshot.Ceid.Value;

        Logger.Info("S6F11 page summary: CEID={ceid}, EventName={eventName}, RPTID={rptid}, Pending=[{pending}]",
            ceid,
            HostSimTester.App.Secs.SecsPayload.GetEventName(ceid),
            snapshot.Rptid?.ToString() ?? string.Empty,
            PendingReportAction ?? string.Empty);

        AppendResult(snapshot.Rptid.HasValue
            ? $"[INFO] S6F11 received CEID={ceid}, RPTID={snapshot.Rptid.Value}"
            : $"[INFO] S6F11 received CEID={ceid}");

        if (ceid == 1000 && string.IsNullOrWhiteSpace(snapshot.CarrierIdFromEvent))
        {
            AppendResult("[WARN] CEID=1000 received but Carrier ID not found in payload (or empty). Please check equipment CarrierIDRead report content.");
            AppendResult($"[DEBUG] CEID=1000 report dump: {BuildS6F11ValueDebugSummary(snapshot.Raw, snapshot.Message)}");
            if (!string.IsNullOrWhiteSpace(snapshot.CarrierExtractDebug))
            {
                AppendResult($"[DEBUG] CEID=1000 carrier parse detail: {snapshot.CarrierExtractDebug}");
            }
        }

        if (TryGetKnownReportAction(ceid, out var knownAction))
        {
            if (!IsReportActionArmed(knownAction))
            {
                PushS6F11Monitor(snapshot.Raw, $"ignored known {_activeReportScope} CEID={ceid} -> {knownAction}; step not started");
                return;
            }

            if (!IsReportForCurrentJob(knownAction, snapshot, out var knownRejectReason))
            {
                PushS6F11Monitor(snapshot.Raw, $"ignored known {_activeReportScope} CEID={ceid} -> {knownAction}; {knownRejectReason}");
                return;
            }

            PushS6F11Monitor(snapshot.Raw, $"known {_activeReportScope} CEID={ceid} -> {knownAction}");
            MarkReportPass(knownAction, $"CEID={ceid} known");
            return;
        }

        if (_activeReportScope == ReportScope.MultiLot && TryMarkPendingReportForCeid(ceid, snapshot))
        {
            return;
        }

        if (_reportActionByCeid.TryGetValue((_activeReportScope, ceid), out var mappedAction))
        {
            if (!IsReportActionArmed(mappedAction))
            {
                PushS6F11Monitor(snapshot.Raw, $"ignored mapped {_activeReportScope} CEID={ceid} -> {mappedAction}; step not started");
                return;
            }

            if (!IsReportForCurrentJob(mappedAction, snapshot, out var mappedRejectReason))
            {
                PushS6F11Monitor(snapshot.Raw, $"ignored mapped {_activeReportScope} CEID={ceid} -> {mappedAction}; {mappedRejectReason}");
                return;
            }

            PushS6F11Monitor(snapshot.Raw, $"mapped {_activeReportScope} CEID={ceid} -> {mappedAction}");
            MarkReportPass(mappedAction, $"CEID={ceid}");
            return;
        }

        if (TryExtractReportByPayload(snapshot, out var actionByPayload))
        {
            if (!IsReportActionArmed(actionByPayload))
            {
                PushS6F11Monitor(snapshot.Raw, $"ignored payload -> {actionByPayload}; step not started");
                return;
            }

            if (!IsReportForCurrentJob(actionByPayload, snapshot, out var payloadRejectReason))
            {
                PushS6F11Monitor(snapshot.Raw, $"ignored payload -> {actionByPayload}; {payloadRejectReason}");
                return;
            }

            PushS6F11Monitor(snapshot.Raw, $"payload -> {actionByPayload}");
            MarkReportPass(actionByPayload, "payload");
            return;
        }

        if (TryExtractReportByKeyword(snapshot.CombinedText, out var actionByKeyword))
        {
            if (!IsReportActionArmed(actionByKeyword))
            {
                PushS6F11Monitor(snapshot.Raw, $"ignored keyword -> {actionByKeyword}; step not started");
                return;
            }

            if (!IsReportForCurrentJob(actionByKeyword, snapshot, out var keywordRejectReason))
            {
                PushS6F11Monitor(snapshot.Raw, $"ignored keyword -> {actionByKeyword}; {keywordRejectReason}");
                return;
            }

            PushS6F11Monitor(snapshot.Raw, $"keyword -> {actionByKeyword}");
            MarkReportPass(actionByKeyword, "keyword");
            return;
        }

        if (TryMarkPendingReportForCeid(ceid, snapshot))
        {
            return;
        }

        PushS6F11Monitor(snapshot.Raw, $"unmapped CEID={ceid}");
    }

    private sealed record S6F11Snapshot(
        string Raw,
        string InterpretedText,
        string CombinedText,
        Secs4Net.SecsMessage Message,
        uint? Ceid,
        uint? Rptid,
        string? CarrierIdFromEvent,
        string? CarrierIdSource,
        string? CarrierExtractDebug);

    private bool TryGetKnownReportAction(uint ceid, out string actionText)
    {
        var map = _activeReportScope == ReportScope.MultiLot
            ? MultiLotReportByCeid
            : NormalReportByCeid;

        return map.TryGetValue(ceid, out actionText!);
    }

    private bool IsReportForCurrentJob(string actionText, S6F11Snapshot snapshot, out string rejectReason)
    {
        rejectReason = string.Empty;
        var expectedId = GetExpectedJobIdForAction(actionText);
        if (string.IsNullOrWhiteSpace(expectedId))
        {
            return true;
        }

        if (ContainsToken(snapshot.CombinedText, expectedId))
        {
            return true;
        }

        // Some tools report CEID=96 (Process Job Start) with CJID-only payload.
        // Accept current CJID for Normal "12) Process Job Start Event" to avoid false negative.
        if (_activeReportScope == ReportScope.Normal &&
            snapshot.Ceid == 96 &&
            string.Equals(actionText, "12) Process Job Start Event", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(_currentNormalControlJobId) &&
            ContainsToken(snapshot.CombinedText, _currentNormalControlJobId))
        {
            return true;
        }

        rejectReason = $"payload does not contain expected ID '{expectedId}'";
        return false;
    }

    private string GetExpectedJobIdForAction(string actionText)
    {
        if (_activeReportScope == ReportScope.MultiLot)
        {
            if (ContainsActionName(actionText, "Control Job"))
            {
                return _currentNormalControlJobId;
            }

            if (ContainsActionName(actionText, "PJ1"))
            {
                return _multiLotPj1Id;
            }

            if (ContainsActionName(actionText, "PJ2"))
            {
                return _multiLotPj2Id;
            }

            return string.Empty;
        }

        if (ContainsActionName(actionText, "Control Job"))
        {
            return _currentNormalControlJobId;
        }

        if (ContainsActionName(actionText, "Process Job"))
        {
            return _currentNormalProcessJobId;
        }

        return string.Empty;
    }

    private static bool ContainsActionName(string actionText, string name)
        => actionText.Contains(name, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsToken(string text, string token)
        => !string.IsNullOrWhiteSpace(text) &&
            text.Contains(token, StringComparison.OrdinalIgnoreCase);

    private bool TryMarkPendingReportForCeid(uint ceid, S6F11Snapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(PendingReportAction))
        {
            return false;
        }

        if (StrictCeidOnlyActions.Contains(PendingReportAction))
        {
            PushS6F11Monitor(snapshot.Raw, $"unmapped CEID={ceid}; strict CEID match required for {PendingReportAction}");
            return true;
        }

        var learnedAction = PendingReportAction!;
        if (!IsReportForCurrentJob(learnedAction, snapshot, out var rejectReason))
        {
            PushS6F11Monitor(snapshot.Raw, $"ignored pending {_activeReportScope} CEID={ceid} -> {learnedAction}; {rejectReason}");
            return true;
        }

        if (!IsMultiLotSequentialAction(learnedAction))
        {
            _reportActionByCeid[(_activeReportScope, ceid)] = learnedAction;
        }

        PushS6F11Monitor(snapshot.Raw, $"pending {_activeReportScope} CEID={ceid} -> {learnedAction}");
        MarkReportPass(learnedAction, $"CEID={ceid} pending");
        return true;
    }

    private bool IsMultiLotSequentialAction(string actionText)
    {
        return _activeReportScope == ReportScope.MultiLot &&
            (string.Equals(actionText, "9) Process Job PJ1 Start Event", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(actionText, "10) Process Job PJ2 Start Event", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(actionText, "13) Process Job PJ1 Completed Event", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(actionText, "14) Process Job PJ2 Completed Event", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(actionText, "15) Control Job CJ1 Completed Event", StringComparison.OrdinalIgnoreCase));
    }

    private bool TryGetReportLamp(string actionText, out Panel lamp)
        => _reportLamps.TryGetValue((_activeReportScope, actionText), out lamp!);

    private bool TryGetReportLamp(ReportScope scope, string actionText, out Panel lamp)
        => _reportLamps.TryGetValue((scope, actionText), out lamp!);

    private bool IsReportActionArmed(string actionText)
    {
        return _armedReportActions.Contains((_activeReportScope, actionText));
    }

    private bool IsCarrierIdUpdateAllowed()
    {
        return _activeReportScope == ReportScope.Normal
            ? IsReportActionArmed("4) Auto Read Carrier ID") || IsReportActionArmed("5) Proceed With Carrier")
            : IsReportActionArmed("1) Testing Port1 Carrier ID Read Event") || IsReportActionArmed("3) SlotMap Event");
    }

    private bool IsSlotMapDataUpdateAllowed()
    {
        return _activeReportScope == ReportScope.Normal &&
            (IsReportActionArmed("7) Auto Open Door Event") || IsReportActionArmed("8) Slot Mapping Data"));
    }

    private void PushS6F11Monitor(string raw, string verdict)
    {
        // Monitor window removed by request; keep method as a no-op to avoid touching event flow.
        Logger.Info("S6F11 mapping verdict: {verdict}; raw={raw}", verdict, Shorten(raw, 500));
    }

    private bool TryExtractReportByPayload(S6F11Snapshot snapshot, out string actionText)
    {
        actionText = string.Empty;
        var upper = snapshot.CombinedText.ToUpperInvariant();

        if ((snapshot.Rptid.HasValue && SlotMapRptids.Contains(snapshot.Rptid.Value)) ||
            (IsSlotMapExpected() && ContainsSlotMapPayload(snapshot.Message)))
        {
            actionText = ResolveScopedAction("8) Slot Mapping Data");
            return true;
        }

        if (string.Equals(PendingReportAction, "7) Auto Open Door Event", StringComparison.OrdinalIgnoreCase) &&
            (TryExtractCarrierId(snapshot.Message, snapshot.CombinedText, out _) ||
             !string.IsNullOrWhiteSpace(snapshot.CarrierIdFromEvent)))
        {
            actionText = "7) Auto Open Door Event";
            return true;
        }

        if (string.Equals(PendingReportAction, "12) Process Job Start Event", StringComparison.OrdinalIgnoreCase) &&
            (upper.Contains("PRJOBID", StringComparison.Ordinal) ||
             upper.Contains("PRJOBSTATE", StringComparison.Ordinal) ||
             upper.Contains("PJSTATUS", StringComparison.Ordinal)))
        {
            actionText = ResolveScopedAction("12) Process Job Start Event");
            return true;
        }

        if (string.Equals(PendingReportAction, "15) Process Job Completed Event", StringComparison.OrdinalIgnoreCase) &&
            (upper.Contains("PRJOBID", StringComparison.Ordinal) ||
             upper.Contains("PRJOBSTATE", StringComparison.Ordinal) ||
             upper.Contains("PJSTATUS", StringComparison.Ordinal)))
        {
            actionText = ResolveScopedAction("15) Process Job Completed Event");
            return true;
        }

        return false;
    }

    private bool IsSlotMapExpected()
    {
        return string.Equals(PendingReportAction, "7) Auto Open Door Event", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(PendingReportAction, "8) Slot Mapping Data", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(PendingReportAction, "3) SlotMap Event", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveScopedAction(string normalActionText)
    {
        if (_activeReportScope != ReportScope.MultiLot)
        {
            return normalActionText;
        }

        if (string.Equals(PendingReportAction, "3) SlotMap Event", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalActionText, "8) Slot Mapping Data", StringComparison.OrdinalIgnoreCase))
        {
            return "3) SlotMap Event";
        }

        if (string.Equals(PendingReportAction, "8) Control Job CJ1 Start Event", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalActionText, "11) Control Job Start Event", StringComparison.OrdinalIgnoreCase))
        {
            return "8) Control Job CJ1 Start Event";
        }

        if (string.Equals(PendingReportAction, "9) Process Job PJ1 Start Event", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalActionText, "12) Process Job Start Event", StringComparison.OrdinalIgnoreCase))
        {
            return "9) Process Job PJ1 Start Event";
        }

        if (string.Equals(PendingReportAction, "10) Process Job PJ2 Start Event", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalActionText, "12) Process Job Start Event", StringComparison.OrdinalIgnoreCase))
        {
            return "10) Process Job PJ2 Start Event";
        }

        if (string.Equals(PendingReportAction, "11) Wafer Process Start Event", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalActionText, "13) Wafer Process Start Event", StringComparison.OrdinalIgnoreCase))
        {
            return "11) Wafer Process Start Event";
        }

        if (string.Equals(PendingReportAction, "12) Wafer Process End Event", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalActionText, "14) Wafer Process End Event", StringComparison.OrdinalIgnoreCase))
        {
            return "12) Wafer Process End Event";
        }

        if (string.Equals(PendingReportAction, "13) Process Job PJ1 Completed Event", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalActionText, "15) Process Job Completed Event", StringComparison.OrdinalIgnoreCase))
        {
            return "13) Process Job PJ1 Completed Event";
        }

        if (string.Equals(PendingReportAction, "14) Process Job PJ2 Completed Event", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalActionText, "15) Process Job Completed Event", StringComparison.OrdinalIgnoreCase))
        {
            return "14) Process Job PJ2 Completed Event";
        }

        if (string.Equals(PendingReportAction, "15) Control Job CJ1 Completed Event", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalActionText, "16) Control Job Completed Event", StringComparison.OrdinalIgnoreCase))
        {
            return "15) Control Job CJ1 Completed Event";
        }

        return normalActionText;
    }

    private async Task SendControlJobCreateWithFallbackAsync(
        string operationName,
        string controlJobId,
        string carrierId,
        IEnumerable<string> processJobIds,
        byte preferredProcessOrderMgmt,
        string recipeId,
        IEnumerable<string>? slotIds = null)
    {
        _ = preferredProcessOrderMgmt;

        var normalizedProcessJobIds = processJobIds
            .Select(id => id.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var attempts = new (string AttemptName, string AttemptDesc, Secs4Net.Item Payload, bool RequiresStartCommand)[]
        {
            (
                operationName,
                "E94 standard payload (Equipment/ControlJob, ProcessOrderMgmt=2, StartMethod=True)",
                Secs.SecsMessageFactory.S14F9ControlJobCreate(
                    controlJobId,
                    carrierId,
                    normalizedProcessJobIds,
                    processOrderMgmt: 2,
                    slotIds: null,
                    startMethod: true),
                false
            ),
            (
                $"{operationName}_Compat_TEST",
                "E94 compatibility payload (TEST/CONTROLJOB, ProcessOrderMgmt=3, StartMethod=False, DataPlanTest)",
                Secs.SecsMessageFactory.S14F9ControlJobCreateTestDomain(
                    controlJobId,
                    carrierId,
                    normalizedProcessJobIds,
                    processOrderMgmt: 3,
                    startMethod: false,
                    dataCollectionPlan: "DataPlanTest"),
                true
            )
        };

        for (var i = 0; i < attempts.Length; i++)
        {
            var attempt = attempts[i];

            if (attempt.RequiresStartCommand)
            {
                foreach (var processJobId in normalizedProcessJobIds)
                {
                    var prepName = $"{attempt.AttemptName}_PreparePJ_{processJobId}";
                    var prepOk = await SendAllowNakAsync(
                        prepName,
                        14,
                        9,
                        Secs.SecsMessageFactory.S14F9ProcessJobCreateTestDomain(
                            processJobId,
                            recipeId,
                            carrierId,
                            slotIds,
                            processStart: true)).ConfigureAwait(true);
                    if (!prepOk)
                    {
                        AppendResult($"[WARN] Compat TEST fallback: TEST/PROCESSJOB prepare not accepted for {processJobId}; continue with TEST/CONTROLJOB create.");
                    }
                }
            }

            var ok = await SendAllowNakAsync(
                attempt.AttemptName,
                14,
                9,
                attempt.Payload).ConfigureAwait(true);

            if (ok)
            {
                if (i > 0)
                {
                    AppendResult($"[INFO] ControlJob create succeeded by fallback profile: {attempt.AttemptDesc}.");
                }

                if (attempt.RequiresStartCommand)
                {
                    AppendResult("[INFO] Compat TEST profile uses StartMethod=False; send S16F27 START to enter measurement flow.");
                    await SendAsync(
                        $"{attempt.AttemptName}_S16F27_Start",
                        16,
                        27,
                        Secs.SecsMessageFactory.S16F27ControlJobCommand(controlJobId, 1)).ConfigureAwait(true);
                }

                return;
            }

            AppendResult($"[WARN] ControlJob create rejected, next profile: {attempt.AttemptDesc}.");
        }

        throw new InvalidOperationException($"{operationName} returned NAK/ERROR for all retry combinations.");
    }

    private async Task SendControlJobCreateWithFallbackAsync(
        string operationName,
        string controlJobId,
        string carrierId,
        IEnumerable<(string ProcessJobId, IEnumerable<string> SlotIds)> processJobs,
        byte preferredProcessOrderMgmt)
    {
        _ = preferredProcessOrderMgmt;

        var normalizedProcessJobIds = processJobs
            .Select(job => job.ProcessJobId.Trim())
            .Where(processJobId => !string.IsNullOrWhiteSpace(processJobId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var controlJobProcessJobs = normalizedProcessJobIds
            .Select(processJobId => (ProcessJobId: processJobId, SlotIds: Enumerable.Empty<string>()))
            .ToArray();

        var attempts = new (string AttemptName, string AttemptDesc, Secs4Net.Item Payload, bool RequiresStartCommand)[]
        {
            (
                operationName,
                "E94 standard payload (Equipment/ControlJob, ProcessOrderMgmt=2, StartMethod=True)",
                Secs.SecsMessageFactory.S14F9ControlJobCreate(
                    controlJobId,
                    carrierId,
                    controlJobProcessJobs,
                    processOrderMgmt: 2,
                    startMethod: true),
                false
            ),
            (
                $"{operationName}_Compat_TEST",
                "E94 compatibility payload (TEST/CONTROLJOB, ProcessOrderMgmt=3, StartMethod=False, DataPlanTest)",
                Secs.SecsMessageFactory.S14F9ControlJobCreateTestDomain(
                    controlJobId,
                    carrierId,
                    normalizedProcessJobIds,
                    processOrderMgmt: 3,
                    startMethod: false,
                    dataCollectionPlan: "DataPlanTest"),
                true
            )
        };

        for (var i = 0; i < attempts.Length; i++)
        {
            var attempt = attempts[i];
            var ok = await SendAllowNakAsync(
                attempt.AttemptName,
                14,
                9,
                attempt.Payload).ConfigureAwait(true);

            if (ok)
            {
                if (i > 0)
                {
                    AppendResult($"[INFO] ControlJob create succeeded by fallback profile: {attempt.AttemptDesc}.");
                }

                if (attempt.RequiresStartCommand)
                {
                    AppendResult("[INFO] Compat TEST profile uses StartMethod=False; send S16F27 START to enter measurement flow.");
                    await SendAsync(
                        $"{attempt.AttemptName}_S16F27_Start",
                        16,
                        27,
                        Secs.SecsMessageFactory.S16F27ControlJobCommand(controlJobId, 1)).ConfigureAwait(true);
                }

                return;
            }

            AppendResult($"[WARN] ControlJob create rejected, next profile: {attempt.AttemptDesc}.");
        }

        throw new InvalidOperationException($"{operationName} returned NAK/ERROR for all retry combinations.");
    }

    private static bool TryShouldSupplementCarrierId(uint? ceid, uint? rptid, string combinedText)
    {
        if (ceid == 1000)
        {
            return false;
        }

        if (ceid.HasValue && IsJobLifecycleCeid(ceid.Value))
        {
            return false;
        }

        if (rptid.HasValue && rptid.Value > 0 && rptid.Value <= 10)
        {
            // Some tools emit CarrierID in early loading reports. Keep this conservative.
            return true;
        }

        var upper = combinedText.ToUpperInvariant();
        return CarrierIdSupplementEventKeywords.Any(k => upper.Contains(k, StringComparison.Ordinal));
    }

    private static bool TryExtractCarrierIdFromSupplementEvent(Secs4Net.SecsMessage message, string raw, string text, out string carrierId)
    {
        carrierId = string.Empty;

        var aliasMatches = Regex.Matches(
            raw,
            @"<([A-Za-z0-9]+)\s*\[\d+\]\s*([^>]+)>\s*/\*\s*([^*]+?)\s*\*/",
            RegexOptions.Singleline);

        foreach (Match m in aliasMatches.Cast<Match>())
        {
            var alias = m.Groups[3].Value.Trim().ToUpperInvariant();
            if (!alias.Contains("CARRIERID", StringComparison.Ordinal) &&
                !alias.Contains("FOUPID", StringComparison.Ordinal) &&
                !alias.Contains("FOUP ID", StringComparison.Ordinal))
            {
                continue;
            }

            var value = m.Groups[2].Value.Trim().Trim('\'', '"', ' ');
            if (!IsLikelyCarrierId(value))
            {
                continue;
            }

            carrierId = value;
            return true;
        }

        var labelMatch = Regex.Match(
            text,
            @"CARRIER\s*[_\- ]?ID\s*[:=]\s*([A-Za-z0-9._\-]{3,64})",
            RegexOptions.IgnoreCase);
        if (labelMatch.Success)
        {
            var value = labelMatch.Groups[1].Value.Trim();
            if (IsLikelyCarrierId(value))
            {
                carrierId = value;
                return true;
            }
        }

        foreach (var binaryCandidate in ExtractAsciiCandidatesFromBinaryRaw(raw))
        {
            if (!IsLikelyCarrierId(binaryCandidate))
            {
                continue;
            }

            carrierId = binaryCandidate;
            return true;
        }

        if (TryExtractCarrierIdFromItemTree(message.SecsItem, out var treeCarrierId))
        {
            carrierId = treeCarrierId;
            return true;
        }

        return false;
    }

    private void ResetWaferSlotProgress(int slotCount)
    {
        _expectedWaferSlotCount = Math.Max(1, slotCount);
        _waferEndCount = 0;
        UpdateWaferSlotProgressLabels();

        if (TryGetReportLamp(ReportScope.Normal, "13) Wafer Process Start Event", out var startLamp))
        {
            startLamp.BackColor = Color.FromArgb(160, 170, 180);
        }

        if (TryGetReportLamp(ReportScope.Normal, "14) Wafer Process End Event", out var endLamp))
        {
            endLamp.BackColor = Color.FromArgb(160, 170, 180);
        }
    }

    private void ResetMultiLotWaferSlotProgress(int slotCount)
    {
        _multiLotExpectedWaferSlotCount = Math.Max(1, slotCount);
        _multiLotWaferEndCount = 0;
        UpdateMultiLotWaferSlotProgressLabels();

        if (TryGetReportLamp(ReportScope.MultiLot, "11) Wafer Process Start Event", out var startLamp))
        {
            startLamp.BackColor = Color.FromArgb(160, 170, 180);
        }

        if (TryGetReportLamp(ReportScope.MultiLot, "12) Wafer Process End Event", out var endLamp))
        {
            endLamp.BackColor = Color.FromArgb(160, 170, 180);
        }
    }

    private void UpdateWaferSlotProgressLabels()
    {
        _lblWaferStartSlotProgress.Text = $"Slot ID: {_waferEndCount}/{_expectedWaferSlotCount} completed";
        _lblWaferEndSlotProgress.Text = $"Slot ID: {_waferEndCount}/{_expectedWaferSlotCount} completed";
    }

    private void UpdateMultiLotWaferSlotProgressLabels()
    {
        _lblMultiLotWaferStartSlotProgress.Text = $"Slot ID: {_multiLotWaferEndCount}/{_multiLotExpectedWaferSlotCount} completed";
        _lblMultiLotWaferEndSlotProgress.Text = $"Slot ID: {_multiLotWaferEndCount}/{_multiLotExpectedWaferSlotCount} completed";
    }

    private void UpdateMultiLotPjHints()
    {
        _lblMultiLotPj1StartHint.Text = $"PJ ID: {_multiLotPj1Id}";
        _lblMultiLotPj2StartHint.Text = $"PJ ID: {_multiLotPj2Id}";
        _lblMultiLotPj1CompletedHint.Text = $"PJ ID: {_multiLotPj1Id}";
        _lblMultiLotPj2CompletedHint.Text = $"PJ ID: {_multiLotPj2Id}";
    }

    private bool TryHandleWaferSlotProgress(string actionText, string source)
    {
        if (_activeReportScope == ReportScope.MultiLot && TryHandleMultiLotWaferSlotProgress(actionText, source))
        {
            return true;
        }

        var isStart = string.Equals(actionText, "13) Wafer Process Start Event", StringComparison.OrdinalIgnoreCase);
        var isEnd = string.Equals(actionText, "14) Wafer Process End Event", StringComparison.OrdinalIgnoreCase);
        if (!isStart && !isEnd)
        {
            return false;
        }

        if (isStart)
        {
            AppendResult($"[INFO] Wafer Process Start received; completion count remains {_waferEndCount}/{_expectedWaferSlotCount} ({source})");
            TrackExpectedReport("14) Wafer Process End Event");
            return true;
        }

        if (isEnd && _waferEndCount < _expectedWaferSlotCount)
        {
            _waferEndCount++;
            UpdateWaferSlotProgressLabels();
            AppendResult($"[INFO] Wafer completed: {_waferEndCount}/{_expectedWaferSlotCount} ({source})");
        }

        if (isEnd && _waferEndCount < _expectedWaferSlotCount)
        {
            TrackExpectedReport("13) Wafer Process Start Event");
            return true;
        }

        if (isEnd)
        {
            if (TryGetReportLamp(ReportScope.Normal, "13) Wafer Process Start Event", out var startLamp) &&
                startLamp.BackColor != Color.FromArgb(78, 180, 95))
            {
                startLamp.BackColor = Color.FromArgb(78, 180, 95);
                AppendResult("[INFO] Auto PASS: 13) Wafer Process Start Event (all wafers completed)");
            }

            if (TryGetReportLamp(ReportScope.Normal, "14) Wafer Process End Event", out var endLamp) &&
                endLamp.BackColor != Color.FromArgb(78, 180, 95))
            {
                endLamp.BackColor = Color.FromArgb(78, 180, 95);
                AppendResult("[INFO] Auto PASS: 14) Wafer Process End Event (all wafers completed)");
            }

            TrackExpectedReport("15) Process Job Completed Event");
            return true;
        }

        return false;
    }

    private bool TryHandleMultiLotWaferSlotProgress(string actionText, string source)
    {
        var isStart = string.Equals(actionText, "11) Wafer Process Start Event", StringComparison.OrdinalIgnoreCase);
        var isEnd = string.Equals(actionText, "12) Wafer Process End Event", StringComparison.OrdinalIgnoreCase);
        if (!isStart && !isEnd)
        {
            return false;
        }

        if (isStart)
        {
            AppendResult($"[INFO] Multi Lot Wafer Process Start received; completion count remains {_multiLotWaferEndCount}/{_multiLotExpectedWaferSlotCount} ({source})");
            TrackExpectedReport("12) Wafer Process End Event");
            return true;
        }

        if (_multiLotWaferEndCount < _multiLotExpectedWaferSlotCount)
        {
            _multiLotWaferEndCount++;
            UpdateMultiLotWaferSlotProgressLabels();
            AppendResult($"[INFO] Multi Lot wafer completed: {_multiLotWaferEndCount}/{_multiLotExpectedWaferSlotCount} ({source})");
        }

        if (_multiLotWaferEndCount < _multiLotExpectedWaferSlotCount)
        {
            TrackExpectedReport("11) Wafer Process Start Event");
            return true;
        }

        if (TryGetReportLamp(ReportScope.MultiLot, "11) Wafer Process Start Event", out var startLamp) &&
            startLamp.BackColor != Color.FromArgb(78, 180, 95))
        {
            startLamp.BackColor = Color.FromArgb(78, 180, 95);
            AppendResult("[INFO] Auto PASS: 11) Wafer Process Start Event (all Multi Lot wafers completed)");
        }

        if (TryGetReportLamp(ReportScope.MultiLot, "12) Wafer Process End Event", out var endLamp) &&
            endLamp.BackColor != Color.FromArgb(78, 180, 95))
        {
            endLamp.BackColor = Color.FromArgb(78, 180, 95);
            AppendResult("[INFO] Auto PASS: 12) Wafer Process End Event (all Multi Lot wafers completed)");
        }

        TrackExpectedReport("13) Process Job PJ1 Completed Event");
        return true;
    }

    private static bool IsCarrierRemovalAction(string actionText)
    {
        return string.Equals(actionText, "25) Carrier Removed Event", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionText, "26) Ready to Load Event", StringComparison.OrdinalIgnoreCase);
    }

    private void MarkReportPass(string actionText, string source)
    {
        if (IsCarrierRemovalAction(actionText) && !_carrierRemovalEventsEnabled)
        {
            AppendResult($"[INFO] Ignored {actionText}: waiting for carrier removal step.");
            return;
        }

        var completedPending = string.Equals(PendingReportAction, actionText, StringComparison.OrdinalIgnoreCase);
        if (completedPending)
        {
            PendingReportAction = null;
        }

        if (TryHandleWaferSlotProgress(actionText, source))
        {
            return;
        }

        if (string.Equals(actionText, "4) Auto Read Carrier ID", StringComparison.OrdinalIgnoreCase))
        {
            _ = TryAutoSendProceedWithCarrierAsync();
        }

        if (_activeReportScope == ReportScope.Normal &&
            string.Equals(actionText, "12) Process Job Start Event", StringComparison.OrdinalIgnoreCase))
        {
            _ = TryAutoKickMeasurementAfterProcessStartAsync();
        }

        if (!TryGetReportLamp(actionText, out var lamp))
        {
            return;
        }

        if (lamp.BackColor == Color.FromArgb(78, 180, 95))
        {
            if (completedPending && NextReportByAction.TryGetValue(actionText, out var alreadyLitNextAction))
            {
                TrackExpectedReport(alreadyLitNextAction);
            }

            return;
        }

        lamp.BackColor = Color.FromArgb(78, 180, 95);
        AppendResult($"[INFO] Auto PASS: {actionText} ({source})");

        if (string.Equals(actionText, "8) Slot Mapping Data", StringComparison.OrdinalIgnoreCase) &&
            TryGetReportLamp(ReportScope.Normal, "7) Auto Open Door Event", out var autoOpenLamp) &&
            autoOpenLamp.BackColor != Color.FromArgb(78, 180, 95))
        {
            autoOpenLamp.BackColor = Color.FromArgb(78, 180, 95);
            AppendResult("[INFO] Auto PASS: 7) Auto Open Door Event (inferred before Slot Mapping Data)");
        }

        if (NextReportByAction.TryGetValue(actionText, out var nextAction))
        {
            TrackExpectedReport(nextAction);
        }

        if (string.Equals(actionText, "26) Ready to Load Event", StringComparison.OrdinalIgnoreCase))
        {
            _carrierRemovalEventsEnabled = false;
        }
    }

    private async Task TryAutoSendProceedWithCarrierAsync()
    {
        if (_loadingProceedSent)
        {
            return;
        }

        var carrierId = _txtCarrierId.Text.Trim();
        if (string.IsNullOrWhiteSpace(carrierId))
        {
            AppendResult("[WARN] Auto send S3F17 skipped: Carrier ID is empty. Please enter Carrier ID manually and click 'Bypass'.");
            _txtLoadingExecutionResult.Text = "Carrier ID empty — manual Bypass required";
            return;
        }

        _loadingProceedSent = true;

        try
        {
            await SendAsync("L1Normal_S3F17_ProceedWithCarrier", 3, 17,
                Secs.SecsMessageFactory.S3F17ProceedWithCarrier(carrierId, (byte)_nudPortId.Value)).ConfigureAwait(true);

            _txtLoadingExecutionResult.Text = "Auto sent S3F17 completed";
            MarkReportPass("5) Proceed With Carrier", "auto-send S3F17");
        }
        catch (Exception ex)
        {
            _loadingProceedSent = false;
            _txtLoadingExecutionResult.Text = "Auto sent S3F17 failed";
            AppendResult($"[WARN] Auto send S3F17 failed: {ex.Message}");
        }
    }

    private async Task TryAutoKickMeasurementAfterProcessStartAsync()
    {
        if (_normalMeasurementKickSent)
        {
            return;
        }

        var processJobId = _currentNormalProcessJobId.Trim();
        if (string.IsNullOrWhiteSpace(processJobId))
        {
            return;
        }

        // Some tools stop at CEID=96 without entering wafer events unless PR START is issued once more.
        await Task.Delay(1500).ConfigureAwait(true);

        if (IsDisposed || Disposing)
        {
            return;
        }

        if (!IsReportActionArmed("13) Wafer Process Start Event"))
        {
            return;
        }

        if (TryGetReportLamp(ReportScope.Normal, "13) Wafer Process Start Event", out var startLamp) &&
            startLamp.BackColor == Color.FromArgb(78, 180, 95))
        {
            return;
        }

        _normalMeasurementKickSent = true;
        AppendResult($"[INFO] Measurement watchdog: no wafer-start event after CEID=96; send S16F5 START for PJ='{processJobId}'.");

        var ok = await SendAllowNakAsync(
            "L1Normal_S16F5_ProcessJobStart_AfterCEID96",
            16,
            5,
            Secs.SecsMessageFactory.S16F5ProcessJobCommand(processJobId, "START")).ConfigureAwait(true);

        if (!ok)
        {
            AppendResult("[WARN] Measurement watchdog S16F5 START was rejected (NAK/ERROR). Keep waiting for S6F11 wafer events.");
            return;
        }

        AppendResult("[INFO] Measurement watchdog S16F5 START accepted.");
    }

    private static bool TryExtractCeid(string raw, out uint ceid)
    {
        ceid = 0;

        // Prefer explicit CEID-tagged fields when present in decoded text.
        var ceidTagged = Regex.Match(
            raw,
            @"<U(?:1|2|4|8)\s*\[\d+\]\s*(\d+)\s*>\s*/\*\s*CEID\s*\*/",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (ceidTagged.Success && uint.TryParse(ceidTagged.Groups[1].Value, out ceid))
        {
            return true;
        }

        var ceidTaggedBefore = Regex.Match(
            raw,
            @"/\*\s*CEID\s*\*/\s*<U(?:1|2|4|8)\s*\[\d+\]\s*(\d+)\s*>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (ceidTaggedBefore.Success && uint.TryParse(ceidTaggedBefore.Groups[1].Value, out ceid))
        {
            return true;
        }

        // Fallback to the classic position: DATAID then CEID.
        var byPosition = Regex.Match(
            raw,
            @"<U(?:1|2|4|8)\s*\[\d+\]\s*\d+\s*>\s*<U(?:1|2|4|8)\s*\[\d+\]\s*(\d+)\s*>",
            RegexOptions.Singleline);
        if (byPosition.Success && uint.TryParse(byPosition.Groups[1].Value, out ceid))
        {
            return true;
        }

        // Last fallback for interpreted strings that contain "CEID=xxxx".
        var byText = Regex.Match(raw, @"CEID\s*=\s*(\d+)", RegexOptions.IgnoreCase);
        return byText.Success && uint.TryParse(byText.Groups[1].Value, out ceid);
    }

    private static string Shorten(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
        {
            return text;
        }

        return text[..maxLen] + "...";
    }

    private void TryApplyCarrierIdFromEvent(string actionText, Secs4Net.SecsMessage primaryMessage, string combinedText)
    {
        if (!actionText.Contains("Carrier ID", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!TryExtractCarrierId(primaryMessage, combinedText, out var carrierId))
        {
            return;
        }

        _txtCarrierId.Text = carrierId;
        AppendResult($"[INFO] Auto read Carrier ID = {carrierId}");
    }

    private static bool TryExtractCarrierId(Secs4Net.SecsMessage message, string text, out string carrierId)
    {
        carrierId = string.Empty;

        // Prefer object-level extraction from S6F11 payload shape:
        // L[3](DATAID, CEID, L[reports]( L[2](RPTID, L[values]) ))
        // CarrierID is typically values[1] in CarrierIDRead report.
        if (TryExtractCarrierIdFromS6F11(message, out carrierId))
        {
            return true;
        }

        var candidates = new List<string>();

        // 1) Collect ALL ASCII leaf values from payload (depth-first).
        CollectAsciiCandidates(message.SecsItem, candidates);

        // 2) Fallback to explicit labels: CarrierID=..., Carrier ID: ...
        var labelMatch = Regex.Match(text,
            @"CARRIER\s*[_\- ]?ID\s*[:=]\s*([A-Za-z0-9._\-]{3,64})",
            RegexOptions.IgnoreCase);
        if (labelMatch.Success)
        {
            candidates.Add(labelMatch.Groups[1].Value.Trim());
        }

        // 3) Fallback to quoted strings in raw/interpreted text.
        var quoted = Regex.Matches(text, @"'([^']+)'", RegexOptions.Singleline)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v));
        candidates.AddRange(quoted);

        if (candidates.Count == 0)
        {
            return false;
        }

        // Apply filters and collect all valid candidates, then pick the best match.
        // "Best" = longest string that passes all rules (Carrier IDs tend to be
        // longer and more unique than generic attribute name strings like "ID").
        var validCandidates = new List<string>();
        foreach (var candidate in candidates)
        {
            var value = candidate.Trim();
            if (!IsLikelyCarrierId(value))
            {
                continue;
            }

            validCandidates.Add(value);
        }

        if (validCandidates.Count == 0)
        {
            return false;
        }

        // Prefer the longest valid candidate (Carrier IDs tend to be longer than
        // generic attribute labels that also pass the regex).
        carrierId = validCandidates.OrderByDescending(v => v.Length).First();
        return true;
    }

    private static bool TryExtractCarrierIdForCarrierRead(Secs4Net.SecsMessage message, string raw, string text, out string carrierId)
    {
        carrierId = string.Empty;

        // 1) Best effort: parse scalar values with aliases and pick fields explicitly marked
        // as CarrierID/FOUPID to avoid CLOCK/LocationID contamination.
        var aliasMatches = Regex.Matches(
            raw,
            @"<([A-Za-z0-9]+)\s*\[\d+\]\s*([^>]+)>\s*/\*\s*([^*]+?)\s*\*/",
            RegexOptions.Singleline);

        foreach (Match m in aliasMatches.Cast<Match>())
        {
            var rawValue = m.Groups[2].Value.Trim();
            var alias = m.Groups[3].Value.Trim();
            var aliasUpper = alias.ToUpperInvariant();

            if (!aliasUpper.Contains("CARRIERID", StringComparison.Ordinal) &&
                !aliasUpper.Contains("FOUPID", StringComparison.Ordinal) &&
                !aliasUpper.Contains("FOUP ID", StringComparison.Ordinal))
            {
                continue;
            }

            var cleaned = rawValue.Trim('\'', '"', ' ');
            if (!IsLikelyCarrierId(cleaned))
            {
                continue;
            }

            carrierId = cleaned;
            return true;
        }

        // 2) Decode binary payload candidates (e.g. <B [n] 0x43 0x41 ...>) and try as Carrier ID.
        foreach (var binaryCandidate in ExtractAsciiCandidatesFromBinaryRaw(raw))
        {
            if (!IsLikelyCarrierId(binaryCandidate))
            {
                continue;
            }

            carrierId = binaryCandidate;
            return true;
        }

        // 3) Fallback to object/heuristic extraction for CEID=1000 payload variants.
        return TryExtractCarrierId(message, text, out carrierId);
    }

    private static bool TryExtractCarrierIdFromS6F11(Secs4Net.SecsMessage message, out string carrierId)
    {
        carrierId = string.Empty;

        if (message.S != 6 || message.F != 11)
        {
            return false;
        }

        var root = message.SecsItem;
        if (root is null || root.Format != Secs4Net.SecsFormat.List || root.Count < 3)
        {
            return false;
        }

        var reports = root[2];
        if (reports.Format != Secs4Net.SecsFormat.List || reports.Count <= 0)
        {
            return false;
        }

        for (int i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            if (report.Format != Secs4Net.SecsFormat.List || report.Count < 2)
            {
                continue;
            }

            var values = report[1];
            if (values.Format != Secs4Net.SecsFormat.List || values.Count <= 0)
            {
                continue;
            }

            // Prefer legacy position (values[1]) first when available.
            if (values.Count > 1 && TryReadStringValue(values[1], out var preferred) && IsLikelyCarrierId(preferred))
            {
                carrierId = preferred;
                return true;
            }

            // Fallback: scan all scalar values in this report and pick first valid candidate.
            for (int v = 0; v < values.Count; v++)
            {
                if (!TryReadStringValue(values[v], out var candidate))
                {
                    continue;
                }

                if (!IsLikelyCarrierId(candidate))
                {
                    continue;
                }

                carrierId = candidate;
                return true;
            }

            // Deep fallback: recursively walk nested value lists.
            if (TryExtractCarrierIdFromItemTree(values, out var nestedCarrierId))
            {
                carrierId = nestedCarrierId;
                return true;
            }
        }

        return false;
    }

    private static bool TryReadStringValue(Secs4Net.Item item, out string value)
    {
        value = string.Empty;

        if (item is null || item.Count == 0)
        {
            return false;
        }

        if (item.Format == Secs4Net.SecsFormat.List)
        {
            return false;
        }

        try
        {
            var s = item.GetString().Trim();
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            value = s;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLikelyCarrierId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Filter out clock/timestamp style strings like 2026042415413046.
        if (Regex.IsMatch(value, @"^\d{12,}$"))
        {
            return false;
        }

        if (Regex.IsMatch(value, @"^S\d+F\d+$", RegexOptions.IgnoreCase))
        {
            return false;
        }

        if (Regex.IsMatch(value, @"^LOADPORT\d*$", RegexOptions.IgnoreCase))
        {
            return false;
        }

        if (Regex.IsMatch(value, @"^(PJ|CJ)[A-Za-z0-9._-]{1,62}$", RegexOptions.IgnoreCase))
        {
            return false;
        }

        if (value.Equals("ProceedWithCarrier", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("ContentMap", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("SlotMap", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Usage", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Capacity", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("CarrierID", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("PortID", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("SubstrateCount", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Regex.IsMatch(value, @"^[A-Za-z0-9._-]{3,64}$");
    }

    private static bool IsJobLifecycleCeid(uint ceid)
    {
        return ceid is 41 or 42 or 44 or 46 or 47 or 91 or 93 or 96 or 97 or 100 or 103;
    }

    private static void CollectAsciiCandidates(Secs4Net.Item? item, List<string> output)
    {
        if (item is null)
        {
            return;
        }

        if (item.Format == Secs4Net.SecsFormat.ASCII)
        {
            var s = item.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(s))
            {
                output.Add(s);
            }
            return;
        }

        if (item.Format != Secs4Net.SecsFormat.List || item.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < item.Count; i++)
        {
            CollectAsciiCandidates(item[i], output);
        }
    }

    private bool TryExtractReportByKeyword(string raw, out string actionText)
    {
        actionText = string.Empty;
        var upper = raw.ToUpperInvariant();

        if (upper.Contains("LOAD COMPLETE", StringComparison.Ordinal) ||
            upper.Contains("CARRIER PLACEMENT", StringComparison.Ordinal))
        {
            actionText = "2) Carrier Placement (Load Complete)";
            return true;
        }

        if (upper.Contains("CARRIERDOCKED", StringComparison.Ordinal) ||
            upper.Contains("AUTO DOCK", StringComparison.Ordinal) ||
            upper.Contains("DOCKING", StringComparison.Ordinal))
        {
            actionText = "6) Auto Docking Event";
            return true;
        }

        if (upper.Contains("CARRIEROPENED", StringComparison.Ordinal) ||
            upper.Contains("AUTO OPEN DOOR", StringComparison.Ordinal) ||
            upper.Contains("DOOR OPEN", StringComparison.Ordinal))
        {
            actionText = "7) Auto Open Door Event";
            return true;
        }

        if (upper.Contains("SLOTMAPREPORT", StringComparison.Ordinal) ||
            upper.Contains("SLOT MAPPING", StringComparison.Ordinal) ||
            upper.Contains("SLOT MAP", StringComparison.Ordinal) ||
            upper.Contains("CONTENTMAP", StringComparison.Ordinal))
        {
            actionText = ResolveScopedAction("8) Slot Mapping Data");
            return true;
        }

        if ((upper.Contains("CJSTATUSCHANGE", StringComparison.Ordinal) &&
             (upper.Contains("AUTO_START", StringComparison.Ordinal) ||
              upper.Contains("START", StringComparison.Ordinal) ||
              upper.Contains("EXECUT", StringComparison.Ordinal))) ||
            upper.Contains("CJ_SELECTEDTOEXECUTING", StringComparison.Ordinal) ||
            upper.Contains("CJ_WAITTINGFORSTARTTOEXECUTING", StringComparison.Ordinal))
        {
            actionText = ResolveScopedAction("11) Control Job Start Event");
            return true;
        }

        if (upper.Contains("PJSTATUSCHANGE", StringComparison.Ordinal) ||
            upper.Contains("PJ_WAITTINGFORSTARTTOPROCESSING", StringComparison.Ordinal) ||
            upper.Contains("PJ_SETTINGUPTOPROCESSING", StringComparison.Ordinal) ||
            upper.Contains("PJ_JOBPROCESSING", StringComparison.Ordinal))
        {
            actionText = ResolveScopedAction("12) Process Job Start Event");
            return true;
        }

        if (upper.Contains("WAFER PROCESS START", StringComparison.Ordinal) ||
            upper.Contains("WAFERSTART", StringComparison.Ordinal) ||
            upper.Contains("E90 WAFER START", StringComparison.Ordinal))
        {
            actionText = ResolveScopedAction("13) Wafer Process Start Event");
            return true;
        }

        if (upper.Contains("WAFER PROCESS END", StringComparison.Ordinal) ||
            upper.Contains("WAFEREND", StringComparison.Ordinal) ||
            upper.Contains("E90 WAFER END", StringComparison.Ordinal))
        {
            actionText = ResolveScopedAction("14) Wafer Process End Event");
            return true;
        }

        if (upper.Contains("PJ_PROCESSINGTOPROCESSCOMPLETE", StringComparison.Ordinal) ||
            upper.Contains("PJ_JOBPROCESSINGCOMPLETE", StringComparison.Ordinal) ||
            upper.Contains("PROCESS JOB COMPLETED", StringComparison.Ordinal) ||
            upper.Contains("PROCESSJOB COMPLETED", StringComparison.Ordinal))
        {
            actionText = ResolveScopedAction("15) Process Job Completed Event");
            return true;
        }

        if (upper.Contains("CJ_EXECUTINGTOCOMPLETED", StringComparison.Ordinal) ||
            upper.Contains("CONTROL JOB COMPLETED", StringComparison.Ordinal) ||
            upper.Contains("CONTROLJOB COMPLETED", StringComparison.Ordinal))
        {
            actionText = ResolveScopedAction("16) Control Job Completed Event");
            return true;
        }

        return false;
    }

    private static string BuildS6F11ValueDebugSummary(string raw, Secs4Net.SecsMessage message)
    {
        try
        {
            var reportMatches = Regex.Matches(
                raw,
                @"<L\s*\[2\]\s*<(?:U|I)(?:1|2|4|8)\s*\[\d+\]\s*(\d+)\s*>\s*<L\s*\[(\d+)\]\s*(.*?)\s*>\s*>",
                RegexOptions.Singleline);

            if (reportMatches.Count == 0)
            {
                return BuildS6F11ValueDebugSummaryFromItemTree(message);
            }

            var chunks = new List<string>();
            foreach (Match report in reportMatches)
            {
                var rptId = report.Groups[1].Value;
                var body = report.Groups[3].Value;

                var values = Regex.Matches(
                        body,
                        @"<([A-Za-z0-9]+)\s*\[\d+\]\s*([^>]+)>\s*(?:/\*\s*([^*]+?)\s*\*/)?",
                        RegexOptions.Singleline)
                    .Cast<Match>()
                    .Select((m, idx) =>
                    {
                        var format = m.Groups[1].Value;
                        var rawValue = m.Groups[2].Value.Trim();
                        var alias = m.Groups[3].Success ? m.Groups[3].Value.Trim() : string.Empty;
                        var name = string.IsNullOrWhiteSpace(alias) ? $"v{idx}" : alias;
                        return $"{name}:{format}={rawValue}";
                    })
                    .Take(8)
                    .ToArray();

                if (values.Length == 0)
                {
                    chunks.Add($"RPTID={rptId}: no scalar values");
                }
                else
                {
                    chunks.Add($"RPTID={rptId}: {string.Join(", ", values)}");
                }
            }

            return string.Join(" | ", chunks);
        }
        catch (Exception ex)
        {
            return $"debug summary failed: {ex.Message}";
        }
    }

    private static string BuildS6F11ValueDebugSummaryFromItemTree(Secs4Net.SecsMessage message)
    {
        try
        {
            var root = message.SecsItem;
            if (root is null || root.Format != Secs4Net.SecsFormat.List || root.Count < 3)
            {
                return "no report-list parsed from item tree";
            }

            var reports = root[2];
            if (reports.Format != Secs4Net.SecsFormat.List || reports.Count == 0)
            {
                return "no reports in item tree";
            }

            var chunks = new List<string>();
            for (int i = 0; i < reports.Count; i++)
            {
                var report = reports[i];
                if (report.Format != Secs4Net.SecsFormat.List || report.Count < 2)
                {
                    chunks.Add($"report[{i}]: invalid shape");
                    continue;
                }

                var rptid = TryReadUIntValue(report[0], out var rptidVal) ? rptidVal.ToString() : "?";
                var values = report[1];
                if (values.Format != Secs4Net.SecsFormat.List)
                {
                    chunks.Add($"RPTID={rptid}: values is not list");
                    continue;
                }

                var valueTokens = new List<string>();
                for (int v = 0; v < values.Count && v < 8; v++)
                {
                    valueTokens.Add($"v{v}:{FormatItemValue(values[v])}");
                }

                chunks.Add(valueTokens.Count == 0
                    ? $"RPTID={rptid}: no values"
                    : $"RPTID={rptid}: {string.Join(", ", valueTokens)}");
            }

            return string.Join(" | ", chunks);
        }
        catch (Exception ex)
        {
            return $"item tree debug failed: {ex.Message}";
        }
    }

    private static bool TryReadUIntValue(Secs4Net.Item item, out uint value)
    {
        value = 0;
        try
        {
            switch (item.Format)
            {
                case Secs4Net.SecsFormat.U1:
                    value = item.FirstValue<byte>();
                    return true;
                case Secs4Net.SecsFormat.U2:
                    value = item.FirstValue<ushort>();
                    return true;
                case Secs4Net.SecsFormat.U4:
                    value = item.FirstValue<uint>();
                    return true;
                case Secs4Net.SecsFormat.U8:
                    var u8 = item.FirstValue<ulong>();
                    value = u8 > uint.MaxValue ? uint.MaxValue : (uint)u8;
                    return true;
                case Secs4Net.SecsFormat.I1:
                    value = (uint)item.FirstValue<sbyte>();
                    return true;
                case Secs4Net.SecsFormat.I2:
                    value = (uint)item.FirstValue<short>();
                    return true;
                case Secs4Net.SecsFormat.I4:
                    value = (uint)item.FirstValue<int>();
                    return true;
                case Secs4Net.SecsFormat.I8:
                    value = (uint)item.FirstValue<long>();
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static string FormatItemValue(Secs4Net.Item item)
    {
        try
        {
            return item.Format switch
            {
                Secs4Net.SecsFormat.ASCII => $"A='{item.GetString().Trim()}'",
                Secs4Net.SecsFormat.U1 => $"U1={item.FirstValue<byte>()}",
                Secs4Net.SecsFormat.U2 => $"U2={item.FirstValue<ushort>()}",
                Secs4Net.SecsFormat.U4 => $"U4={item.FirstValue<uint>()}",
                Secs4Net.SecsFormat.U8 => $"U8={item.FirstValue<ulong>()}",
                Secs4Net.SecsFormat.I1 => $"I1={item.FirstValue<sbyte>()}",
                Secs4Net.SecsFormat.I2 => $"I2={item.FirstValue<short>()}",
                Secs4Net.SecsFormat.I4 => $"I4={item.FirstValue<int>()}",
                Secs4Net.SecsFormat.I8 => $"I8={item.FirstValue<long>()}",
                Secs4Net.SecsFormat.Binary => $"B[{item.Count}]={FormatBinaryPreview(item)}",
                Secs4Net.SecsFormat.List => $"L[{item.Count}]",
                _ => item.ToString()
            };
        }
        catch
        {
            return $"{item.Format}=<read-failed>";
        }
    }

    private static string FormatBinaryPreview(Secs4Net.Item item)
    {
        try
        {
            var bytes = item.GetMemory<byte>().ToArray();
            var preview = bytes.Take(16).Select(b => $"0x{b:X2}").ToArray();
            return bytes.Length > 16
                ? string.Join(" ", preview) + " ..."
                : string.Join(" ", preview);
        }
        catch
        {
            return "<binary-read-failed>";
        }
    }

    private static string BuildCarrierExtractDebug(Secs4Net.SecsMessage message, string raw, string text)
    {
        try
        {
            var aliasCandidates = Regex.Matches(
                    raw,
                    @"<([A-Za-z0-9]+)\s*\[\d+\]\s*([^>]+)>\s*/\*\s*([^*]+?)\s*\*/",
                    RegexOptions.Singleline)
                .Cast<Match>()
                .Select(m =>
                {
                    var alias = m.Groups[3].Value.Trim();
                    var value = m.Groups[2].Value.Trim().Trim('\'', '"', ' ');
                    return $"{alias}={value}";
                })
                .Take(10)
                .ToArray();

            var asciiCandidates = new List<string>();
            CollectAsciiCandidates(message.SecsItem, asciiCandidates);

            var validCandidates = asciiCandidates
                .Select(v => v.Trim())
                .Where(IsLikelyCarrierId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();

            var binaryAsciiCandidates = ExtractAsciiCandidatesFromBinaryRaw(raw)
                .Where(IsLikelyCarrierId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();

            var itemTreeCandidates = CollectCarrierIdCandidatesFromItemTree(message.SecsItem)
                .Where(IsLikelyCarrierId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();

            var labelMatch = Regex.Match(
                text,
                @"CARRIER\s*[_\- ]?ID\s*[:=]\s*([A-Za-z0-9._\-]{3,64})",
                RegexOptions.IgnoreCase);

            var parts = new List<string>
            {
                aliasCandidates.Length > 0
                    ? $"alias=[{string.Join(", ", aliasCandidates)}]"
                    : "alias=[]",
                validCandidates.Length > 0
                    ? $"validAscii=[{string.Join(", ", validCandidates)}]"
                    : "validAscii=[]",
                binaryAsciiCandidates.Length > 0
                    ? $"binaryAscii=[{string.Join(", ", binaryAsciiCandidates)}]"
                    : "binaryAscii=[]",
                itemTreeCandidates.Length > 0
                    ? $"itemTree=[{string.Join(", ", itemTreeCandidates)}]"
                    : "itemTree=[]",
                labelMatch.Success
                    ? $"labelCarrierId={labelMatch.Groups[1].Value.Trim()}"
                    : "labelCarrierId=<none>"
            };

            return string.Join(" | ", parts);
        }
        catch (Exception ex)
        {
            return $"carrier debug build failed: {ex.Message}";
        }
    }

    private static IEnumerable<string> ExtractAsciiCandidatesFromBinaryRaw(string raw)
    {
        var matches = Regex.Matches(raw, @"<B\s*\[\d+\]\s*([^>]+)>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match match in matches.Cast<Match>())
        {
            var byteTokens = Regex.Matches(match.Groups[1].Value, @"0x[0-9A-Fa-f]{1,2}|\d+")
                .Cast<Match>()
                .Select(m => m.Value)
                .ToArray();

            if (byteTokens.Length == 0)
            {
                continue;
            }

            var bytes = new List<byte>(byteTokens.Length);
            foreach (var token in byteTokens)
            {
                if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (byte.TryParse(token[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex))
                    {
                        bytes.Add(hex);
                    }
                    continue;
                }

                if (byte.TryParse(token, out var dec))
                {
                    bytes.Add(dec);
                }
            }

            if (bytes.Count == 0)
            {
                continue;
            }

            foreach (var run in ExtractPrintableAsciiRuns(bytes))
            {
                yield return run;
            }
        }
    }

    private static bool ContainsSlotMapPayload(Secs4Net.SecsMessage message)
    {
        var root = message.SecsItem;
        if (root is null || root.Format != Secs4Net.SecsFormat.List || root.Count < 3)
        {
            return false;
        }

        var reports = root[2];
        if (reports.Format != Secs4Net.SecsFormat.List || reports.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            if (report.Format != Secs4Net.SecsFormat.List || report.Count < 2)
            {
                continue;
            }

            var values = report[1];
            if (values.Format != Secs4Net.SecsFormat.List || values.Count == 0)
            {
                continue;
            }

            for (int v = 0; v < values.Count; v++)
            {
                if (values[v].Format != Secs4Net.SecsFormat.List)
                {
                    continue;
                }

                // SlotMap commonly appears as a long list of U1 entries.
                if (CountNumericLeaves(values[v]) >= 20)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountNumericLeaves(Secs4Net.Item item)
    {
        if (item.Format == Secs4Net.SecsFormat.List)
        {
            var total = 0;
            for (int i = 0; i < item.Count; i++)
            {
                total += CountNumericLeaves(item[i]);
            }

            return total;
        }

        return item.Format switch
        {
            Secs4Net.SecsFormat.U1 or
            Secs4Net.SecsFormat.U2 or
            Secs4Net.SecsFormat.U4 or
            Secs4Net.SecsFormat.U8 or
            Secs4Net.SecsFormat.I1 or
            Secs4Net.SecsFormat.I2 or
            Secs4Net.SecsFormat.I4 or
            Secs4Net.SecsFormat.I8 => item.Count,
            _ => 0
        };
    }

    private static bool TryExtractSlotMapData(Secs4Net.SecsMessage message, out string slotMapData)
    {
        slotMapData = string.Empty;

        var root = message.SecsItem;
        if (root is null || root.Format != Secs4Net.SecsFormat.List || root.Count < 3)
        {
            return false;
        }

        var reports = root[2];
        if (reports.Format != Secs4Net.SecsFormat.List || reports.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            if (report.Format != Secs4Net.SecsFormat.List || report.Count < 2)
            {
                continue;
            }

            var values = report[1];
            if (values.Format != Secs4Net.SecsFormat.List || values.Count == 0)
            {
                continue;
            }

            for (int v = 0; v < values.Count; v++)
            {
                var candidate = values[v];
                if (candidate.Format != Secs4Net.SecsFormat.List)
                {
                    continue;
                }

                var nums = new List<uint>();
                CollectNumericLeaves(candidate, nums);
                if (nums.Count < 20)
                {
                    continue;
                }

                slotMapData = "[" + string.Join(",", nums.Select(n => n.ToString())) + "]";
                return true;
            }
        }

        return false;
    }

    private static void CollectNumericLeaves(Secs4Net.Item item, List<uint> output)
    {
        if (item.Format == Secs4Net.SecsFormat.List)
        {
            for (int i = 0; i < item.Count; i++)
            {
                CollectNumericLeaves(item[i], output);
            }
            return;
        }

        if (TryReadUIntValue(item, out var val))
        {
            output.Add(val);
        }
    }

    private static IEnumerable<string> ExtractPrintableAsciiRuns(IEnumerable<byte> bytes)
    {
        var sb = new StringBuilder();

        foreach (var b in bytes)
        {
            if (b >= 32 && b <= 126)
            {
                sb.Append((char)b);
                continue;
            }

            if (sb.Length >= 3)
            {
                yield return sb.ToString().Trim();
            }

            sb.Clear();
        }

        if (sb.Length >= 3)
        {
            yield return sb.ToString().Trim();
        }
    }

    private static bool TryExtractCarrierIdFromItemTree(Secs4Net.Item? item, out string carrierId)
    {
        carrierId = string.Empty;
        if (item is null)
        {
            return false;
        }

        foreach (var candidate in CollectCarrierIdCandidatesFromItemTree(item))
        {
            if (!IsLikelyCarrierId(candidate))
            {
                continue;
            }

            carrierId = candidate;
            return true;
        }

        return false;
    }

    private static IEnumerable<string> CollectCarrierIdCandidatesFromItemTree(Secs4Net.Item? item)
    {
        if (item is null)
        {
            yield break;
        }

        if (item.Format == Secs4Net.SecsFormat.ASCII)
        {
            string text;
            try
            {
                text = item.GetString().Trim();
            }
            catch
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }
            yield break;
        }

        if (item.Format == Secs4Net.SecsFormat.Binary || item.Format == Secs4Net.SecsFormat.U1)
        {
            byte[] bytes;
            try
            {
                bytes = item.GetMemory<byte>().ToArray();
            }
            catch
            {
                bytes = [];
            }

            if (bytes.Length > 0)
            {
                foreach (var run in ExtractPrintableAsciiRuns(bytes))
                {
                    yield return run;
                }
            }
        }

        if (item.Format != Secs4Net.SecsFormat.List || item.Count <= 0)
        {
            yield break;
        }

        for (int i = 0; i < item.Count; i++)
        {
            foreach (var nested in CollectCarrierIdCandidatesFromItemTree(item[i]))
            {
                yield return nested;
            }
        }
    }
}
