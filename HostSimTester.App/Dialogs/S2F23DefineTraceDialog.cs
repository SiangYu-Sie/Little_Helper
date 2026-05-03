using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HostSimTester.App.Theme;

namespace HostSimTester.App.Dialogs;

/// <summary>
/// S2F23 Define Trace 設定對話視窗。對應影像01 / 影像02。
/// </summary>
public sealed class S2F23DefineTraceDialog : Form
{
    public byte TraceId { get; }
    public string Dsper { get; private set; } = "000001";
    public uint TotalSamples { get; private set; } = 600;
    public byte ReportGroupSize { get; } = 1;
    public IReadOnlyList<uint> Svids { get; private set; } = Array.Empty<uint>();

    private readonly TextBox _txtTraceId;
    private readonly ComboBox _cmbTridType;
    private readonly RadioButton _rdoDsperHhmmss;
    private readonly RadioButton _rdoDsperHhmmsscc;
    private readonly TextBox _txtTotsmp;
    private readonly ComboBox _cmbTotsmpType;
    private readonly ComboBox _cmbRepgszType;
    private readonly TextBox _txtSvids;

    public S2F23DefineTraceDialog(byte traceId,
        string defaultDsper = "000001",
        uint defaultTotsmp = 600,
        IEnumerable<uint>? defaultSvids = null)
    {
        TraceId = traceId;

        Text = "Define Trace";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 460;
        Height = 540;
        ThemeHelper.ApplyTheme(this);

        var hintBlue = Color.FromArgb(40, 90, 180);
        var labelFont = new Font("Microsoft JhengHei UI", 9F);
        var hintFont = new Font("Microsoft JhengHei UI", 8.5F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 16, 20, 12),
            ColumnCount = 3,
            RowCount = 12,
            AutoSize = false
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (var i = 0; i < 12; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        int row = 0;

        // TraceID
        layout.Controls.Add(new Label { Text = "TraceID :", AutoSize = true, Font = labelFont, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right }, 0, row);
        _txtTraceId = new TextBox { Text = traceId.ToString(), ReadOnly = true, Width = 80, Font = labelFont };
        layout.Controls.Add(_txtTraceId, 1, row++);

        // TRID Type label + combo
        var lblTrid = new Label { Text = "Please Confirm Data Format. TRID Type =", AutoSize = true, ForeColor = hintBlue, Font = hintFont, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right };
        layout.Controls.Add(lblTrid, 0, row);
        layout.SetColumnSpan(lblTrid, 2);
        _cmbTridType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = labelFont };
        _cmbTridType.Items.AddRange(new object[] { "U1", "U2", "U4", "U8" });
        _cmbTridType.SelectedItem = "U1";
        layout.Controls.Add(_cmbTridType, 2, row++);

        // Data Sample Period header
        var lblDsperHdr = new Label { Text = "Data Sample Period :  Must be 1 second:", AutoSize = true, Font = labelFont };
        layout.Controls.Add(lblDsperHdr, 0, row);
        layout.SetColumnSpan(lblDsperHdr, 3);
        row++;

        _rdoDsperHhmmss = new RadioButton { Text = "000001 (Format: hhmmss)", AutoSize = true, Checked = defaultDsper == "000001", Font = labelFont };
        _rdoDsperHhmmsscc = new RadioButton { Text = "00000100 (Format: hhmmsscc)", AutoSize = true, Checked = defaultDsper != "000001", Font = labelFont };
        var dsperPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(40, 0, 0, 6) };
        dsperPanel.Controls.Add(_rdoDsperHhmmss);
        dsperPanel.Controls.Add(_rdoDsperHhmmsscc);
        layout.Controls.Add(dsperPanel, 0, row);
        layout.SetColumnSpan(dsperPanel, 3);
        row++;

        // Sample Limit (TOTSMP)
        layout.Controls.Add(new Label { Text = "Sample Limit (TOTSMP) :", AutoSize = true, Font = labelFont, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right }, 0, row);
        _txtTotsmp = new TextBox { Text = defaultTotsmp.ToString(), Width = 140, Font = labelFont };
        layout.Controls.Add(_txtTotsmp, 1, row);
        layout.Controls.Add(new Label { Text = "(must >= 600)", AutoSize = true, ForeColor = Color.Gray, Font = hintFont, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left }, 2, row);
        row++;

        // TOTSMP Type
        var lblTotsmp = new Label { Text = "Please Confirm Data Format. TOTSMP Type =", AutoSize = true, ForeColor = hintBlue, Font = hintFont, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right };
        layout.Controls.Add(lblTotsmp, 0, row);
        layout.SetColumnSpan(lblTotsmp, 2);
        _cmbTotsmpType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = labelFont };
        _cmbTotsmpType.Items.AddRange(new object[] { "U1", "U2", "U4", "U8" });
        _cmbTotsmpType.SelectedItem = "U4";
        layout.Controls.Add(_cmbTotsmpType, 2, row++);

        // Report Group Size: 1
        layout.Controls.Add(new Label { Text = "Report Group Size :  1", AutoSize = true, Font = labelFont, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right, Margin = new Padding(0, 8, 0, 0) }, 0, row);
        row++;

        var lblRepgsz = new Label { Text = "Please Confirm Data Format. REPGSZ Type =", AutoSize = true, ForeColor = hintBlue, Font = hintFont, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right };
        layout.Controls.Add(lblRepgsz, 0, row);
        layout.SetColumnSpan(lblRepgsz, 2);
        _cmbRepgszType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = labelFont };
        _cmbRepgszType.Items.AddRange(new object[] { "U1", "U2", "U4", "U8" });
        _cmbRepgszType.SelectedItem = "U1";
        layout.Controls.Add(_cmbRepgszType, 2, row++);

        // SVID
        layout.Controls.Add(new Label { Text = "SVID :", AutoSize = true, Font = labelFont, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) }, 0, row);
        var defaultSvidText = defaultSvids is null ? string.Empty : string.Join(",", defaultSvids);
        _txtSvids = new TextBox { Text = defaultSvidText, Width = 220, Font = labelFont, Margin = new Padding(0, 12, 0, 0) };
        layout.Controls.Add(_txtSvids, 1, row);
        layout.SetColumnSpan(_txtSvids, 2);
        row++;

        var lblSvidHint = new Label { Text = "ex. 12,34,56,78,90", AutoSize = true, ForeColor = Color.Gray, Font = hintFont };
        layout.Controls.Add(lblSvidHint, 1, row);
        layout.SetColumnSpan(lblSvidHint, 2);
        row++;

        // Buttons
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 18, 0, 0),
            Height = 40
        };
        var btnOk = new Button { Text = "Start Trace", DialogResult = DialogResult.None, Width = 110, Height = 32, Margin = new Padding(8, 4, 0, 0) };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 110, Height = 32, Margin = new Padding(8, 4, 0, 0) };
        pnlButtons.Controls.Add(btnOk);
        pnlButtons.Controls.Add(btnCancel);
        layout.Controls.Add(pnlButtons, 0, row);
        layout.SetColumnSpan(pnlButtons, 3);

        Controls.Add(layout);
        ThemeHelper.ApplyButtonTheme(pnlButtons);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        btnOk.Click += (_, _) =>
        {
            Dsper = _rdoDsperHhmmss.Checked ? "000001" : "00000100";

            if (!uint.TryParse(_txtTotsmp.Text.Trim(), out var totsmp) || totsmp < 600)
            {
                MessageBox.Show(this, "Sample Limit (TOTSMP) 必須為大於等於 600 的整數。", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            TotalSamples = totsmp;

            var svids = ParseSvids(_txtSvids.Text);
            if (svids.Count == 0)
            {
                MessageBox.Show(this, "請至少輸入一個 SVID（以逗號分隔）。", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Svids = svids;

            DialogResult = DialogResult.OK;
            Close();
        };
    }

    private static IReadOnlyList<uint> ParseSvids(string raw)
    {
        var list = new List<uint>();
        foreach (var part in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (uint.TryParse(part, out var svid))
                list.Add(svid);
        }
        return list;
    }
}
