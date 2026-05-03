using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace HostSimTester.App.Pages;

public sealed class L1InitialTestPage : BaseTestPage
{
    private const string EquipOfflineAction = "Equipment Set Offline";
    private const string EquipOnlineAction = "Equipment Set Online";
    private const string EquipLocalAction = "Equipment Set Local";
    private const string EquipRemoteAction = "Equipment Set Remote";

    private readonly TextBox _txtTemplatePath;
    private readonly TextBox _txtLoadPortCount;
    private readonly TextBox _txtPortIds;
    private readonly Dictionary<uint, string> _equipmentActionByCeid = new();
    private string? _pendingEquipmentAction;
    private DateTime _pendingEquipmentActionAt;

    public L1InitialTestPage(Secs.SecsConnection connection)
        : base("L1 Initial Test", Logging.LoggerNames.L1Initial, connection)
    {
        Connection.PrimaryMessageReceived += OnPrimaryMessageReceived;
        Disposed += (_, _) => Connection.PrimaryMessageReceived -= OnPrimaryMessageReceived;

        ConfigureActionPanel(620, wrapContents: false);
        ClearActionItems();

        ActionPanel.AutoScroll = false;

        var tabTests = new TabControl
        {
            Width = 1150,
            Height = 600,
            Margin = new Padding(0),
            Padding = new Point(12, 6)
        };
        var tabMain = new TabPage("Comm && Template")
        {
            BackColor = Theme.ThemeHelper.IceSurface
        };
        var tabControlMode = new TabPage("Control Mode")
        {
            BackColor = Theme.ThemeHelper.IceSurface
        };
        var tabEventAccess = new TabPage("Event && Access")
        {
            BackColor = Theme.ThemeHelper.IceSurface
        };
        tabTests.TabPages.Add(tabMain);
        tabTests.TabPages.Add(tabControlMode);
        tabTests.TabPages.Add(tabEventAccess);
        ActionPanel.Controls.Add(tabTests);

        var gridMain = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        gridMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        gridMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        gridMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabMain.Controls.Add(gridMain);

        var gridControlMode = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        gridControlMode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        gridControlMode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        gridControlMode.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabControlMode.Controls.Add(gridControlMode);

        var gridEventAccess = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        gridEventAccess.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        gridEventAccess.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        gridEventAccess.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabEventAccess.Controls.Add(gridEventAccess);

        var commSection = CreateSection("2. Establish Communication Test");
        var excelSection = CreateSection("3. TSMC Excel File Template for \"Define Event Report\"");
        var equipmentControlSection = CreateSection("4.1 Equipment Control Mode Check");
        var hostControlSection = CreateSection("4.2 Host Control Mode Check");
        var defineEventSection = CreateSection("5. Define Event Report Test");
        var accessModeSection = CreateSection("6. Access Mode Check");

        gridMain.Controls.Add(commSection, 0, 0);
        gridMain.Controls.Add(excelSection, 1, 0);
        gridControlMode.Controls.Add(equipmentControlSection, 0, 0);
        gridControlMode.Controls.Add(hostControlSection, 1, 0);
        gridEventAccess.Controls.Add(defineEventSection, 0, 0);
        gridEventAccess.Controls.Add(accessModeSection, 1, 0);

        var commBody = CreateSectionBody(commSection);
        commBody.Controls.Add(new Label { Text = "Establish communication (equipment-to-host)", Width = 360, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(commBody, "Equipment-to-Host (receive S1F13)", () =>
        {
            // 若連線後任一時刻已收過 S1F13，直接視為成功，避免測試步驟晚於 60 秒造成誤判。
            if (connection.LastS1F13ReceivedAt.HasValue)
            {
                Logger.Info("S1F13 already received at " + connection.LastS1F13ReceivedAt.Value.ToString("HH:mm:ss"));
                AppendResult("[INFO] Equipment-to-Host check passed by previously received S1F13.");
                return Task.CompletedTask;
            }
            return WaitPrimaryAsync("L1Initial_WaitS1F13", 1, 13, 30);
        }, 270);
        commBody.Controls.Add(new Label { Text = "Establish communication (host-to-equipment)", Width = 360, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(commBody, "Host-to-Equipment (send S1F13)", () => SendAsync("L1Initial_S1F13", 1, 13, Secs.SecsMessageFactory.S1F13EstablishCommunicationRequest()), 270);

        var excelBody = CreateSectionBody(excelSection);
        excelBody.Controls.Add(new Label { Text = "Step 1. Open the TSMC Excel template", Width = 420, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        excelBody.Controls.Add(new Label { Text = "Step 2. Vendor fills in and imports the Excel file", Width = 420, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 0, 6, 0) });

        _txtTemplatePath = new TextBox
        {
            Dock = DockStyle.Fill,
            Height = 24,
            BackColor = Theme.ThemeHelper.TableBg
        };

        AddActionTo(
            excelBody,
            "Import Excel File",
            () =>
            {
                using var dialog = new OpenFileDialog
                {
                    Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                    Title = "Select Define Event Report Template"
                };

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _txtTemplatePath.Text = dialog.FileName;
                    AppSession.MarkL1InitialExcelImported(dialog.FileName);
                    Logger.Info($"Imported template path: {dialog.FileName}");
                    AppendResult($"[INFO] L1 Initial Excel imported: {Path.GetFileName(dialog.FileName)}");
                    try
                    {
                        var content = Excel.ExcelTemplateReader.Read(dialog.FileName);
                        AppSession.SetL1InitialTemplateContent(content.Ceids, content.Dvids, content.Rptids);
                        AppendResult($"[INFO] Parsed template -> CEIDs={content.Ceids.Count}, DVIDs={content.Dvids.Count}, RPTIDs={content.Rptids.Count}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Excel parse failed; will treat as empty template.");
                        AppSession.SetL1InitialTemplateContent(Array.Empty<uint>(), Array.Empty<uint>(), Array.Empty<uint>());
                        AppendResult($"[WARN] Template parse failed: {ex.Message}. Will run flow without per-ID verification.");
                    }
                    return Task.CompletedTask;
                }

                throw new InvalidOperationException("Import cancelled.");
            },
            230);

        AddActionTo(
            excelBody,
            "View Excel File",
            () =>
            {
                var path = _txtTemplatePath.Text.Trim();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    throw new InvalidOperationException("Template file not found. Please import first.");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });

                Logger.Info($"Open template file: {path}");
                return Task.CompletedTask;
            },
            230);

        var txtWrapPanel = new Panel { Width = 410, Height = 28, Margin = new Padding(34, 0, 6, 0) };
        txtWrapPanel.Controls.Add(_txtTemplatePath);
        excelBody.Controls.Add(txtWrapPanel);

        var equipmentControlBody = CreateSectionBody(equipmentControlSection);
        equipmentControlBody.Controls.Add(new Label { Text = "Observe equipment state event (S6F11)", Width = 380, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(equipmentControlBody, EquipOfflineAction, () => WaitPrimaryAsync("L1Initial_EquipSetOffline_S6F11", 6, 11, 30), 250);
        AddActionTo(equipmentControlBody, EquipOnlineAction, () => WaitPrimaryAsync("L1Initial_EquipSetOnline_S6F11", 6, 11, 30), 250);
        AddActionTo(equipmentControlBody, EquipLocalAction, () => WaitPrimaryAsync("L1Initial_EquipSetLocal_S6F11", 6, 11, 30), 250);
        AddActionTo(equipmentControlBody, EquipRemoteAction, () => WaitPrimaryAsync("L1Initial_EquipSetRemote_S6F11", 6, 11, 30), 250);

        var hostControlBody = CreateSectionBody(hostControlSection);
        hostControlBody.Controls.Add(new Label { Text = "Switch control mode (host-to-equipment)", Width = 360, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(hostControlBody, "Host Set Offline (S1F15)", () => SendHostModeAndTrackAsync("L1Initial_S1F15", 1, 15, null, EquipOfflineAction), 250);
        AddActionTo(hostControlBody, "Host Set Online (S1F17)", () => SendHostModeAndTrackAsync("L1Initial_S1F17", 1, 17, null, EquipOnlineAction), 250);
        AddActionTo(hostControlBody, "Host Set Local (S2F41)", () => SendHostModeAndTrackAsync("L1Initial_S2F41_SetLocal", 2, 41, Secs.SecsMessageFactory.S2F41HostCommand("GO-LOCAL"), EquipLocalAction), 250);
        AddActionTo(hostControlBody, "Host Set Remote (S2F41)", () => SendHostModeAndTrackAsync("L1Initial_S2F41_SetRemote", 2, 41, Secs.SecsMessageFactory.S2F41HostCommand("GO-REMOTE"), EquipRemoteAction), 250);

        var defineEventBody = CreateSectionBody(defineEventSection);
        defineEventBody.Controls.Add(new Label { Text = "Run all 8 steps in one click with sample CEID/RPTID/DVID", Width = 420, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(defineEventBody, "Run Define Event Report Test", async () =>
        {
            // Step order aligned with reference TSMC Tester:
            // 1) DisableAllEvent  2) UnlinkEventReport  3) DeleteAllEventReport
            // 4) VerifyEachCEID (one S2F37 per CEID from Excel)
            // 5) VerifyEachDVID (one S2F37 per DVID from Excel)
            // 6) DefineEventReport  7) LinkEventReport  8) EnableEventReport
            await SendAsync("L1Initial_Step1_DisableAllEvent_S2F37", 2, 37, Secs.SecsMessageFactory.S2F37EnableDisableEventReport(false)).ConfigureAwait(true);
            await SendAsync("L1Initial_Step2_UnlinkEventReport_S2F35", 2, 35, Secs.SecsMessageFactory.S2F35UnlinkAllReports()).ConfigureAwait(true);
            await SendAsync("L1Initial_Step3_DeleteAllEventReport_S2F33", 2, 33, Secs.SecsMessageFactory.S2F33DeleteAllReports()).ConfigureAwait(true);

            var ceids = AppSession.L1InitialCeids;
            if (ceids.Count == 0)
            {
                AppendResult("[INFO] Step4 VerifyEachCEID: no CEIDs from Excel, sending one fallback S2F37.");
                await SendAsync("L1Initial_Step4_VerifyCEID_S2F37", 2, 37, Secs.SecsMessageFactory.S2F37EnableDisableEventReport(true)).ConfigureAwait(true);
            }
            else
            {
                AppendResult($"[INFO] Step4 VerifyEachCEID: {ceids.Count} CEIDs from Excel.");
                foreach (var ceid in ceids)
                {
                    await SendAsync($"L1Initial_Step4_VerifyCEID_{ceid}_S2F37", 2, 37, Secs.SecsMessageFactory.S2F37EnableDisableEventReport(true)).ConfigureAwait(true);
                }
            }

            var dvids = AppSession.L1InitialDvids;
            if (dvids.Count == 0)
            {
                AppendResult("[INFO] Step5 VerifyEachDVID: no DVIDs from Excel, sending one fallback S2F37.");
                await SendAsync("L1Initial_Step5_VerifyDVID_S2F37", 2, 37, Secs.SecsMessageFactory.S2F37EnableDisableEventReport(true)).ConfigureAwait(true);
            }
            else
            {
                AppendResult($"[INFO] Step5 VerifyEachDVID: {dvids.Count} DVIDs from Excel.");
                foreach (var dvid in dvids)
                {
                    await SendAsync($"L1Initial_Step5_VerifyDVID_{dvid}_S2F37", 2, 37, Secs.SecsMessageFactory.S2F37EnableDisableEventReport(true)).ConfigureAwait(true);
                }
            }

            var rptids = AppSession.L1InitialRptids.Count > 0
                ? AppSession.L1InitialRptids.Where(x => x <= ushort.MaxValue).Distinct().ToArray()
                : [1u];
            var defineDvids = AppSession.L1InitialDvids.Count > 0
                ? AppSession.L1InitialDvids.Distinct().ToArray()
                : [101001u, 101002u];

            AppendResult($"[INFO] Step6 DefineEventReport: RPTIDs={string.Join(",", rptids)}, DVIDs={defineDvids.Length}");
            foreach (var rptid in rptids)
            {
                await SendAsync($"L1Initial_Step6_DefineEventReport_RPTID_{rptid}_S2F33", 2, 33,
                    Secs.SecsMessageFactory.S2F33DefineReport(rptid, defineDvids)).ConfigureAwait(true);
            }

            var linkCeids = AppSession.L1InitialCeids.Count > 0
                ? AppSession.L1InitialCeids.Distinct().ToArray()
                : [1001u];
            AppendResult($"[INFO] Step7 LinkEventReport: CEIDs={linkCeids.Length}, RPTIDs={rptids.Length}");
            foreach (var ceid in linkCeids)
            {
                foreach (var rptid in rptids)
                {
                    await SendAsync($"L1Initial_Step7_LinkEventReport_CEID_{ceid}_RPTID_{rptid}_S2F35", 2, 35,
                        Secs.SecsMessageFactory.S2F35LinkEventReport(ceid, rptid)).ConfigureAwait(true);
                }
            }

            // Diagnostic summary for quick comparison with reference tester logs.
            var ceidPreview = string.Join(",", linkCeids.Take(20)) + (linkCeids.Length > 20 ? ",..." : string.Empty);
            var rptidPreview = string.Join(",", rptids.Take(20)) + (rptids.Length > 20 ? ",..." : string.Empty);
            var dvidPreview = string.Join(",", defineDvids.Take(30)) + (defineDvids.Length > 30 ? ",..." : string.Empty);
            AppendResult($"[DEBUG] DefineEvent summary: CEIDs({linkCeids.Length})=[{ceidPreview}] RPTIDs({rptids.Length})=[{rptidPreview}] DVIDs({defineDvids.Length})=[{dvidPreview}]");
            Logger.Info("DefineEvent summary: CEIDs({ceidCount})=[{ceids}] RPTIDs({rptCount})=[{rptids}] DVIDs({dvidCount})=[{dvids}]",
                linkCeids.Length,
                ceidPreview,
                rptids.Length,
                rptidPreview,
                defineDvids.Length,
                dvidPreview);

            // Carrier-related DVIDs should come from Excel template mapping.
            // Typical E87 set in your template: 2000(CarrierID), 443(CarrierIDStatus), 444(CarrierAccessingStatus), 446(ContentMap).
            var keyCarrierDvids = defineDvids.Where(d => d is 2000u or 443u or 444u or 446u).Distinct().ToArray();
            if (keyCarrierDvids.Length == 0)
            {
                AppendResult("[WARN] DefineEvent DVID list does not include 2000/443/444/446. Carrier-related fields may not be reported in S6F11.");
                Logger.Warn("DefineEvent DVID list does not include 2000/443/444/446. Carrier-related fields may not be reported in S6F11.");
            }
            else
            {
                AppendResult($"[INFO] DefineEvent includes carrier-related DVIDs: {string.Join(",", keyCarrierDvids)}");
                Logger.Info("DefineEvent includes carrier-related DVIDs: {dvids}", string.Join(",", keyCarrierDvids));
            }

            await SendAsync("L1Initial_Step8_EnableEventReport_S2F37", 2, 37, Secs.SecsMessageFactory.S2F37EnableDisableEventReport(true)).ConfigureAwait(true);
            AppSession.MarkL1InitialDefineEventCompleted();
            AppendResult("[INFO] Define Event Report Test completed (8 steps).");
            if (AppSession.IsL1InitialCompleted)
            {
                AppendResult("[INFO] L1 Initial completed. You can continue to L1 Normal/L2 tabs.");
            }
        }, 280);

        var accessModeBody = CreateSectionBody(accessModeSection);
        accessModeBody.Controls.Add(new Label { Text = "Set load port count and port IDs (comma-separated)", Width = 420, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        accessModeBody.Controls.Add(new Label { Text = "Load Port Count", Width = 120, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 6, 6, 0) });
        _txtLoadPortCount = new TextBox { Width = 180, Height = 24, BackColor = Theme.ThemeHelper.TableBg, Margin = new Padding(34, 0, 6, 4), Text = "2" };
        accessModeBody.Controls.Add(_txtLoadPortCount);
        accessModeBody.Controls.Add(new Label { Text = "Port IDs (e.g., 1,2,3)", Width = 180, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        _txtPortIds = new TextBox { Width = 260, Height = 24, BackColor = Theme.ThemeHelper.TableBg, Margin = new Padding(34, 0, 6, 4), Text = "1,2" };
        accessModeBody.Controls.Add(_txtPortIds);

        AddActionTo(accessModeBody, "Test AUTO (S3F27)", () => SendAccessModeByPortsAsync(0x01), 250);
        AddActionTo(accessModeBody, "Test MANUAL (S3F27)", () => SendAccessModeByPortsAsync(0x00), 250);
    }

    private async Task SendAccessModeByPortsAsync(byte mode)
    {
        if (mode is not 0x00 and not 0x01)
        {
            throw new InvalidOperationException($"Invalid AccessMode: {mode}. Expected 0 (MANUAL) or 1 (AUTO).");
        }

        if (!byte.TryParse(_txtLoadPortCount.Text.Trim(), out var loadPortCount) || loadPortCount == 0)
        {
            throw new InvalidOperationException("LoadPort count must be a positive number.");
        }

        var portIds = ParsePortIds(_txtPortIds.Text);
        if (portIds.Length == 0)
        {
            throw new InvalidOperationException("Please input at least one Port ID.");
        }

        if (loadPortCount != portIds.Length)
        {
            throw new InvalidOperationException($"LoadPort count ({loadPortCount}) must match Port ID count ({portIds.Length}).");
        }

        var payload = Secs.SecsMessageFactory.S3F27SetAccessMode(mode, portIds);
        var operationName = mode == 0x01
            ? "L1Initial_SetAccessAuto_S3F27"
            : "L1Initial_SetAccessManual_S3F27";
        await SendAsync(operationName, 3, 27, payload).ConfigureAwait(true);

        var modeName = mode == 0x01 ? "AUTO" : "MANUAL";
        AppendResult($"[INFO] S3F27 {modeName} test completed. LoadPort={loadPortCount}, PortIDs={string.Join(",", portIds)}");
    }

    private async Task SendHostModeAndTrackAsync(string operationName, byte stream, byte function, Secs4Net.Item? payload, string expectedEquipmentAction)
    {
        _pendingEquipmentAction = expectedEquipmentAction;
        _pendingEquipmentActionAt = DateTime.Now;
        await SendAsync(operationName, stream, function, payload).ConfigureAwait(true);
    }

    private void OnPrimaryMessageReceived(Secs4Net.PrimaryMessageWrapper wrapper)
    {
        if (wrapper.PrimaryMessage.S != 6 || wrapper.PrimaryMessage.F != 11)
        {
            return;
        }

        // Object-level CEID extraction (S6F11 = L[3]( DataID, CEID, ReportList )).
        Secs.SecsPayload.TryGetS6F11Ceid(wrapper.PrimaryMessage, out var ceid);
        var raw = wrapper.PrimaryMessage.ToString();
        BeginInvoke(() => HandleEquipmentS6F11(raw, ceid));
    }

    private void HandleEquipmentS6F11(string raw, uint ceid)
    {
        if (TryExtractActionByKeyword(raw, out var actionByKeyword))
        {
            SetActionLampToPass(actionByKeyword);
            AppendResult($"[INFO] Equipment state detected: {actionByKeyword}");
            return;
        }

        if (ceid == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_pendingEquipmentAction) &&
            (DateTime.Now - _pendingEquipmentActionAt) <= TimeSpan.FromSeconds(10))
        {
            _equipmentActionByCeid[ceid] = _pendingEquipmentAction;
            SetActionLampToPass(_pendingEquipmentAction);
            AppendResult($"[INFO] Learned control CEID mapping: CEID={ceid} -> {_pendingEquipmentAction}");
            _pendingEquipmentAction = null;
            return;
        }

        if (_equipmentActionByCeid.TryGetValue(ceid, out var mappedAction))
        {
            SetActionLampToPass(mappedAction);
            AppendResult($"[INFO] Equipment state detected by CEID={ceid}: {mappedAction}");
        }
    }

    private static bool TryExtractCeid(string raw, out uint ceid)
    {
        ceid = 0;
        var match = Regex.Match(raw, @"<U(?:1|2|4|8)\s*\[\d+\]\s*\d+\s*>\s*<U(?:1|2|4|8)\s*\[\d+\]\s*(\d+)\s*>", RegexOptions.Singleline);
        return match.Success && uint.TryParse(match.Groups[1].Value, out ceid);
    }

    private static bool TryExtractActionByKeyword(string raw, out string action)
    {
        action = string.Empty;
        var upper = raw.ToUpperInvariant();
        if (upper.Contains("OFFLINE", StringComparison.Ordinal))
        {
            action = EquipOfflineAction;
            return true;
        }
        if (upper.Contains("ONLINE", StringComparison.Ordinal))
        {
            action = EquipOnlineAction;
            return true;
        }
        if (upper.Contains("LOCAL", StringComparison.Ordinal))
        {
            action = EquipLocalAction;
            return true;
        }
        if (upper.Contains("REMOTE", StringComparison.Ordinal))
        {
            action = EquipRemoteAction;
            return true;
        }

        return false;
    }

    private static byte[] ParsePortIds(string input)
    {
        var rawParts = input
            .Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .ToArray();

        var result = new List<byte>();
        foreach (var part in rawParts)
        {
            if (!byte.TryParse(part, out var id))
            {
                throw new InvalidOperationException($"Invalid Port ID: {part} (must be 0-255).");
            }
            result.Add(id);
        }

        return result.ToArray();
    }
}

