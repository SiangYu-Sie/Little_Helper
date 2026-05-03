using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HostSimTester.App.Theme;

namespace HostSimTester.App.Dialogs;

/// <summary>
/// S1F3 SVID Sets &amp; Total Count 設定對話視窗。對應影像03。
/// </summary>
public sealed class S1F3SvidSetsDialog : Form
{
    public uint TotalCount { get; private set; }
    public IReadOnlyList<uint> Set1Svids { get; private set; } = Array.Empty<uint>();
    public IReadOnlyList<uint> Set2Svids { get; private set; } = Array.Empty<uint>();

    private readonly TextBox _txtTotalCount;
    private readonly TextBox _txtSet1;
    private readonly TextBox _txtSet2;

    public S1F3SvidSetsDialog(uint defaultTotal = 600,
        IEnumerable<uint>? defaultSet1 = null,
        IEnumerable<uint>? defaultSet2 = null)
    {
        Text = "Enter SVID Sets & Total Count for S1F3 Messaging";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 540;
        Height = 560;
        ThemeHelper.ApplyTheme(this);

        var labelFont = new Font("Microsoft JhengHei UI", 9F);
        var hintFont = new Font("Microsoft JhengHei UI", 8.5F);
        var noteColor = Color.Gray;

        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(20, 14, 20, 12),
            AutoScroll = true
        };

        root.Controls.Add(new Label { Text = "Tester will send an S1F3 message every second.", AutoSize = true, Font = labelFont });
        root.Controls.Add(new Label { Text = "Enter the required information:", AutoSize = true, Font = labelFont, Margin = new Padding(0, 0, 0, 8) });

        // 1. Total Count
        root.Controls.Add(new Label { Text = "1. Total Count for S1F3 :", AutoSize = true, Font = labelFont });
        var totalRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(16, 2, 0, 8) };
        _txtTotalCount = new TextBox { Text = defaultTotal.ToString(), Width = 140, Font = labelFont };
        totalRow.Controls.Add(_txtTotalCount);
        totalRow.Controls.Add(new Label { Text = "(must >= 600)", AutoSize = true, ForeColor = noteColor, Font = hintFont, Margin = new Padding(8, 6, 0, 0) });
        root.Controls.Add(totalRow);

        // 2. Set 1
        root.Controls.Add(new Label { Text = "2. SVID (Set 1) :", AutoSize = true, Font = labelFont });
        root.Controls.Add(new Label
        {
            Text = "Enter SVIDs, total count must be between 20 and 45 for general conditions",
            AutoSize = true,
            Font = labelFont,
            Margin = new Padding(16, 0, 0, 0)
        });
        root.Controls.Add(new Label
        {
            Text = "Note: Separate each SVID with a comma, e.g., \"12,34,56,78,90,...\"",
            AutoSize = true,
            ForeColor = noteColor,
            Font = hintFont,
            Margin = new Padding(16, 0, 0, 4)
        });
        _txtSet1 = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Width = 460,
            Height = 80,
            Font = labelFont,
            Text = defaultSet1 is null ? string.Empty : string.Join(",", defaultSet1),
            Margin = new Padding(16, 0, 0, 10)
        };
        root.Controls.Add(_txtSet1);

        // 2. Set 2  (圖示中亦標 "2.")
        root.Controls.Add(new Label { Text = "2. SVID (Set 2) :", AutoSize = true, Font = labelFont });
        root.Controls.Add(new Label
        {
            Text = "Enter SVIDs, total count must be between 50 and 100 for stress test conditions",
            AutoSize = true,
            Font = labelFont,
            Margin = new Padding(16, 0, 0, 0)
        });
        root.Controls.Add(new Label
        {
            Text = "Note: Separate each SVID with a comma, e.g., \"120,340,560,780,900,...\"",
            AutoSize = true,
            ForeColor = noteColor,
            Font = hintFont,
            Margin = new Padding(16, 0, 0, 4)
        });
        _txtSet2 = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Width = 460,
            Height = 80,
            Font = labelFont,
            Text = defaultSet2 is null ? string.Empty : string.Join(",", defaultSet2),
            Margin = new Padding(16, 0, 0, 10)
        };
        root.Controls.Add(_txtSet2);

        var pnlButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0),
            Width = 460
        };
        var btnOk = new Button { Text = "Set", DialogResult = DialogResult.None, Width = 100, Height = 32 };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100, Height = 32, Margin = new Padding(8, 0, 0, 0) };
        pnlButtons.Controls.Add(btnOk);
        pnlButtons.Controls.Add(btnCancel);
        root.Controls.Add(pnlButtons);

        Controls.Add(root);
        ThemeHelper.ApplyButtonTheme(pnlButtons);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        btnOk.Click += (_, _) =>
        {
            if (!uint.TryParse(_txtTotalCount.Text.Trim(), out var total) || total < 600)
            {
                MessageBox.Show(this, "Total Count for S1F3 必須為大於等於 600 的整數。", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var s1 = ParseSvids(_txtSet1.Text);
            var s2 = ParseSvids(_txtSet2.Text);
            if (s1.Count < 20 || s1.Count > 45)
            {
                MessageBox.Show(this, "SVID (Set 1) 數量需介於 20 ~ 45。", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (s2.Count < 50 || s2.Count > 100)
            {
                MessageBox.Show(this, "SVID (Set 2) 數量需介於 50 ~ 100。", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            TotalCount = total;
            Set1Svids = s1;
            Set2Svids = s2;
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    private static IReadOnlyList<uint> ParseSvids(string raw)
    {
        var list = new List<uint>();
        foreach (var part in raw.Split(new[] { ',', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (uint.TryParse(part.Trim(), out var svid))
                list.Add(svid);
        }
        return list;
    }
}
