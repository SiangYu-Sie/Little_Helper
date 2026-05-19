using Secs4Net;

namespace HostSimTester.App.Pages;

public sealed class L2RecipePage : BaseTestPage
{
    private readonly ComboBox _cboMainPpid;
    private readonly ComboBox _cboSubPpid;
    private readonly ComboBox _cboMainPpidDel;
    private readonly ComboBox _cboSubPpidDel;
    private readonly TextBox _txtPpidResult;
    private readonly Dictionary<string, Secs.SecsMessageFactory.FormattedRecipeTemplate> _formattedRecipeCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _unformattedRecipeCache =
        new(StringComparer.OrdinalIgnoreCase);

    public L2RecipePage(Secs.SecsConnection connection)
        : base("L2 Recipe", Logging.LoggerNames.L2Recipe, connection)
    {
        _cboMainPpid    = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDown };
        _cboSubPpid     = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDown };
        _cboMainPpidDel = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDown };
        _cboSubPpidDel  = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDown };
        _txtPpidResult  = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Width = 196,
            Height = 360,
            BackColor = Theme.ThemeHelper.TableBg,
            Margin = new Padding(6, 2, 6, 2)
        };

        ConfigureActionPanel(640, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        var tabControl = CreateTabControl();

        // ── Tab: Recipe Management ──────────────────────────────────────────
        var tabRecipe = CreateTab(tabControl, "Recipe Management");

        // 3-column grid: left=220, mid=50%, right=50%
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 3,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabRecipe.Controls.Add(grid);

        // ── Section 1: PPID List Inquiry (left) ─────────────────────────────
        var s1 = CreateSection("1. PPID List Inquiry");
        var s1Body = CreateSectionBody(s1);
        AddActionTo(s1Body, "(1) PPID List Inquiry", QueryPpidListAsync, 190);
        s1Body.Controls.Add(new Label
        {
            Text = "PPID List Inquiry Result :",
            ForeColor = Theme.ThemeHelper.TextMid,
            Width = 196,
            Margin = new Padding(6, 8, 6, 2)
        });
        s1Body.Controls.Add(_txtPpidResult);
        grid.Controls.Add(s1, 0, 0);

        // ── Section 2: Recipe Body Upload/Download (middle) ──────────────────
        var s2 = CreateSection("2. Recipe Body Upload/Download");
        var s2Body = CreateSectionBody(s2);

        var mainBar = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(6, 6, 6, 0) };
        mainBar.Controls.Add(new Label { Text = "Main Recipe PPID :", Width = 124, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft });
        mainBar.Controls.Add(_cboMainPpid);
        s2Body.Controls.Add(mainBar);

        AddActionTo(s2Body, "(2.1) Formatted Recipe Body Upload",
            () => FormattedUploadAndCacheAsync(GetMainPpid()), 280);
        AddActionTo(s2Body, "(2.2) Formatted Recipe Body Download",
            () => FormattedDownloadWithCacheAsync(GetMainPpid()), 280);
        AddActionTo(s2Body, "(3.1) UnFormatted Recipe Body Upload",
            () => UnformattedUploadAndCacheAsync(GetMainPpid()), 280);
        AddActionTo(s2Body, "(3.2) UnFormatted Recipe Body Download",
            () => UnformattedDownloadWithCacheAsync(GetMainPpid()), 280);

        var subBar = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(6, 10, 6, 0) };
        subBar.Controls.Add(new Label { Text = "Sub Recipe PPID :", Width = 124, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft });
        subBar.Controls.Add(_cboSubPpid);
        s2Body.Controls.Add(subBar);

        AddActionTo(s2Body, "(4.1) Formatted Recipe Body Upload",
            () => FormattedUploadAndCacheAsync(GetSubPpid()), 280);
        AddActionTo(s2Body, "(4.2) Formatted Recipe Body Download",
            () => FormattedDownloadWithCacheAsync(GetSubPpid()), 280);
        AddActionTo(s2Body, "(5.1) UnFormatted Recipe Body Upload",
            () => UnformattedUploadAndCacheAsync(GetSubPpid()), 280);
        AddActionTo(s2Body, "(5.2) UnFormatted Recipe Body Download",
            () => UnformattedDownloadWithCacheAsync(GetSubPpid()), 280);

        grid.Controls.Add(s2, 1, 0);

        // ── Section 3: Delete Recipe (right) ─────────────────────────────────
        var s3 = CreateSection("3. Delete Recipe");
        var s3Body = CreateSectionBody(s3);

        var mainDelBar = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(6, 6, 6, 0) };
        mainDelBar.Controls.Add(new Label { Text = "Main Recipe PPID :", Width = 124, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft });
        mainDelBar.Controls.Add(_cboMainPpidDel);
        s3Body.Controls.Add(mainDelBar);

        AddActionTo(s3Body, "(6.1) Delete Recipe (Main Recipe)",
            () => SendAsync("L2Recipe_S7F17_Main_Delete", 7, 17,
                Secs.SecsMessageFactory.S7F17DeleteProcessProgramSend(GetMainPpidDel())), 260);
        AddActionTo(s3Body, "(6.2) PPID List Inquiry",
            QueryPpidListAsync, 260);

        var subDelBar = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(6, 10, 6, 0) };
        subDelBar.Controls.Add(new Label { Text = "Sub Recipe PPID :", Width = 124, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft });
        subDelBar.Controls.Add(_cboSubPpidDel);
        s3Body.Controls.Add(subDelBar);

        AddActionTo(s3Body, "(7.1) Delete Recipe (Sub Recipe)",
            () => SendAsync("L2Recipe_S7F17_Sub_Delete", 7, 17,
                Secs.SecsMessageFactory.S7F17DeleteProcessProgramSend(GetSubPpidDel())), 260);
        AddActionTo(s3Body, "(7.2) PPID List Inquiry",
            QueryPpidListAsync, 260);

        grid.Controls.Add(s3, 2, 0);
    }

    private string GetMainPpid()    => _cboMainPpid.Text.Trim();
    private string GetSubPpid()     => _cboSubPpid.Text.Trim();
    private string GetMainPpidDel() => _cboMainPpidDel.Text.Trim();
    private string GetSubPpidDel()  => _cboSubPpidDel.Text.Trim();

    private async Task QueryPpidListAsync()
    {
        var reply = await Connection.SendAsync("L2Recipe_S7F19_QueryList", 7, 19).ConfigureAwait(true);
        AppendResult("> L2Recipe_S7F19_QueryList S7F19");
        AppendResult($"< Reply S{reply?.S}F{reply?.F}");

        // S7F20: L[ A(ppid1), A(ppid2), ... ]
        var root = reply?.SecsItem;
        AppendResult($"< Raw: {root}");

        var ppids = ParsePpidList(root);
        _txtPpidResult.Text = ppids.Count > 0
            ? string.Join(Environment.NewLine, ppids)
            : "(no PPIDs returned)";

        PopulateCombo(_cboMainPpid,    ppids);
        PopulateCombo(_cboSubPpid,     ppids);
        PopulateCombo(_cboMainPpidDel, ppids);
        PopulateCombo(_cboSubPpidDel,  ppids);
    }

    private static List<string> ParsePpidList(Item? root)
    {
        if (root is null || root.Format != SecsFormat.List)
            return [];

        var result = new List<string>();
        for (int i = 0; i < root.Count; i++)
        {
            var sub = root[i];
            if (sub.Format == SecsFormat.ASCII)
            {
                var s = sub.GetString().TrimStart('/');
                if (s.Length > 0) result.Add(s);
            }
        }
        return result;
    }

    private static void PopulateCombo(ComboBox combo, IList<string> items)
    {
        var current = combo.Text;
        combo.Items.Clear();
        foreach (var item in items)
            combo.Items.Add(item);
        if (combo.Items.Count > 0)
            combo.Text = combo.Items.Cast<string>().Contains(current) ? current : combo.Items[0]!.ToString()!;
    }

    private async Task FormattedUploadAndCacheAsync(string ppid)
    {
        if (string.IsNullOrWhiteSpace(ppid))
        {
            throw new InvalidOperationException("PPID is empty. Please select or input a PPID first.");
        }

        var reply = await SendWithReplyAsync(
            "L2Recipe_S7F25_FormattedProcessProgramRequest_FormattedRecipeBodyUpload",
            7,
            25,
            Secs.SecsMessageFactory.S7F25FormattedProcessProgramRequest(ppid)).ConfigureAwait(true);

        if (reply?.SecsItem is null)
        {
            AppendResult($"[WARN] No S7F26 body to cache for PPID={ppid}.");
            return;
        }

        if (Secs.SecsMessageFactory.TryExtractFormattedRecipeTemplateFromS7F26(reply.SecsItem, out var template, out var reason))
        {
            _formattedRecipeCache[ppid] = template;
            if (template.RawFormattedBody is not null)
            {
                AppendResult($"[INFO] Cached S7F26 template for PPID={ppid}, MDLN={template.Model}, SOFTREV={template.SoftRev}, format=RAW_GROUPS({template.RawFormattedBody.Count}).");
            }
            else
            {
                AppendResult($"[INFO] Cached S7F26 template for PPID={ppid}, MDLN={template.Model}, SOFTREV={template.SoftRev}, CCODE={template.CCode}, PPARM={template.Pparms.Count}.");
            }

            if (template.RawFormattedBody is null && template.Pparms.Count != 40)
            {
                AppendResult($"[WARN] Equipment requires 40 PPARM. Current cache={template.Pparms.Count}, S7F23 send will normalize to 40.");
            }
        }
        else
        {
            AppendResult($"[WARN] S7F26 cache skipped for PPID={ppid}: {reason}");
        }
    }

    private async Task FormattedDownloadWithCacheAsync(string ppid)
    {
        if (string.IsNullOrWhiteSpace(ppid))
        {
            throw new InvalidOperationException("PPID is empty. Please select or input a PPID first.");
        }

        if (!_formattedRecipeCache.TryGetValue(ppid, out var template))
        {
            throw new InvalidOperationException($"No cached S7F26 template for PPID={ppid}. Run formatted upload first.");
        }

        var payload = Secs.SecsMessageFactory.S7F23FormattedProcessProgramSend(ppid, template, refreshTime: true);
        AppendResult($"[INFO] S7F23 uses cached template for PPID={ppid}.");

        await SendWithReplyAsync(
            "L2Recipe_S7F23_FormattedProcessProgramSend_FormattedRecipeBodyDownload",
            7,
            23,
            payload).ConfigureAwait(true);
    }

    private async Task UnformattedUploadAndCacheAsync(string ppid)
    {
        if (string.IsNullOrWhiteSpace(ppid))
        {
            throw new InvalidOperationException("PPID is empty. Please select or input a PPID first.");
        }

        var reply = await SendWithReplyAsync(
            "L2Recipe_S7F5_UnformattedProcessProgramRequest_UnformattedRecipeBodyUpload",
            7,
            5,
            Secs.SecsMessageFactory.S7F5UnformattedProcessProgramRequest(ppid)).ConfigureAwait(true);

        if (reply?.SecsItem is null)
        {
            AppendResult($"[WARN] No S7F6 body to cache for PPID={ppid}.");
            return;
        }

        if (Secs.SecsMessageFactory.TryExtractUnformattedRecipeBodyFromS7F6(reply.SecsItem, out var body, out var reason))
        {
            _unformattedRecipeCache[ppid] = body;
            AppendResult($"[INFO] Cached S7F6 body for PPID={ppid}, bytes={body.Length}.");
        }
        else
        {
            AppendResult($"[WARN] S7F6 cache skipped for PPID={ppid}: {reason}");
        }
    }

    private async Task UnformattedDownloadWithCacheAsync(string ppid)
    {
        if (string.IsNullOrWhiteSpace(ppid))
        {
            throw new InvalidOperationException("PPID is empty. Please select or input a PPID first.");
        }

        if (!_unformattedRecipeCache.TryGetValue(ppid, out var body) || body.Length == 0)
        {
            throw new InvalidOperationException($"No cached S7F6 body for PPID={ppid}. Run unformatted upload first.");
        }

        var payload = Secs.SecsMessageFactory.S7F3UnformattedProcessProgramSend(ppid, body);
        AppendResult($"[INFO] S7F3 uses cached S7F6 body for PPID={ppid}, bytes={body.Length}.");

        await SendWithReplyAsync(
            "L2Recipe_S7F3_UnformattedProcessProgramSend_UnformattedRecipeBodyDownload",
            7,
            3,
            payload).ConfigureAwait(true);
    }

    private async Task<SecsMessage?> SendWithReplyAsync(string operationName, byte stream, byte function, Item? payload = null, bool expectReply = true)
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

        return reply;
    }
}

