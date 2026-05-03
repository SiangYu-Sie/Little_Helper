namespace HostSimTester.App.Pages;

public sealed class L2CancelStopAbortPage : BaseTestPage
{
    private enum CancelFlowPhase
    {
        Idle,
        WaitingCarrierRead,
        ProceededWithCarrier,
        WaitingSlotMap,
        CancelSent,
        ReadyToUnload,
        UnloadComplete,
        ReadyToLoad
    }

    private enum PendingFinalReport
    {
        None,
        ProcessJobAbort,
        ProcessJobStop,
        ControlJobAbort,
        ControlJobStop
    }

    // 共用狀態（可由對話框寫回）
    private string _carrierId = "CANCEL_001";
    private byte _portId = 1;
    private string _processJobId = "PJ001";
    private string _controlJobId = "CJ001";
    private string _ppid = "Trim_2";
    private string _lastCarrierReadLocationId = string.Empty;
    private bool _hasCarrierRead;
    private CancelFlowPhase _cancelFlowPhase = CancelFlowPhase.Idle;
    private PendingFinalReport _pendingFinalReport = PendingFinalReport.None;
    private string _activeLampScope = string.Empty;
    private readonly Dictionary<string, CancelFlowPhase> _cancelFlowPhaseByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingFinalReport> _pendingFinalReportByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _autoProceedOperationByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _proceedSentScopes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Panel>> _eventLamps = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _lastContentMapSlotIds = Array.Empty<string>();

    public L2CancelStopAbortPage(Secs.SecsConnection connection)
        : base("L2 Cancel/Stop/Abort", Logging.LoggerNames.L2CancelStopAbort, connection)
    {
        ConfigureActionPanel(640, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        Connection.PrimaryMessageReceived += OnPrimaryMessageReceived;
        Disposed += (_, _) => Connection.PrimaryMessageReceived -= OnPrimaryMessageReceived;

        var tabControl = CreateTabControl();

        // ── 不可點擊事件列（純顯示） ─────────────────────────────────────
        void AddPassiveRow(Control host, string scope, string text)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 30, Margin = new Padding(6, 2, 6, 2),
                Padding = new Padding(0, 1, 0, 0),
                BackColor = Theme.ThemeHelper.TableBg, WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16, Height = 16, Margin = new Padding(3, 5, 8, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            row.Controls.Add(lamp);
            row.Controls.Add(new Label
            {
                Text = text, AutoSize = true,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 0, 0)
            });
            host.Controls.Add(row);
            RegisterEventLamp(scope, text, lamp);
        }

        // ── 不可點擊事件列 + 旁邊一顆小按鈕 ──────────────────────────────
        void AddPassiveRowWithSideButton(Control host, string scope, string text, string btnText, Func<Task> btnAction)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 30, Margin = new Padding(6, 2, 6, 2),
                Padding = new Padding(0, 1, 0, 0),
                BackColor = Theme.ThemeHelper.TableBg, WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16, Height = 16, Margin = new Padding(3, 5, 8, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            row.Controls.Add(lamp);
            row.Controls.Add(new Label
            {
                Text = text, AutoSize = true,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 6, 0)
            });
            var btn = new Button
            {
                Text = btnText, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 24, Margin = new Padding(0, 2, 0, 0),
                Padding = new Padding(8, 0, 8, 0)
            };
            btn.Click += async (_, _) =>
            {
                ActivateLampScope(scope);
                try { await btnAction().ConfigureAwait(true); }
                catch (Exception ex) { AppendResult($"[ERROR] {ex.Message}"); }
            };
            row.Controls.Add(btn);
            host.Controls.Add(row);
            RegisterEventLamp(scope, text, lamp);
            Theme.ThemeHelper.ApplyButtonTheme(row);
        }

        void AddRegisteredAction(Control host, string scope, string text, Func<Task> action, int buttonWidth = 270)
        {
            AddActionTo(host, text, async () =>
            {
                ActivateLampScope(scope);
                await action().ConfigureAwait(true);
            }, buttonWidth);
            if (TryGetActionLamp(text, out var lamp))
            {
                RegisterEventLamp(scope, text, lamp);
            }
        }

        // ── 開啟 S3F17 Proceed 對話框 ────────────────────────────────────
        async Task ProceedSlotMapWithDialog(string sendName)
        {
            using var dlg = new Dialogs.S3F17ProceedDialog(_carrierId, _portId);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            _carrierId = dlg.CarrierId;
            _portId = dlg.PortId;
            _lastContentMapSlotIds = NormalizeSlotIds(dlg.OccupiedSlotIds);
            await SendAsync(sendName, 3, 17,
                BuildProceedWithCarrierPayload(dlg)).ConfigureAwait(true);
            MarkEventLampPass("4) Proceed SlotMap", $"S3F17 ProceedWithSlotMap Port={_portId}, CarrierID={_carrierId}");
        }

        // ── 開啟 PpSelect/PJ/CJ 設定對話框 ──────────────────────────────
        async Task CreatePjCjWithDialog(string pjSendName, string cjSendName)
        {
            using var prepDlg = new Dialogs.PpSelectStartPjCjDialog(
                _carrierId, _processJobId, _ppid, _controlJobId, _lastContentMapSlotIds);
            if (prepDlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            _carrierId = prepDlg.CarrierId;
            if (!string.IsNullOrWhiteSpace(prepDlg.ProcessJobId)) _processJobId = prepDlg.ProcessJobId;
            if (!string.IsNullOrWhiteSpace(prepDlg.Ppid)) _ppid = prepDlg.Ppid;
            if (!string.IsNullOrWhiteSpace(prepDlg.ControlJobId)) _controlJobId = prepDlg.ControlJobId;

            var slotIds = prepDlg.GetSlotIds();
            await SendAsync(pjSendName, 16, 15,
                Secs.SecsMessageFactory.S16F15ProcessJobCreate(_processJobId, _ppid, _carrierId, slotIds)).ConfigureAwait(true);

            var processOrderMgmt = byte.TryParse(prepDlg.ProcessOrderMgmt, out var pom) ? pom : (byte)2;
            await SendAsync(cjSendName, 14, 9,
                Secs.SecsMessageFactory.S14F9ControlJobCreate(_controlJobId, _carrierId, [_processJobId], processOrderMgmt, slotIds)).ConfigureAwait(true);
            MarkEventLampPass("5) Create ProcessJob & ControlJob", $"S16F15/S14F9 PJ={_processJobId}, CJ={_controlJobId}");
        }

        async Task CancelCarrierWithDialog(string sendName)
        {
            using var dlg = new Dialogs.CancelCarrierDialog(_carrierId, _portId);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            _carrierId = dlg.CarrierId;
            _portId = dlg.PortId;
            if (dlg.IsCancelCarrierAtPort)
            {
                await SendAsync($"{sendName}_AtPort", 3, 17,
                    Secs.SecsMessageFactory.S3F17CancelCarrierAtPort(dlg.PortId)).ConfigureAwait(true);
                ActiveCancelFlowPhase = CancelFlowPhase.CancelSent;
                MarkEventLampPass(sendName.Contains("_Slot_", StringComparison.OrdinalIgnoreCase) ? "4) Cancel Carrier" : "2) Cancel Carrier",
                    $"S3F17 CancelCarrierAtPort Port={_portId}");
                return;
            }

            await SendAsync(sendName, 3, 17,
                Secs.SecsMessageFactory.S3F17CancelCarrier(dlg.CarrierId, dlg.PortId)).ConfigureAwait(true);
            ActiveCancelFlowPhase = CancelFlowPhase.CancelSent;
            MarkEventLampPass(sendName.Contains("_Slot_", StringComparison.OrdinalIgnoreCase) ? "4) Cancel Carrier" : "2) Cancel Carrier",
                $"S3F17 CancelCarrier Port={_portId}, CarrierID={_carrierId}");
        }

        async Task SendProcessJobCommandWithDialog(string sendName, string command)
        {
            using var dlg = new Dialogs.ProcessJobCommandDialog(_processJobId, command);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            _processJobId = dlg.ProcessJobId;
            await SendAsync(sendName, 16, 5,
                Secs.SecsMessageFactory.S16F5ProcessJobCommand(dlg.ProcessJobId, dlg.Command)).ConfigureAwait(true);
            if (dlg.Command.Equals("ABORT", StringComparison.OrdinalIgnoreCase))
            {
                ActivePendingFinalReport = PendingFinalReport.ProcessJobAbort;
                MarkEventLampPass("8) S16F5 PJ Command ABORT", $"S16F5 ABORT PJ={_processJobId}");
            }
            else if (dlg.Command.Equals("STOP", StringComparison.OrdinalIgnoreCase))
            {
                ActivePendingFinalReport = PendingFinalReport.ProcessJobStop;
                MarkEventLampPass("8) S16F5 PJ Command STOP", $"S16F5 STOP PJ={_processJobId}");
            }
        }

        async Task SendControlJobCommandWithDialog(string sendName, byte command)
        {
            using var dlg = new Dialogs.ControlJobCommandDialog(_controlJobId, command, 1);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            _controlJobId = dlg.ControlJobId;
            await SendAsync(sendName, 16, 27,
                Secs.SecsMessageFactory.S16F27ControlJobCommand(dlg.ControlJobId, dlg.ControlJobCommand, dlg.ActionCode)).ConfigureAwait(true);
            if (dlg.ControlJobCommand == 7)
            {
                ActivePendingFinalReport = PendingFinalReport.ControlJobAbort;
                MarkEventLampPass("8) S16F27 CJ Command ABORT", $"S16F27 ABORT CJ={_controlJobId}");
            }
            else if (dlg.ControlJobCommand == 6)
            {
                ActivePendingFinalReport = PendingFinalReport.ControlJobStop;
                MarkEventLampPass("8) S16F27 CJ Command STOP", $"S16F27 STOP CJ={_controlJobId}");
            }
        }

        // ── 完整流程（PJ/CJ Stop/Abort 共用）─────────────────────────────
        void AddFullFlowSteps(
            FlowLayoutPanel body,
            string tag,
            string step8Label,
            Func<Task> step8Action,
            string step9Label)
        {
            var scope = tag;

            AddRegisteredAction(body, scope, "1) Testing Carrier ID Read Event",
                () => WaitCarrierIdReadAsync($"{tag}_1_CIDReadEvent", $"{tag}_2_ProceedCarrier", autoProceedWithCarrier: true), 270);

            AddPassiveRowWithSideButton(body, scope, "2) Proceed With Carrier", "Bypass CarrierID Event",
                () => SendProceedWithCarrierAsync($"{tag}_2_Bypass"));

            AddPassiveRow(body, scope, "3) SlotMap Report");

            AddRegisteredAction(body, scope, "4) Proceed SlotMap",
                () => ProceedSlotMapWithDialog($"{tag}_4_ProceedSlotMap"), 270);

            AddRegisteredAction(body, scope, "5) Create ProcessJob & ControlJob",
                () => CreatePjCjWithDialog($"{tag}_5_CreatePJ", $"{tag}_5_CreateCJ"), 270);

            body.Controls.Add(new Label
            {
                Text = "PS. PJ/CJ Auto Start", AutoSize = false, Width = 300, Height = 16,
                ForeColor = Color.Gray, Font = new Font("Microsoft JhengHei UI", 7.5F, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, 0, 12, 2)
            });

            AddPassiveRow(body, scope, "6) Control Job Start");
            AddPassiveRow(body, scope, "7) Process Job Start");

            AddRegisteredAction(body, scope, step8Label, step8Action, 270);

            AddPassiveRow(body, scope, step9Label);
        }

        // ── Tab 1: Cancel Carrier ─────────────────────────────────────────────
        var tabCancel = CreateTab(tabControl, "Cancel Carrier");
        var gridCancel = CreateTwoColumnGrid();
        tabCancel.Controls.Add(gridCancel);

        var cidS = CreateSection("(1) CIDReadFail/CancelCarrier");
        var cidB = CreateSectionBody(cidS);
        AddRegisteredAction(cidB, "CancelCarrier.CIDReadFail", "1) Testing Carrier ID Read Event",
            () => WaitCarrierIdReadAsync("L2Cancel_CID_1_Wait", "L2Cancel_CID_2_ProceedCarrier", autoProceedWithCarrier: false), 270);
        AddRegisteredAction(cidB, "CancelCarrier.CIDReadFail", "2) Cancel Carrier",
            () => CancelCarrierWithDialog("L2Cancel_CID_2_Cancel"), 270);
        AddPassiveRow(cidB, "CancelCarrier.CIDReadFail", "3) ReadyToUnload Report");
        AddPassiveRow(cidB, "CancelCarrier.CIDReadFail", "4) Unload Complete Report");
        AddPassiveRow(cidB, "CancelCarrier.CIDReadFail", "5) ReadyToLoad Report");

        var slotS = CreateSection("(2) SlotMapFail/CancelCarrier");
        var slotB = CreateSectionBody(slotS);
        AddRegisteredAction(slotB, "CancelCarrier.SlotMapFail", "1) Testing Carrier ID Read Event",
            () => WaitCarrierIdReadAsync("L2Cancel_Slot_1_Wait", "L2Cancel_Slot_2_ProceedCarrier", autoProceedWithCarrier: true), 270);
        AddPassiveRowWithSideButton(slotB, "CancelCarrier.SlotMapFail", "2) Proceed With Carrier", "Bypass CarrierID Event",
            () => SendProceedWithCarrierAsync("L2Cancel_Slot_2_Bypass"));
        AddPassiveRow(slotB, "CancelCarrier.SlotMapFail", "3) SlotMap Report");
        AddRegisteredAction(slotB, "CancelCarrier.SlotMapFail", "4) Cancel Carrier",
            () => CancelCarrierWithDialog("L2Cancel_Slot_4_Cancel"), 270);
        AddPassiveRow(slotB, "CancelCarrier.SlotMapFail", "5) ReadyToUnload Report");

        gridCancel.Controls.Add(cidS, 0, 0);
        gridCancel.Controls.Add(slotS, 1, 0);

        // ── Tab 2: Process Job Stop/Abort ─────────────────────────────────────
        var tabPJ = CreateTab(tabControl, "Process Job Stop/Abort");
        var gridPJ = CreateTwoColumnGrid();
        tabPJ.Controls.Add(gridPJ);

        var pjAbortS = CreateSection("(1) ProcessJob Abort");
        var pjAbortB = CreateSectionBody(pjAbortS);
        AddFullFlowSteps(pjAbortB, "PJAbort",
            "8) S16F5 PJ Command ABORT",
            () => SendProcessJobCommandWithDialog("L2Cancel_PJAbort_8_Abort", "ABORT"),
            "9) Process Job Aborting Report");

        var pjStopS = CreateSection("(2) ProcessJob Stop");
        var pjStopB = CreateSectionBody(pjStopS);
        AddFullFlowSteps(pjStopB, "PJStop",
            "8) S16F5 PJ Command STOP",
            () => SendProcessJobCommandWithDialog("L2Cancel_PJStop_8_Stop", "STOP"),
            "9) Process Job Stopping Report");

        gridPJ.Controls.Add(pjAbortS, 0, 0);
        gridPJ.Controls.Add(pjStopS, 1, 0);

        // ── Tab 3: Control Job Stop/Abort ─────────────────────────────────────
        var tabCJ = CreateTab(tabControl, "Control Job Stop/Abort");
        var gridCJ = CreateTwoColumnGrid();
        tabCJ.Controls.Add(gridCJ);

        var cjAbortS = CreateSection("(1) ControlJob Abort");
        var cjAbortB = CreateSectionBody(cjAbortS);
        AddFullFlowSteps(cjAbortB, "CJAbort",
            "8) S16F27 CJ Command ABORT",
            () => SendControlJobCommandWithDialog("L2Cancel_CJAbort_8_Abort", 7),
            "9) Control Job Completed (ABORT) Report");

        var cjStopS = CreateSection("(2) ControlJob Stop");
        var cjStopB = CreateSectionBody(cjStopS);
        AddFullFlowSteps(cjStopB, "CJStop",
            "8) S16F27 CJ Command STOP",
            () => SendControlJobCommandWithDialog("L2Cancel_CJStop_8_Stop", 6),
            "9) Control Job Completed (STOP) Report");

        gridCJ.Controls.Add(cjAbortS, 0, 0);
        gridCJ.Controls.Add(cjStopS, 1, 0);
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

    private void RegisterEventLamp(string scope, string text, Panel lamp)
    {
        var key = MakeEventLampKey(scope, text);
        if (!_eventLamps.TryGetValue(key, out var lamps))
        {
            lamps = new List<Panel>();
            _eventLamps[key] = lamps;
        }

        lamps.Add(lamp);
    }

    private static string MakeEventLampKey(string scope, string text)
    {
        return $"{scope}\u001F{text}";
    }

    private CancelFlowPhase ActiveCancelFlowPhase
    {
        get => string.IsNullOrWhiteSpace(_activeLampScope)
            ? _cancelFlowPhase
            : _cancelFlowPhaseByScope.TryGetValue(_activeLampScope, out var phase) ? phase : CancelFlowPhase.Idle;
        set
        {
            if (string.IsNullOrWhiteSpace(_activeLampScope))
            {
                _cancelFlowPhase = value;
                return;
            }

            _cancelFlowPhaseByScope[_activeLampScope] = value;
        }
    }

    private PendingFinalReport ActivePendingFinalReport
    {
        get => string.IsNullOrWhiteSpace(_activeLampScope)
            ? _pendingFinalReport
            : _pendingFinalReportByScope.TryGetValue(_activeLampScope, out var pending) ? pending : PendingFinalReport.None;
        set
        {
            if (string.IsNullOrWhiteSpace(_activeLampScope))
            {
                _pendingFinalReport = value;
                return;
            }

            _pendingFinalReportByScope[_activeLampScope] = value;
        }
    }

    private void ActivateLampScope(string scope)
    {
        _activeLampScope = scope;
        _cancelFlowPhaseByScope.TryAdd(scope, CancelFlowPhase.Idle);
        _pendingFinalReportByScope.TryAdd(scope, PendingFinalReport.None);
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

        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(() => HandleL2CancelS6F11Report(message, text));
    }

    private async Task WaitCarrierIdReadAsync(string waitOperationName, string proceedOperationName, bool autoProceedWithCarrier)
    {
        ActiveCancelFlowPhase = CancelFlowPhase.WaitingCarrierRead;
        if (!string.IsNullOrWhiteSpace(_activeLampScope))
        {
            if (autoProceedWithCarrier)
            {
                _autoProceedOperationByScope[_activeLampScope] = proceedOperationName;
            }
            else
            {
                _autoProceedOperationByScope.Remove(_activeLampScope);
            }
        }

        if (_hasCarrierRead && !string.IsNullOrWhiteSpace(_carrierId))
        {
            AppendResult($"[INFO] Use cached CarrierIDRead: PortID={_portId}, CarrierID={_carrierId}");
            MarkEventLampPass("1) Testing Carrier ID Read Event", $"Cached CarrierIDRead Port={_portId}, CarrierID={_carrierId}");
            if (autoProceedWithCarrier)
            {
                await SendProceedWithCarrierAsync(proceedOperationName).ConfigureAwait(true);
            }

            return;
        }

        var deadline = DateTime.UtcNow.AddSeconds(60);
        AppendResult($"> Waiting {waitOperationName} S6F11 CarrierIDRead, timeout=60s");

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            var waitSlice = remaining > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : remaining;

            Secs4Net.PrimaryMessageWrapper primary;
            try
            {
                primary = await Connection.WaitForPrimaryAsync(6, 11, waitSlice).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                continue;
            }

            foreach (var line in Secs.SecsReplyInterpreter.DescribePrimary(primary))
            {
                AppendResult($"< {line}");
                Logger.Info("Primary detail: {detail}", line);
            }

            if (!TryApplyCarrierReadInfo(primary.PrimaryMessage, "wait"))
            {
                AppendResult("[INFO] S6F11 received, but it is not CarrierIDRead. Continue waiting.");
                continue;
            }

            if (autoProceedWithCarrier)
            {
                await SendProceedWithCarrierAsync(proceedOperationName).ConfigureAwait(true);
            }

            return;
        }

        throw new TimeoutException("Timed out waiting for CarrierIDRead.");
    }

    private async Task SendProceedWithCarrierAsync(string operationName)
    {
        var scope = _activeLampScope;
        try
        {
            if (!string.IsNullOrWhiteSpace(scope) && !_proceedSentScopes.Add(scope))
            {
                MarkEventLampPass("2) Proceed With Carrier", $"S3F17 ProceedWithCarrier already sent Port={_portId}, CarrierID={_carrierId}");
                return;
            }

            await SendAsync(operationName, 3, 17,
                Secs.SecsMessageFactory.S3F17ProceedWithCarrier(_carrierId, _portId)).ConfigureAwait(true);
            ActiveCancelFlowPhase = CancelFlowPhase.ProceededWithCarrier;
            MarkEventLampPass("2) Proceed With Carrier", $"S3F17 ProceedWithCarrier Port={_portId}, CarrierID={_carrierId}");
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(scope))
            {
                _proceedSentScopes.Remove(scope);
            }

            AppendResult($"[WARN] CarrierIDRead captured, but auto S3F17 ProceedWithCarrier failed: {ex.Message}");
        }
    }

    private bool TryApplyCarrierReadInfo(Secs4Net.SecsMessage message, string source)
    {
        if (!TryExtractCarrierReadInfo(message, out var portId, out var carrierId, out var locationId))
        {
            return false;
        }

        _portId = portId;
        _carrierId = carrierId;
        _lastCarrierReadLocationId = locationId;
        _hasCarrierRead = true;

        var locationText = string.IsNullOrWhiteSpace(locationId) ? string.Empty : $", LocationID={locationId}";
        AppendResult($"[INFO] CarrierIDRead captured from {source}: PortID={portId}, CarrierID={carrierId}{locationText}");
        MarkEventLampPass("1) Testing Carrier ID Read Event", $"CarrierIDRead from {source}: PortID={portId}, CarrierID={carrierId}");
        return true;
    }

    private void HandleL2CancelS6F11Report(Secs4Net.SecsMessage message, string text)
    {
        var hasCeid = Secs.SecsPayload.TryGetS6F11Ceid(message, out var ceid);
        var eventName = hasCeid ? Secs.SecsPayload.GetEventName(ceid) : string.Empty;
        var upper = (text + Environment.NewLine + eventName).ToUpperInvariant();
        var source = hasCeid ? $"S6F11 CEID={ceid} {eventName}" : "S6F11";

        if (hasCeid && ceid == 1000)
        {
            if (TryApplyCarrierReadInfo(message, "receive"))
            {
                _ = TryAutoProceedWithCarrierForActiveScopeAsync();
            }

            return;
        }

        if (IsReadyToUnloadReport(ceid, upper))
        {
            if (ActiveCancelFlowPhase < CancelFlowPhase.CancelSent)
            {
                AppendResult($"[INFO] Ignored ReadyToUnload before CancelCarrier command ({source}).");
                return;
            }

            ActiveCancelFlowPhase = CancelFlowPhase.ReadyToUnload;
            MarkEventLampPass("3) ReadyToUnload Report", source);
            MarkEventLampPass("5) ReadyToUnload Report", source);
            return;
        }

        if (IsUnloadCompleteReport(ceid, upper))
        {
            if (ActiveCancelFlowPhase < CancelFlowPhase.ReadyToUnload)
            {
                AppendResult($"[INFO] Ignored UnloadComplete before ReadyToUnload ({source}).");
                return;
            }

            ActiveCancelFlowPhase = CancelFlowPhase.UnloadComplete;
            MarkEventLampPass("4) Unload Complete Report", source);
            return;
        }

        if (IsReadyToLoadReport(ceid, upper))
        {
            if (ActiveCancelFlowPhase < CancelFlowPhase.UnloadComplete)
            {
                AppendResult($"[INFO] Ignored ReadyToLoad before UnloadComplete ({source}).");
                return;
            }

            ActiveCancelFlowPhase = CancelFlowPhase.ReadyToLoad;
            MarkEventLampPass("5) ReadyToLoad Report", source);
            return;
        }

        if (IsSlotMapReport(message, upper))
        {
            if (ActiveCancelFlowPhase < CancelFlowPhase.ProceededWithCarrier)
            {
                AppendResult($"[INFO] Ignored SlotMap before ProceedWithCarrier ({source}).");
                return;
            }

            ActiveCancelFlowPhase = CancelFlowPhase.WaitingSlotMap;
            MarkEventLampPass("3) SlotMap Report", source);
            return;
        }

        if (IsControlJobStartReport(ceid, upper))
        {
            MarkEventLampPass("6) Control Job Start", source);
            return;
        }

        if (IsProcessJobStartReport(ceid, upper))
        {
            MarkEventLampPass("7) Process Job Start", source);
            return;
        }

        if (upper.Contains("ABORTING", StringComparison.Ordinal))
        {
            if (ActivePendingFinalReport is PendingFinalReport.None or PendingFinalReport.ProcessJobAbort)
            {
                MarkEventLampPass("9) Process Job Aborting Report", source);
                ActivePendingFinalReport = PendingFinalReport.None;
            }
            else
            {
                AppendResult($"[INFO] Ignored ProcessJob ABORTING because pending final report is {ActivePendingFinalReport} ({source}).");
            }

            return;
        }

        if (upper.Contains("STOPPING", StringComparison.Ordinal))
        {
            if (ActivePendingFinalReport is PendingFinalReport.None or PendingFinalReport.ProcessJobStop)
            {
                MarkEventLampPass("9) Process Job Stopping Report", source);
                ActivePendingFinalReport = PendingFinalReport.None;
            }
            else
            {
                AppendResult($"[INFO] Ignored ProcessJob STOPPING because pending final report is {ActivePendingFinalReport} ({source}).");
            }

            return;
        }

        if (IsControlJobCompletedReport(upper))
        {
            if (ActivePendingFinalReport == PendingFinalReport.ControlJobAbort)
            {
                MarkEventLampPass("9) Control Job Completed (ABORT) Report", source);
                ActivePendingFinalReport = PendingFinalReport.None;
                return;
            }

            if (ActivePendingFinalReport == PendingFinalReport.ControlJobStop)
            {
                MarkEventLampPass("9) Control Job Completed (STOP) Report", source);
                ActivePendingFinalReport = PendingFinalReport.None;
                return;
            }

            AppendResult($"[INFO] ControlJob completed received without pending STOP/ABORT command ({source}).");
        }
    }

    private async Task TryAutoProceedWithCarrierForActiveScopeAsync()
    {
        if (string.IsNullOrWhiteSpace(_activeLampScope) ||
            !_autoProceedOperationByScope.TryGetValue(_activeLampScope, out var operationName))
        {
            return;
        }

        await SendProceedWithCarrierAsync(operationName).ConfigureAwait(true);
    }

    private void MarkEventLampPass(string text, string source)
    {
        if (string.IsNullOrWhiteSpace(_activeLampScope))
        {
            AppendResult($"[INFO] {source} -> {text} ignored: no active test section.");
            return;
        }

        var key = MakeEventLampKey(_activeLampScope, text);
        if (!_eventLamps.TryGetValue(key, out var lamps))
        {
            return;
        }

        foreach (var lamp in lamps)
        {
            lamp.BackColor = Color.FromArgb(78, 180, 95);
        }

        AppendResult($"[INFO] {source} -> {_activeLampScope}: {text}");
    }

    private static bool IsReadyToUnloadReport(uint ceid, string upper)
    {
        return ceid == 1005 || upper.Contains("READYTOUNLOAD", StringComparison.Ordinal);
    }

    private static bool IsUnloadCompleteReport(uint ceid, string upper)
    {
        return ceid == 5118 ||
            upper.Contains("UNLOADCOMPLETE", StringComparison.Ordinal) ||
            upper.Contains("CARRIERUNLOADCOMPLETE", StringComparison.Ordinal);
    }

    private static bool IsReadyToLoadReport(uint ceid, string upper)
    {
        return ceid == 1004 || upper.Contains("READYTOLOAD", StringComparison.Ordinal);
    }

    private static bool IsSlotMapReport(Secs4Net.SecsMessage message, string upper)
    {
        return upper.Contains("SLOTMAP", StringComparison.Ordinal) ||
            upper.Contains("SLOT MAP", StringComparison.Ordinal) ||
            upper.Contains("CONTENTMAP", StringComparison.Ordinal) ||
            Secs.SecsPayload.GetS6F11Rptids(message).Contains(26u);
    }

    private bool TryExtractCarrierReadInfo(Secs4Net.SecsMessage message, out byte portId, out string carrierId, out string locationId)
    {
        return Secs.SecsPayload.TryExtractCarrierReadInfo(message, out portId, out carrierId, out locationId);
    }

    private static bool TryExtractPortIdFromLocation(string value, out byte portId)
    {
        portId = 0;
        var match = System.Text.RegularExpressions.Regex.Match(
            value.Trim(),
            @"^(?:LO|LOAD)?PORT\s*([1-8])$",
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
        if (TryExtractPortIdFromLocation(normalized, out _) || IsLikelyClockValue(normalized))
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

    private static bool IsControlJobStartReport(uint ceid, string upper)
    {
        return ceid == 6199 ||
            upper.Contains("CONTROLJOBSTART", StringComparison.Ordinal) ||
            upper.Contains("CONTROL JOB START", StringComparison.Ordinal) ||
            upper.Contains("CJSTATUSCHANGE_AUTO_START", StringComparison.Ordinal);
    }

    private static bool IsProcessJobStartReport(uint ceid, string upper)
    {
        return ceid == 41 ||
            upper.Contains("PROCESSJOBSTART", StringComparison.Ordinal) ||
            upper.Contains("PROCESS JOB START", StringComparison.Ordinal) ||
            upper.Contains("PJSTATUSCHANGE_AUTO_START", StringComparison.Ordinal);
    }

    private static bool IsControlJobCompletedReport(string upper)
    {
        return upper.Contains("CONTROLJOBCOMPLETED", StringComparison.Ordinal) ||
            upper.Contains("CONTROL JOB COMPLETED", StringComparison.Ordinal) ||
            upper.Contains("CJSTATUSCHANGE_COMPLETED", StringComparison.Ordinal);
    }
}
