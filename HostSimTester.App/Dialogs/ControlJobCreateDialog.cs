using System.Drawing;
using System.Text.RegularExpressions;

namespace HostSimTester.App.Dialogs;

public sealed class ControlJobCreateDialog : Form
{
    public string ObjSpec { get; private set; } = "Equipment";
    public string ControlJobId { get; private set; } = string.Empty;
    public string ProcessOrderMgmt { get; private set; } = "2";
    public string PjList { get; private set; } = string.Empty;
    public string CarrierId { get; private set; } = string.Empty;

    private readonly TextBox _txtObjSpec;
    private readonly TextBox _txtControlJobId;
    private readonly TextBox _txtProcessOrderMgmt;
    private readonly TextBox _txtPjList;
    private readonly TextBox _txtCarrierId;

    public ControlJobCreateDialog(string controlJobId, IEnumerable<string> processJobIds, string carrierId, string processOrderMgmt = "2")
    {
        Text = "CJ Create";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(478, 390);
        BackColor = Theme.ThemeHelper.IceSurface;
        Font = new Font("Microsoft JhengHei UI", 9F);

        var labelColor = Theme.ThemeHelper.TextDark;
        var y = 16;

        Controls.Add(new Label { Text = "1.OBJSPEC :", AutoSize = true, Location = new Point(12, y + 4), ForeColor = labelColor });
        _txtObjSpec = new TextBox { Location = new Point(90, y), Width = 200, Text = "Equipment", BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_txtObjSpec);

        y += 36;
        Controls.Add(new Label { Text = "2.ControlJob (CJID) :", AutoSize = true, Location = new Point(12, y + 4), ForeColor = labelColor });
        _txtControlJobId = new TextBox { Location = new Point(138, y), Width = 200, Text = controlJobId, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_txtControlJobId);

        y += 36;
        Controls.Add(new Label { Text = "3.ProcessOrderMgmt :", AutoSize = true, Location = new Point(12, y + 4), ForeColor = labelColor });
        _txtProcessOrderMgmt = new TextBox { Location = new Point(150, y), Width = 200, Text = processOrderMgmt, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_txtProcessOrderMgmt);

        y += 36;
        Controls.Add(new Label { Text = "4.PJ List :", AutoSize = true, Location = new Point(12, y + 4), ForeColor = labelColor });
        _txtPjList = new TextBox
        {
            Location = new Point(86, y),
            Width = 185,
            Text = FormatPjList(processJobIds),
            BackColor = Color.FromArgb(180, 230, 240),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_txtPjList);

        y += 36;
        Controls.Add(new Label { Text = "5.CarrierID :", AutoSize = true, Location = new Point(12, y + 4), ForeColor = labelColor });
        _txtCarrierId = new TextBox
        {
            Location = new Point(90, y),
            Width = 200,
            Text = carrierId,
            BackColor = Color.FromArgb(180, 230, 240),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_txtCarrierId);

        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(222, 328), Size = new Size(100, 30) };
        var btnSend = new Button { Text = "Send", Location = new Point(332, 328), Size = new Size(100, 30) };
        btnSend.Click += (_, _) => OnSend();
        Controls.Add(btnCancel);
        Controls.Add(btnSend);

        Theme.ThemeHelper.ApplyButtonTheme(this);
        btnCancel.BackColor = Color.FromArgb(210, 218, 228);
        btnCancel.ForeColor = Theme.ThemeHelper.TextDark;

        AcceptButton = btnSend;
        CancelButton = btnCancel;
    }

    public IReadOnlyList<string> GetProcessJobIds()
    {
        return Regex.Matches(_txtPjList.Text, @"[A-Za-z0-9._-]+")
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void OnSend()
    {
        ObjSpec = _txtObjSpec.Text.Trim();
        ControlJobId = _txtControlJobId.Text.Trim();
        ProcessOrderMgmt = _txtProcessOrderMgmt.Text.Trim();
        PjList = _txtPjList.Text.Trim();
        CarrierId = _txtCarrierId.Text.Trim();

        if (string.IsNullOrWhiteSpace(ControlJobId))
        {
            MessageBox.Show(this, "ControlJob ID is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (GetProcessJobIds().Count == 0)
        {
            MessageBox.Show(this, "PJ List is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(CarrierId))
        {
            MessageBox.Show(this, "CarrierID is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static string FormatPjList(IEnumerable<string> processJobIds)
    {
        var ids = processJobIds
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => $"\"{x}\"")
            .ToArray();

        return "[" + string.Join(",", ids) + "]";
    }
}