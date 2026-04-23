namespace HostSimTester.App.Pages;

public sealed class L2TraceDataCollectionPage : BaseTestPage
{
    private readonly NumericUpDown _nudTraceId;
    private readonly TextBox _txtDsper;
    private readonly NumericUpDown _nudTotSmp;
    private readonly TextBox _txtSvids;

    public L2TraceDataCollectionPage(Secs.SecsConnection connection)
        : base("L2 Trace Data Collection", Logging.LoggerNames.L2Trace, connection)
    {
        _nudTraceId = new NumericUpDown { Minimum = 1, Maximum = 255, Value = 1, Width = 60 };
        _txtDsper = new TextBox { Text = "000001", Width = 80 };
        _nudTotSmp = new NumericUpDown { Minimum = 1, Maximum = 100000, Value = 600, Width = 80 };
        _txtSvids = new TextBox { Text = "1,2,3,4,5", Width = 180 };

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
            bar.Controls.Add(new Label { Text = "TRID", Width = 38, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_nudTraceId);
            bar.Controls.Add(new Label { Text = "DSPER", Width = 48, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_txtDsper);
            bar.Controls.Add(new Label { Text = "TOTSMP", Width = 54, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_nudTotSmp);
            bar.Controls.Add(new Label { Text = "SVIDs", Width = 42, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            bar.Controls.Add(_txtSvids);
            parent.Controls.Add(bar);
            bar.BringToFront();
            return bar;
        }

        // ── Tab 1: Trace Control ─────────────────────────────────────────────
        var tabCtrl = CreateTab(tabControl, "Trace Control");
        MakeCfgBar(tabCtrl);
        var gridCtrl = CreateTwoColumnGrid();
        tabCtrl.Controls.Add(gridCtrl);

        var startTraceSection = CreateSection("1. Start Trace Data Collection");
        var startTraceBody = CreateSectionBody(startTraceSection);
        startTraceBody.Controls.Add(new Label { Text = "S2F23 Define Trace Set 1 → wait S6F1", Width = 360, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(startTraceBody, "S2F23 Define Trace",
            () => SendAsync("L2Trace_S2F23_DefineTrace", 2, 23,
                Secs.SecsMessageFactory.S2F23TraceInitialize(
                    (byte)_nudTraceId.Value, _txtDsper.Text.Trim(), (uint)_nudTotSmp.Value, 1,
                    ParseSvids(_txtSvids.Text))), 260);
        AddActionTo(startTraceBody, "S2F23 Define Trace (Single SVID Only)",
            () => SendAsync("L2Trace_S2F23_DefineTraceSet2", 2, 23,
                Secs.SecsMessageFactory.S2F23TraceInitialize(
                    (byte)_nudTraceId.Value, _txtDsper.Text.Trim(), (uint)_nudTotSmp.Value, 1,
                    ParseSvids(_txtSvids.Text).Take(1))), 260);
        AddActionTo(startTraceBody, "Wait S6F1 (Trace Data Report)",
            () => WaitPrimaryAsync("L2Trace_Wait_S6F1_Start", 6, 1, 30), 260);

        var stopTraceSection = CreateSection("2. Stop Trace Data Collection");
        var stopTraceBody = CreateSectionBody(stopTraceSection);
        stopTraceBody.Controls.Add(new Label { Text = "S2F23 Define Trace Set 2 → wait → STOP Trace", Width = 360, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(stopTraceBody, "S2F23 Stop Trace",
            () => SendAsync("L2Trace_S2F23_StopTrace", 2, 23,
                Secs.SecsMessageFactory.S2F23TraceInitialize(
                    (byte)_nudTraceId.Value, _txtDsper.Text.Trim(), 0, 1,
                    ParseSvids(_txtSvids.Text).Take(2))), 260);
        AddActionTo(stopTraceBody, "Wait S6F1 (After Stop)",
            () => WaitPrimaryAsync("L2Trace_Wait_S6F1_Stop", 6, 1, 30), 260);

        gridCtrl.Controls.Add(startTraceSection, 0, 0);
        gridCtrl.Controls.Add(stopTraceSection, 1, 0);

        // ── Tab 2: Trace Query ───────────────────────────────────────────────
        var tabQuery = CreateTab(tabControl, "Trace Data by S1F3/S1F4");
        MakeCfgBar(tabQuery);
        var gridQuery = CreateTwoColumnGrid();
        tabQuery.Controls.Add(gridQuery);

        var querySet1Section = CreateSection("3. Trace Data Set 1");
        var querySet1Body = CreateSectionBody(querySet1Section);
        querySet1Body.Controls.Add(new Label { Text = "S1F3 SVIDs 1-20 (Trace Data Set 1)", Width = 360, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(querySet1Body, "S1F3 Query Trace Data Set1",
            () => SendAsync("L2Trace_S1F3_QueryTraceDataSet1", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(Enumerable.Range(1, 20).Select(i => (uint)i))), 260);
        AddActionTo(querySet1Body, "Wait S6F1 (Trace Data Report Set1)",
            () => WaitPrimaryAsync("L2Trace_Wait_S6F1_Set1", 6, 1, 30), 260);

        var querySet2Section = CreateSection("4. Trace Data Set 2");
        var querySet2Body = CreateSectionBody(querySet2Section);
        querySet2Body.Controls.Add(new Label { Text = "S1F3 SVIDs 100092-100265 (Trace Data Set 2)", Width = 360, ForeColor = Theme.ThemeHelper.TextMid, Margin = new Padding(6, 2, 6, 0) });
        AddActionTo(querySet2Body, "S1F3 Query Trace Data Set2",
            () => SendAsync("L2Trace_S1F3_QueryTraceDataSet2", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest([100092u, 100093u, 100094u, 100262u, 100264u, 100263u, 100265u])), 260);
        AddActionTo(querySet2Body, "Wait S6F1 (Trace Data Report Set2)",
            () => WaitPrimaryAsync("L2Trace_Wait_S6F1_Set2", 6, 1, 30), 260);

        gridQuery.Controls.Add(querySet1Section, 0, 0);
        gridQuery.Controls.Add(querySet2Section, 1, 0);
    }

    private static IReadOnlyList<uint> ParseSvids(string raw)
    {
        var list = new List<uint>();
        foreach (var part in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (uint.TryParse(part, out var svid))
            {
                list.Add(svid);
            }
        }

        return list.Count == 0 ? [1u] : list;
    }
}

