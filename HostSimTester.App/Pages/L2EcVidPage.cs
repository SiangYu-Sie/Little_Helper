using Secs4Net;
using System.Text.RegularExpressions;

namespace HostSimTester.App.Pages;

public sealed class L2EcVidPage : BaseTestPage
{
    private readonly ComboBox _cboEcidListQuery;
    private readonly ComboBox _cboEcidTypeQuery;
    private readonly ComboBox _cboEcidListSet;
    private readonly ComboBox _cboEcidTypeSet;
    private readonly TextBox  _txtEcvValue;
    private readonly ComboBox _cboEcvType;

    private readonly Label _lblDvidCount;
    private readonly Label _lblCeidCount;
    private readonly Label _lblSvidCount;
    private readonly Label _lblEcidCount;
    private readonly Label _lblSelectedEcidQuery;
    private readonly Label _lblSelectedEcidSet;
    private readonly Label _lblEcValue;

    public L2EcVidPage(Secs.SecsConnection connection)
        : base("L2 EC/VID", Logging.LoggerNames.L2Vid, connection)
    {
        static ComboBox TypeCombo() 
        {
            var c = new ComboBox { Width = 60, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var t in new[] { "U1", "U2", "U4", "I1", "I2", "I4", "F4", "F8", "A" })
                c.Items.Add(t);
            c.SelectedIndex = 0;
            return c;
        }

        _cboEcidListQuery  = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDown };
        _cboEcidTypeQuery  = TypeCombo();
        _cboEcidListSet    = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDown };
        _cboEcidTypeSet    = TypeCombo();
        _txtEcvValue       = new TextBox { Width = 100, Text = "1" };
        _cboEcvType        = TypeCombo();

        _lblDvidCount         = DisplayLabel(80);
        _lblCeidCount         = DisplayLabel(80);
        _lblSvidCount         = DisplayLabel(80);
        _lblEcidCount         = DisplayLabel(80);
        _lblSelectedEcidQuery = DisplayLabel(120);
        _lblSelectedEcidSet   = DisplayLabel(120);
        _lblEcValue           = DisplayLabel(140);

        _cboEcidListQuery.SelectedIndexChanged += (_, _) => _lblSelectedEcidQuery.Text = _cboEcidListQuery.Text;
        _cboEcidListQuery.TextChanged          += (_, _) => _lblSelectedEcidQuery.Text = _cboEcidListQuery.Text;
        _cboEcidListSet.SelectedIndexChanged   += (_, _) => _lblSelectedEcidSet.Text   = _cboEcidListSet.Text;
        _cboEcidListSet.TextChanged            += (_, _) => _lblSelectedEcidSet.Text   = _cboEcidListSet.Text;

        ConfigureActionPanel(640, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        var tabControl = CreateTabControl();
        var tabQuery = CreateTab(tabControl, "Query All VID/EC");
        var grid = CreateTwoColumnGrid();
        tabQuery.Controls.Add(grid);

        // ── Left: 4 query sections stacked ─────────────────────────────────
        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(0)
        };
        for (int i = 0; i < 4; i++)
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        // Section 1: Query All DVID
        var s1 = CreateSection("1. Query All DVID");
        var s1Body = CreateSectionBody(s1);
        AddActionTo(s1Body, "S1,F21 Data Variable Namelist Request", QueryDvidAsync, 270);
        s1Body.Controls.Add(CountRow("DVVAL Total Count =", _lblDvidCount));
        s1Body.Controls.Add(InfoLabel("Equipment can report the data variable name (DVVALNAME) in S1,F22"));
        s1Body.Controls.Add(InfoLabel("Equipment can report the data variable unit (UNITS) in S1,F22"));

        // Section 2: Query All CEID/VID
        var s2 = CreateSection("2. Query All CEID/VID");
        var s2Body = CreateSectionBody(s2);
        AddActionTo(s2Body, "S1,F23 Collection Event Namelist Request", QueryCeidAsync, 270);
        s2Body.Controls.Add(CountRow("CEID Total Count =", _lblCeidCount));
        s2Body.Controls.Add(InfoLabel("Equipment can report the event name (CENAME) in S1,F24"));
        s2Body.Controls.Add(InfoLabel("Equipment can report the associated VID list in S1,F24"));

        // Section 3: Query All SVID
        var s3 = CreateSection("3. Query All SVID");
        var s3Body = CreateSectionBody(s3);
        AddActionTo(s3Body, "S1,F11 Status Variable Namelist Request", QuerySvidAsync, 270);
        s3Body.Controls.Add(CountRow("SVID Total Count =", _lblSvidCount));

        // Section 4: Query All ECID
        var s4 = CreateSection("4. Query All ECID");
        var s4Body = CreateSectionBody(s4);
        AddActionTo(s4Body, "S2,F29 Equipment Constant Namelist Request", QueryEcidAsync, 270);
        s4Body.Controls.Add(CountRow("ECID Total Count =", _lblEcidCount));

        leftPanel.Controls.Add(s1, 0, 0);
        leftPanel.Controls.Add(s2, 0, 1);
        leftPanel.Controls.Add(s3, 0, 2);
        leftPanel.Controls.Add(s4, 0, 3);
        grid.Controls.Add(leftPanel, 0, 0);

        // ── Right: Section 5 + Section 6 ───────────────────────────────────
        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(0)
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 62));

        // Section 5: Query Single ECID
        var s5 = CreateSection("5.Query Single ECID");
        var s5Body = CreateSectionBody(s5);
        s5Body.Controls.Add(CboRow("ECID List :", _cboEcidListQuery));
        s5Body.Controls.Add(SelectedEcidRow(_lblSelectedEcidQuery, _cboEcidTypeQuery));
        s5Body.Controls.Add(NoteLabel("NOTE: Please double confirm ECID data format"));
        AddActionTo(s5Body, "S2,F13 Equipment Contant Request", QuerySingleEcidAsync, 280);

        // Section 6: Set Single ECID
        var s6 = CreateSection("6.Set Single ECID");
        var s6Body = CreateSectionBody(s6);
        s6Body.Controls.Add(CboRow("ECID List :", _cboEcidListSet));
        s6Body.Controls.Add(SelectedEcidRow(_lblSelectedEcidSet, _cboEcidTypeSet));
        s6Body.Controls.Add(EcvRow());
        s6Body.Controls.Add(NoteLabel("NOTE: Please double confirm ECID & ECV data format"));
        AddActionTo(s6Body, "S2,F15 New Equipment Contant Send", SetSingleEcidAsync, 280);
        AddActionTo(s6Body, "S2,F13 Equipment Contant Request",  ReadBackEcidAsync,  280);
        s6Body.Controls.Add(CountRow("ECID Value =", _lblEcValue));

        rightPanel.Controls.Add(s5, 0, 0);
        rightPanel.Controls.Add(s6, 0, 1);
        grid.Controls.Add(rightPanel, 1, 0);
    }

    // ── Query actions ────────────────────────────────────────────────────────
    private async Task QueryDvidAsync()
    {
        var reply = await Connection.SendAsync("L2Vid_S1F21_DVIDNameList", 1, 21,
            Secs.SecsMessageFactory.S1F21DataVariableNameListRequest()).ConfigureAwait(true);
        var count = reply?.SecsItem?.Count ?? 0;
        _lblDvidCount.Text = count.ToString();
        AppendResult($"> L2Vid_S1F21_DVIDNameList  DVVAL Total Count = {count}");
        AppendResult($"< Raw: {reply?.SecsItem}");
    }

    private async Task QueryCeidAsync()
    {
        var reply = await Connection.SendAsync("L2Vid_S1F23_CEIDNameList", 1, 23,
            Secs.SecsMessageFactory.S1F23CollectionEventNameListRequest()).ConfigureAwait(true);
        var count = reply?.SecsItem?.Count ?? 0;
        _lblCeidCount.Text = count.ToString();
        AppendResult($"> L2Vid_S1F23_CEIDNameList  CEID Total Count = {count}");
        AppendResult($"< Raw: {reply?.SecsItem}");
    }

    private async Task QuerySvidAsync()
    {
        var reply = await Connection.SendAsync("L2Vid_S1F11_SVIDNameList", 1, 11,
            Secs.SecsMessageFactory.S1F11StatusVariableNameListRequest()).ConfigureAwait(true);
        var count = reply?.SecsItem?.Count ?? 0;
        _lblSvidCount.Text = count.ToString();
        AppendResult($"> L2Vid_S1F11_SVIDNameList  SVID Total Count = {count}");
        AppendResult($"< Raw: {reply?.SecsItem}");
    }

    private async Task QueryEcidAsync()
    {
        var reply = await Connection.SendAsync("L2Vid_S2F29_ECNameList", 2, 29,
            Secs.SecsMessageFactory.S2F29EquipmentConstantNameListRequest()).ConfigureAwait(true);
        var root = reply?.SecsItem;
        int count = root?.Count ?? 0;
        _lblEcidCount.Text = count.ToString();
        AppendResult($"> L2Vid_S2F29_ECNameList  ECID Total Count = {count}");
        AppendResult($"< Raw: {root}");

        var ecids = new List<string>();
        if (root != null)
        {
            for (int i = 0; i < root.Count; i++)
            {
                var ecEntry = root[i]; // S2F30: L[ECID, ECName, ECUnits]
                if (ecEntry.Count >= 1)
                    ecids.Add(ExtractNumericValue(ecEntry[0]));
            }
        }
        PopulateCombo(_cboEcidListQuery, ecids);
        PopulateCombo(_cboEcidListSet,   ecids);
    }

    private async Task QuerySingleEcidAsync()
    {
        var ecid = ParseEcidByte(_cboEcidListQuery);
        var reply = await Connection.SendAsync("L2Vid_S2F13_QueryEC", 2, 13,
            Secs.SecsMessageFactory.S2F13EquipmentConstantRequest([ecid])).ConfigureAwait(true);
        AppendResult($"> L2Vid_S2F13_QueryEC  ECID={ecid}");
        AppendResult($"< Raw: {reply?.SecsItem}");
    }

    private async Task SetSingleEcidAsync()
    {
        var ecid = ParseEcidByte(_cboEcidListSet);
        if (!byte.TryParse(_txtEcvValue.Text.Trim(), out var ecv))
            throw new InvalidOperationException($"ECV value '{_txtEcvValue.Text}' is not a valid U1 (0–255).");
        var reply = await Connection.SendAsync("L2Vid_S2F15_SetEC_U1", 2, 15,
            Secs.SecsMessageFactory.S2F15EquipmentConstantSendU1(ecid, ecv)).ConfigureAwait(true);
        AppendResult($"> L2Vid_S2F15_SetEC_U1  ECID={ecid} ECV={ecv}");
        AppendResult($"< Raw: {reply?.SecsItem}");
    }

    private async Task ReadBackEcidAsync()
    {
        var ecid = ParseEcidByte(_cboEcidListSet);
        var reply = await Connection.SendAsync("L2Vid_S2F13_ReadBackEC", 2, 13,
            Secs.SecsMessageFactory.S2F13EquipmentConstantRequest([ecid])).ConfigureAwait(true);
        var root = reply?.SecsItem;
        _lblEcValue.Text = root is { Count: > 0 } ? ExtractNumericValue(root[0]) : "?";
        AppendResult($"> L2Vid_S2F13_ReadBackEC  ECID={ecid}");
        AppendResult($"< Raw: {root}");
    }

    // ── Parsing helpers ──────────────────────────────────────────────────────
    private static byte ParseEcidByte(ComboBox combo)
    {
        var text = combo.Text.Trim();
        if (byte.TryParse(text, out var v)) return v;
        var m = Regex.Match(text, @"^\d+");
        if (m.Success && byte.TryParse(m.Value, out v)) return v;
        return 0;
    }

    /// <summary>Extract numeric value from a non-List Secs4Net Item using its SecsFormat.</summary>
    private static string ExtractNumericValue(Item item)
    {
        return item.Format switch
        {
            SecsFormat.U1 => item.FirstValueOrDefault<byte>().ToString(),
            SecsFormat.U2 => item.FirstValueOrDefault<ushort>().ToString(),
            SecsFormat.U4 => item.FirstValueOrDefault<uint>().ToString(),
            SecsFormat.U8 => item.FirstValueOrDefault<ulong>().ToString(),
            SecsFormat.I1 => item.FirstValueOrDefault<sbyte>().ToString(),
            SecsFormat.I2 => item.FirstValueOrDefault<short>().ToString(),
            SecsFormat.I4 => item.FirstValueOrDefault<int>().ToString(),
            SecsFormat.I8 => item.FirstValueOrDefault<long>().ToString(),
            SecsFormat.F4 => item.FirstValueOrDefault<float>().ToString(),
            SecsFormat.F8 => item.FirstValueOrDefault<double>().ToString(),
            SecsFormat.ASCII => item.GetString(),
            _ => item.ToString()
        };
    }

    private static void PopulateCombo(ComboBox combo, IList<string> items)
    {
        var current = combo.Text;
        combo.Items.Clear();
        foreach (var item in items) combo.Items.Add(item);
        combo.Text = items.Contains(current) ? current : (items.Count > 0 ? items[0] : string.Empty);
    }

    // ── UI factory helpers ───────────────────────────────────────────────────
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
        var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(6, 2, 6, 2) };
        row.Controls.Add(new Label { Text = caption, Width = 110, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 3, 4, 0) });
        row.Controls.Add(display);
        return row;
    }

    private static FlowLayoutPanel CboRow(string caption, ComboBox combo)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(6, 4, 6, 2) };
        row.Controls.Add(new Label { Text = caption, Width = 72, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft });
        row.Controls.Add(combo);
        return row;
    }

    private static FlowLayoutPanel SelectedEcidRow(Label display, ComboBox typeCombo)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(6, 2, 6, 2) };
        row.Controls.Add(new Label { Text = "Selected ECID =", Width = 98, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft });
        row.Controls.Add(display);
        row.Controls.Add(new Label { Text = "ECID Type =", Width = 72, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(6, 3, 2, 0) });
        row.Controls.Add(typeCombo);
        return row;
    }

    private FlowLayoutPanel EcvRow()
    {
        var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(6, 2, 6, 2) };
        row.Controls.Add(new Label { Text = "Set EC Value =", Width = 90, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft });
        row.Controls.Add(_txtEcvValue);
        row.Controls.Add(new Label { Text = "ECV Type =", Width = 68, ForeColor = Theme.ThemeHelper.TextMid, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(6, 3, 2, 0) });
        row.Controls.Add(_cboEcvType);
        return row;
    }

    private static Label InfoLabel(string text) => new()
    {
        Text = text, AutoSize = false, Width = 400, Height = 18,
        ForeColor = Theme.ThemeHelper.TextMid,
        Margin = new Padding(6, 1, 6, 0)
    };

    private static Label NoteLabel(string text) => new()
    {
        Text = text, AutoSize = false, Width = 400, Height = 18,
        ForeColor = Color.FromArgb(0, 102, 204),
        Font = new Font("Microsoft JhengHei UI", 8.5F, FontStyle.Underline),
        Margin = new Padding(6, 3, 6, 3)
    };
}

