namespace HostSimTester.App.Pages;

public sealed class L2TraceDataCollectionPage : BaseTestPage
{
    // ── 對齊舊工具：S6F1 計數 / 取樣週期 (per TRID) ────────────────────────
    private readonly object _traceLock = new();
    private readonly Dictionary<uint, int> _traceCount = new();
    private readonly Dictionary<uint, int> _maxSamplePeriodMs = new();
    private readonly Dictionary<uint, DateTime> _lastSampleTimeUtc = new();

    // Section 1/2/3 顯示用控制項
    private TextBox? _box13Count, _box14Period;
    private TextBox? _box24Count, _box25Period;
    private TextBox? _box34Count, _box35Period;
    private TextBox? _box37Count, _box38Period;

    public L2TraceDataCollectionPage(Secs.SecsConnection connection)
        : base("L2 Trace Data Collection", Logging.LoggerNames.L2Trace, connection)
    {
        ConfigureActionPanel(640, wrapContents: false);
        ClearActionItems();
        ActionPanel.AutoScroll = false;

        Connection.PrimaryMessageReceived += OnPrimaryMessageReceived;
        Disposed += (_, _) => Connection.PrimaryMessageReceived -= OnPrimaryMessageReceived;

        // ── 預設值 (可由對話視窗覆蓋)──────────────────────────────────────────
        const byte traceId1 = 1;
        const byte traceId2 = 2;
        string dsper1 = "000001";
        string dsper2 = "000001";
        uint totSm1 = 600;
        uint totSm2 = 600;
        uint[] svidsSet1 = [1u, 2u, 3u, 4u, 5u];
        uint[] svidsSet2 = [100092u, 100093u, 100094u, 100262u, 100264u, 100263u, 100265u];

        // S1F3 Section 4 dialog 結果（按下 4.1 之後填入）
        uint s1f3TotalCount = 600;
        uint[] s1f3Set1 = svidsSet1;
        uint[] s1f3Set2 = svidsSet2;

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

        // ── 3-column outer grid ────────────────────────────────────────────────
        var outerGrid = new TableLayoutPanel
        {
            Width = 1130, Height = 580,
            ColumnCount = 3, RowCount = 1,
            Padding = new Padding(0), Margin = new Padding(0)
        };
        outerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        outerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        outerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));
        outerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        ActionPanel.Controls.Add(outerGrid);

        // ── left inner grid (sections 1 + 2 stacked) ─────────────────────────
        var leftGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 2,
            Padding = new Padding(0), Margin = new Padding(0)
        };
        leftGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        outerGrid.Controls.Add(leftGrid, 0, 0);

        // ── local helpers ──────────────────────────────────────────────────────

        // lamp + plain label (wait / report step)
        void AddReportRow(Control host, string text, Func<Task> action)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 28, Margin = new Padding(6, 2, 6, 2),
                Padding = new Padding(0, 1, 0, 0),
                BackColor = Theme.ThemeHelper.TableBg, WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16, Height = 16, Margin = new Padding(3, 5, 6, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            var lbl = new Label
            {
                Text = text, AutoSize = true,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0), Cursor = Cursors.Hand
            };
            lbl.Padding = new Padding(2, 0, 2, 0);
            lbl.BorderStyle = BorderStyle.FixedSingle;
            async Task Run()
            {
                lamp.BackColor = Theme.ThemeHelper.LogWarn;
                try { await action().ConfigureAwait(true); lamp.BackColor = Color.FromArgb(78, 180, 95); }
                catch (Exception ex) { lamp.BackColor = Theme.ThemeHelper.DangerRed; AppendResult($"[ERROR] {ex.Message}"); }
            }
            lbl.Click += async (_, _) => await Run();
            row.Click  += async (_, _) => await Run();
            row.Controls.Add(lamp);
            row.Controls.Add(lbl);
            host.Controls.Add(row);
        }

        // multi-line action button (S2F23 sends)
        void AddS2F23Row(Control host, string line1, string line2, Func<Task> action, bool muted = false)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 52, Margin = new Padding(6, 4, 6, 2),
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Theme.ThemeHelper.IceSurface, WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16, Height = 16, Margin = new Padding(3, 15, 6, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            var btn = new Button
            {
                Text = $"{line1}\n{line2}",
                Height = 44, Width = 290, AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 6, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = muted ? Color.FromArgb(210, 218, 228) : Theme.ThemeHelper.CobaltBlue,
                ForeColor = muted ? Theme.ThemeHelper.TextDark : Color.White,
                Font = new Font("Microsoft JhengHei UI", 8F)
            };
            btn.FlatAppearance.BorderColor = muted ? Color.FromArgb(180, 190, 205) : Theme.ThemeHelper.DeepBlue;
            btn.Click += async (_, _) =>
            {
                lamp.BackColor = Theme.ThemeHelper.LogWarn;
                try { await action().ConfigureAwait(true); lamp.BackColor = Color.FromArgb(78, 180, 95); }
                catch (Exception ex) { lamp.BackColor = Theme.ThemeHelper.DangerRed; AppendResult($"[ERROR] {ex.Message}"); }
            };
            row.Controls.Add(lamp);
            row.Controls.Add(btn);
            host.Controls.Add(row);
        }

        // lamp + label + (display) button
        void AddDisplayRow(Control host, string text, Func<Task> displayAction, string? hint = null)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 30, Margin = new Padding(6, 2, 6, 0),
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Theme.ThemeHelper.IceSurface, WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16, Height = 16, Margin = new Padding(3, 5, 6, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            var lbl = new Label
            {
                Text = text, Width = 180,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 4, 0)
            };
            var displayBtn = new Button
            {
                Text = "(display)", Height = 24, AutoSize = true,
                Padding = new Padding(6, 0, 6, 0),
                Margin = new Padding(0, 1, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 180, 200),
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 8F)
            };
            displayBtn.FlatAppearance.BorderColor = Color.FromArgb(0, 150, 170);
            displayBtn.Click += async (_, _) =>
            {
                lamp.BackColor = Theme.ThemeHelper.LogWarn;
                try { await displayAction().ConfigureAwait(true); lamp.BackColor = Color.FromArgb(78, 180, 95); }
                catch (Exception ex) { lamp.BackColor = Theme.ThemeHelper.DangerRed; AppendResult($"[ERROR] {ex.Message}"); }
            };
            row.Controls.Add(lamp);
            row.Controls.Add(lbl);
            row.Controls.Add(displayBtn);
            host.Controls.Add(row);
            if (hint is not null)
            {
                host.Controls.Add(new Label
                {
                    Text = hint, AutoSize = false, Width = 310, Height = 14,
                    ForeColor = Color.Gray,
                    Font = new Font("Microsoft JhengHei UI", 7.5F, FontStyle.Italic),
                    TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, 0, 8, 2)
                });
            }
        }

        // S1F3 + (display) side button (for section 4 action rows)
        void AddS1F3DisplayRow(Control host, string text, Func<Task> sendAction, Func<Task> displayAction, bool muted = false)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 34, Margin = new Padding(6, 2, 6, 2),
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Theme.ThemeHelper.IceSurface, WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16, Height = 16, Margin = new Padding(3, 8, 6, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            var mainBtn = new Button
            {
                Text = text, Height = 28, Width = 210, AutoSize = false,
                Padding = new Padding(6, 0, 6, 0), Margin = new Padding(0, 0, 4, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = muted ? Color.FromArgb(210, 218, 228) : Theme.ThemeHelper.CobaltBlue,
                ForeColor = muted ? Theme.ThemeHelper.TextDark : Color.White,
                Font = new Font("Microsoft JhengHei UI", 8F)
            };
            mainBtn.FlatAppearance.BorderColor = muted ? Color.FromArgb(180, 190, 205) : Theme.ThemeHelper.DeepBlue;
            var displayBtn = new Button
            {
                Text = "(display)", Height = 28, AutoSize = true,
                Padding = new Padding(6, 0, 6, 0), Margin = new Padding(0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 180, 200),
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 8F)
            };
            displayBtn.FlatAppearance.BorderColor = Color.FromArgb(0, 150, 170);
            mainBtn.Click += async (_, _) =>
            {
                lamp.BackColor = Theme.ThemeHelper.LogWarn;
                try { await sendAction().ConfigureAwait(true); lamp.BackColor = Color.FromArgb(78, 180, 95); }
                catch (Exception ex) { lamp.BackColor = Theme.ThemeHelper.DangerRed; AppendResult($"[ERROR] {ex.Message}"); }
            };
            displayBtn.Click += async (_, _) =>
            {
                lamp.BackColor = Theme.ThemeHelper.LogWarn;
                try { await displayAction().ConfigureAwait(true); lamp.BackColor = Color.FromArgb(78, 180, 95); }
                catch (Exception ex) { lamp.BackColor = Theme.ThemeHelper.DangerRed; AppendResult($"[ERROR] {ex.Message}"); }
            };
            row.Controls.Add(lamp);
            row.Controls.Add(mainBtn);
            row.Controls.Add(displayBtn);
            host.Controls.Add(row);
        }

        // multi-line action button + (display) side button (for 4.7)
        void AddS2F23WithDisplayRow(Control host, string line1, string line2, Func<Task> sendAction, Func<Task> displayAction, bool muted = true)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 52, Margin = new Padding(6, 4, 6, 2),
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Theme.ThemeHelper.IceSurface, WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16, Height = 16, Margin = new Padding(3, 15, 6, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            var mainBtn = new Button
            {
                Text = $"{line1}\n{line2}",
                Height = 44, Width = 210, AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 6, 0), Margin = new Padding(0, 0, 4, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = muted ? Color.FromArgb(210, 218, 228) : Theme.ThemeHelper.CobaltBlue,
                ForeColor = muted ? Theme.ThemeHelper.TextDark : Color.White,
                Font = new Font("Microsoft JhengHei UI", 8F)
            };
            mainBtn.FlatAppearance.BorderColor = muted ? Color.FromArgb(180, 190, 205) : Theme.ThemeHelper.DeepBlue;
            var displayBtn = new Button
            {
                Text = "(display)", Height = 28, AutoSize = true,
                Padding = new Padding(6, 0, 6, 0), Margin = new Padding(0, 8, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 180, 200),
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 8F)
            };
            displayBtn.FlatAppearance.BorderColor = Color.FromArgb(0, 150, 170);
            mainBtn.Click += async (_, _) =>
            {
                lamp.BackColor = Theme.ThemeHelper.LogWarn;
                try { await sendAction().ConfigureAwait(true); lamp.BackColor = Color.FromArgb(78, 180, 95); }
                catch (Exception ex) { lamp.BackColor = Theme.ThemeHelper.DangerRed; AppendResult($"[ERROR] {ex.Message}"); }
            };
            displayBtn.Click += async (_, _) =>
            {
                lamp.BackColor = Theme.ThemeHelper.LogWarn;
                try { await displayAction().ConfigureAwait(true); lamp.BackColor = Color.FromArgb(78, 180, 95); }
                catch (Exception ex) { lamp.BackColor = Theme.ThemeHelper.DangerRed; AppendResult($"[ERROR] {ex.Message}"); }
            };
            row.Controls.Add(lamp);
            row.Controls.Add(mainBtn);
            row.Controls.Add(displayBtn);
            host.Controls.Add(row);
        }

        // 被動：燈 + 多行文字（無按鈕、無點擊動作），可由外部同步點亮（回傳 lamp）。
        Panel AddPassiveLabelRow(Control host, string text)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 38, Margin = new Padding(6, 4, 6, 2),
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Theme.ThemeHelper.TableBg, WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16, Height = 16, Margin = new Padding(3, 10, 6, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(160, 170, 180)
            };
            var lbl = new Label
            {
                Text = text, AutoSize = false, Width = 290, Height = 32,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0)
            };
            row.Controls.Add(lamp);
            row.Controls.Add(lbl);
            host.Controls.Add(row);
            return lamp;
        }

        // 被動：紅燈 + 標籤 + 唯讀值欄位（無 (display) 按鈕），可選 hint (例如 "Max.")。
        TextBox AddValueDisplayRow(Control host, string text, string? hint = null)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 30, Margin = new Padding(6, 2, 6, 0),
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Theme.ThemeHelper.TableBg, WrapContents = false
            };
            var lamp = new Panel
            {
                Width = 16, Height = 16, Margin = new Padding(3, 5, 6, 0),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray
            };
            var lbl = new Label
            {
                Text = text, Width = 170,
                ForeColor = Theme.ThemeHelper.TextDark,
                Font = new Font("Microsoft JhengHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 4, 0)
            };
            var valueBox = new TextBox
            {
                ReadOnly = true,
                Width = 80, Height = 22,
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(196, 230, 245),
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 2, 4, 0),
                Text = string.Empty
            };
            row.Controls.Add(lamp);
            row.Controls.Add(lbl);
            row.Controls.Add(valueBox);
            valueBox.Tag = lamp;
            if (hint is not null)
            {
                row.Controls.Add(new Label
                {
                    Text = hint, AutoSize = true,
                    ForeColor = Color.Gray,
                    Font = new Font("Microsoft JhengHei UI", 7.5F, FontStyle.Italic),
                    Margin = new Padding(0, 8, 0, 0)
                });
            }
            host.Controls.Add(row);
            return valueBox;
        }


        // ── Section 1: Start Trace Data Collection ────────────────────────────
        var sec1 = CreateSection("1. Start Trace Data Collection");
        var body1 = CreateSectionBody(sec1);
        AddStepLegend(body1, 320);

        AddS2F23Row(body1,
            "(1.1) Send S2F23: Define Trace Set 1",
            "(TraceID=1 & SVIDs Assigned to Set 1)",
            async () =>
            {
                using var dlg = new Dialogs.S2F23DefineTraceDialog(traceId1, dsper1, totSm1, svidsSet1);
                if (dlg.ShowDialog(this) != DialogResult.OK) throw new OperationCanceledException("使用者取消 Define Trace Set 1");
                dsper1 = dlg.Dsper;
                totSm1 = dlg.TotalSamples;
                svidsSet1 = dlg.Svids.ToArray();
                ResetTraceStats(traceId1);
                await SendAsync("L2Trace_1_1_DefineSet1", 2, 23,
                    Secs.SecsMessageFactory.S2F23TraceInitialize(traceId1, dsper1, totSm1, 1, svidsSet1)).ConfigureAwait(true);
            });

        AddReportRow(body1, "(1.2) Trace Data Reported (S6F1)",
            () => WaitPrimaryAsync("L2Trace_1_2_S6F1", 6, 1, 60));

        _box13Count = AddValueDisplayRow(body1, "(1.3) Trace Count");
        _box14Period = AddValueDisplayRow(body1, "(1.4) Sample Period(Max)", hint: "Max.");

        leftGrid.Controls.Add(sec1, 0, 0);

        // ── Section 2: Stop Trace Data Collection ─────────────────────────────
        var sec2 = CreateSection("2. Stop Trace Data Collection");
        var body2 = CreateSectionBody(sec2);
        AddStepLegend(body2, 320);

        AddS2F23Row(body2,
            "(2.1) Send S2F23: Define Trace Set 2",
            "(TraceID=2 & SVIDs Assigned to Set 2)",
            async () =>
            {
                using var dlg = new Dialogs.S2F23DefineTraceDialog(traceId2, dsper2, totSm2, svidsSet2);
                if (dlg.ShowDialog(this) != DialogResult.OK) throw new OperationCanceledException("使用者取消 Define Trace Set 2");
                dsper2 = dlg.Dsper;
                totSm2 = dlg.TotalSamples;
                svidsSet2 = dlg.Svids.ToArray();
                ResetTraceStats(traceId2);
                await SendAsync("L2Trace_2_1_DefineSet2", 2, 23,
                    Secs.SecsMessageFactory.S2F23TraceInitialize(traceId2, dsper2, totSm2, 1, svidsSet2)).ConfigureAwait(true);
            });

        AddReportRow(body2, "(2.2) Trace Data Reported (S6F1)",
            () => WaitPrimaryAsync("L2Trace_2_2_S6F1", 6, 1, 60));

        AddS2F23Row(body2,
            "(2.3) Send S2F23: STOP Trace Set 2",
            "(TraceID=2 & SVIDs Assigned to Set 2)",
            () => SendAsync("L2Trace_2_3_StopSet2", 2, 23,
                Secs.SecsMessageFactory.S2F23TraceInitialize(traceId2, dsper2, 0, 1, svidsSet2)));

        _box24Count = AddValueDisplayRow(body2, "(2.4) Trace Count");
        _box25Period = AddValueDisplayRow(body2, "(2.5) Sample Period(Max)", hint: "Max.");

        leftGrid.Controls.Add(sec2, 0, 1);

        // ── Section 3: Concurrent Trace Data Collection ───────────────────────
        var sec3 = CreateSection("3. Concurrent Trace Data Collection");
        var body3 = CreateSectionBody(sec3);
        AddStepLegend(body3, 320);

        // 3.2 燈號（在 3.1 點擊時同步點亮）— 先建立佔位變數，於 3.1 之後加入
        Panel? lamp32 = null;

        AddS2F23Row(body3,
            "(3.1) Send S2F23: Trace Data Set 1",
            "(TraceID=1 & SVIDs Defined in 1.1)",
            async () =>
            {
                ResetTraceStats(traceId1);
                ResetTraceStats(traceId2);
                await SendAsync("L2Trace_3_1_ConSet1", 2, 23,
                    Secs.SecsMessageFactory.S2F23TraceInitialize(traceId1, dsper1, totSm1, 1, svidsSet1)).ConfigureAwait(true);
                // Concurrent：同步送出 Set 2，並點亮 3.2 燈
                if (lamp32 is not null) lamp32.BackColor = Theme.ThemeHelper.LogWarn;
                try
                {
                    await SendAsync("L2Trace_3_2_ConSet2", 2, 23,
                        Secs.SecsMessageFactory.S2F23TraceInitialize(traceId2, dsper2, totSm2, 1, svidsSet2)).ConfigureAwait(true);
                    if (lamp32 is not null) lamp32.BackColor = Color.FromArgb(78, 180, 95);
                }
                catch (Exception ex)
                {
                    if (lamp32 is not null) lamp32.BackColor = Theme.ThemeHelper.DangerRed;
                    AppendResult($"[ERROR] {ex.Message}");
                    throw;
                }
            },
            muted: true);

        // 3.2：被動列（不可點擊；由 3.1 的處理流程點亮）
        lamp32 = AddPassiveLabelRow(body3,
            "(3.2) Send S2F23: Trace Data Set 2\n(TraceID=2 & SVIDs Defined in 2.1)");

        AddReportRow(body3, "(3.3) Trace Data Reported (S6F1 ID=1)",
            () => WaitPrimaryAsync("L2Trace_3_3_S6F1_ID1", 6, 1, 60));

        _box34Count = AddValueDisplayRow(body3, "(3.4) Trace Count (ID=1)");
        _box35Period = AddValueDisplayRow(body3, "(3.5) Sample Period (ID=1)", hint: "Max.");

        AddReportRow(body3, "(3.6) Trace Data Reported (S6F1 ID=2)",
            () => WaitPrimaryAsync("L2Trace_3_6_S6F1_ID2", 6, 1, 60));

        _box37Count = AddValueDisplayRow(body3, "(3.7) Trace Count (ID=2)");
        _box38Period = AddValueDisplayRow(body3, "(3.8) Sample Period (ID=2)", hint: "Max.");

        outerGrid.Controls.Add(sec3, 1, 0);

        // ── Section 4: Trace Data by S1,F3 / S1,F4 ───────────────────────────
        var sec4 = CreateSection("4. Trace Data by S1,F3 / S1,F4");
        var body4 = CreateSectionBody(sec4);
        AddStepLegend(body4, 320);

        AddS1F3DisplayRow(body4, "(4.1) Send S1F3 (SVID Set 1)",
            async () =>
            {
                using var dlg = new Dialogs.S1F3SvidSetsDialog(s1f3TotalCount, s1f3Set1, s1f3Set2);
                if (dlg.ShowDialog(this) != DialogResult.OK) throw new OperationCanceledException("使用者取消 S1F3 SVID Sets 設定");
                s1f3TotalCount = dlg.TotalCount;
                s1f3Set1 = dlg.Set1Svids.ToArray();
                s1f3Set2 = dlg.Set2Svids.ToArray();
                await SendAsync("L2Trace_4_1_S1F3_Set1", 1, 3,
                    Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set1)).ConfigureAwait(true);
            },
            () => SendAsync("L2Trace_4_1_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set1)),
            muted: true);

        AddDisplayRow(body4, "(4.2) Trace Count (Set 1)",
            () => SendAsync("L2Trace_4_2_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set1)));

        AddDisplayRow(body4, "(4.3) Sample Period(Max)(Set1)",
            () => SendAsync("L2Trace_4_3_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set1)));

        AddS1F3DisplayRow(body4, "(4.4) Send S1F3 (SVID Set 2)",
            () => SendAsync("L2Trace_4_4_S1F3_Set2", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set2)),
            () => SendAsync("L2Trace_4_4_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set2)),
            muted: true);

        AddDisplayRow(body4, "(4.5) Trace Count(Set 2)",
            () => SendAsync("L2Trace_4_5_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set2)));

        AddDisplayRow(body4, "(4.6) Sample Period(Max)(Set2)",
            () => SendAsync("L2Trace_4_6_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set2)));

        AddS2F23WithDisplayRow(body4,
            "(4.7) Start To Concurrent Test:",
            "S1F3 (SVID Set 1) & S1F3 (SVID Set 2)",
            async () =>
            {
                await SendAsync("L2Trace_4_7_Con_Set1", 1, 3,
                    Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set1)).ConfigureAwait(true);
                await SendAsync("L2Trace_4_7_Con_Set2", 1, 3,
                    Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set2)).ConfigureAwait(true);
            },
            async () =>
            {
                await SendAsync("L2Trace_4_7_Display_Set1", 1, 3,
                    Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set1)).ConfigureAwait(true);
                await SendAsync("L2Trace_4_7_Display_Set2", 1, 3,
                    Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set2)).ConfigureAwait(true);
            },
            muted: true);

        AddDisplayRow(body4, "(4.8) Trace Count (Set 1)",
            () => SendAsync("L2Trace_4_8_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set1)));

        AddDisplayRow(body4, "(4.9) Sample Period(Max)(Set1)",
            () => SendAsync("L2Trace_4_9_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set1)));

        AddDisplayRow(body4, "(4.10) Trace Count(Set 2)",
            () => SendAsync("L2Trace_4_10_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set2)));

        AddDisplayRow(body4, "(4.11) Sample Period(Max)(Set2)",
            () => SendAsync("L2Trace_4_11_Display", 1, 3,
                Secs.SecsMessageFactory.S1F3SelectedEquipmentStatusRequest(s1f3Set2)));

        outerGrid.Controls.Add(sec4, 2, 0);
    }

    private static IReadOnlyList<uint> ParseSvids(string raw)
    {
        var list = new List<uint>();
        foreach (var part in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (uint.TryParse(part, out var svid))
                list.Add(svid);
        }
        return list.Count == 0 ? [1u] : list;
    }

    // ── S6F1 接收：依 TRID 累計次數 / 計算最大取樣間隔 (ms) ────────────────
    private void OnPrimaryMessageReceived(Secs4Net.PrimaryMessageWrapper wrapper)
    {
        var message = wrapper.PrimaryMessage;
        if (message.S != 6 || message.F != 1)
            return;

        if (!TryGetTrid(message.SecsItem, out var trid))
            return;

        var nowUtc = DateTime.UtcNow;
        int count;
        int maxMs;
        lock (_traceLock)
        {
            count = _traceCount.TryGetValue(trid, out var c) ? c + 1 : 1;
            _traceCount[trid] = count;

            if (_lastSampleTimeUtc.TryGetValue(trid, out var prev))
            {
                var deltaMs = (int)Math.Round((nowUtc - prev).TotalMilliseconds);
                if (deltaMs < 0) deltaMs = 0;
                var prevMax = _maxSamplePeriodMs.TryGetValue(trid, out var m) ? m : 0;
                maxMs = Math.Max(prevMax, deltaMs);
            }
            else
            {
                maxMs = _maxSamplePeriodMs.TryGetValue(trid, out var m) ? m : 0;
            }
            _maxSamplePeriodMs[trid] = maxMs;
            _lastSampleTimeUtc[trid] = nowUtc;
        }

        BeginInvoke(() => UpdateTraceBoxes(trid, count, maxMs));
    }

    private void UpdateTraceBoxes(uint trid, int count, int maxMs)
    {
        var countText = count.ToString();
        var periodText = maxMs.ToString();
        if (trid == 1)
        {
            ApplyValue(_box13Count, countText, count);
            ApplyValue(_box14Period, periodText, count);
            ApplyValue(_box34Count, countText, count);
            ApplyValue(_box35Period, periodText, count);
        }
        else if (trid == 2)
        {
            ApplyValue(_box24Count, countText, count);
            ApplyValue(_box25Period, periodText, count);
            ApplyValue(_box37Count, countText, count);
            ApplyValue(_box38Period, periodText, count);
        }
    }

    private static void ApplyValue(TextBox? box, string text, int count)
    {
        if (box is null) return;
        box.Text = text;
        if (box.Tag is Panel lamp)
        {
            lamp.BackColor = count > 0 ? Color.FromArgb(78, 180, 95) : Color.LightGray;
        }
    }

    private void ResetTraceStats(uint trid)
    {
        lock (_traceLock)
        {
            _traceCount[trid] = 0;
            _maxSamplePeriodMs[trid] = 0;
            _lastSampleTimeUtc.Remove(trid);
        }
        BeginInvoke(() => UpdateTraceBoxes(trid, 0, 0));
    }

    // S6F1 第一個元素為 TRID（U1/U2/U4/U8 都支援）；若是 List，取其第一個元素。
    private static bool TryGetTrid(Secs4Net.Item? root, out uint trid)
    {
        trid = 0;
        if (root is null) return false;

        Secs4Net.Item? first = root;
        if (root.Format == Secs4Net.SecsFormat.List)
        {
            if (root.Count == 0) return false;
            first = root[0];
        }
        if (first is null) return false;

        try
        {
            switch (first.Format)
            {
                case Secs4Net.SecsFormat.U1: trid = first.FirstValue<byte>(); return true;
                case Secs4Net.SecsFormat.U2: trid = first.FirstValue<ushort>(); return true;
                case Secs4Net.SecsFormat.U4: trid = first.FirstValue<uint>(); return true;
                case Secs4Net.SecsFormat.U8:
                    var u8 = first.FirstValue<ulong>();
                    if (u8 > uint.MaxValue) return false;
                    trid = (uint)u8;
                    return true;
                case Secs4Net.SecsFormat.I1: trid = (uint)first.FirstValue<sbyte>(); return true;
                case Secs4Net.SecsFormat.I2: trid = (uint)first.FirstValue<short>(); return true;
                case Secs4Net.SecsFormat.I4: trid = (uint)first.FirstValue<int>(); return true;
                default: return false;
            }
        }
        catch
        {
            return false;
        }
    }
}
