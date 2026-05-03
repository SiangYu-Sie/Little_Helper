using System.Drawing;
using System.Text.RegularExpressions;

namespace HostSimTester.App.Dialogs;

public sealed class ProcessJobCreateDialog : Form
{
    public string ProcessJobId { get; private set; } = string.Empty;
    public string Ppid { get; private set; } = string.Empty;
    public string CarrierId { get; private set; } = string.Empty;
    public string SlotIdList { get; private set; } = string.Empty;

    private readonly TextBox _txtProcessJobId;
    private readonly TextBox _txtPpid;
    private readonly TextBox _txtCarrierId;
    private readonly TextBox _txtSlotIdList;

    public ProcessJobCreateDialog(
        string processJobId,
        string ppid,
        string carrierId,
        IEnumerable<string> defaultSlotIds,
        IEnumerable<string> contentMapSlotIds)
    {
        Text = "PJ Create";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(484, 390);
        BackColor = Theme.ThemeHelper.IceSurface;
        Font = new Font("Microsoft JhengHei UI", 9F);

        var labelColor = Theme.ThemeHelper.TextDark;
        var y = 18;

        Controls.Add(new Label { Text = "1.PRJOBID(Process Job ID) :", AutoSize = true, Location = new Point(12, y + 4), ForeColor = labelColor });
        _txtProcessJobId = new TextBox { Location = new Point(174, y), Width = 202, Text = processJobId, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_txtProcessJobId);

        y += 36;
        Controls.Add(new Label { Text = "2.PPID :", AutoSize = true, Location = new Point(12, y + 4), ForeColor = labelColor });
        _txtPpid = new TextBox { Location = new Point(68, y), Width = 198, Text = ppid, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_txtPpid);

        y += 28;
        Controls.Add(new Label
        {
            Text = "Note: Recipe ID (PPID) will be applied to both Process Jobs(PJ1 and PJ2).\r\nEnsure the Recipe ID (PPID) is the same for both.",
            AutoSize = true,
            Location = new Point(22, y),
            ForeColor = Theme.ThemeHelper.TextMid
        });

        y += 48;
        Controls.Add(new Label { Text = "3.CarrierID :", AutoSize = true, Location = new Point(12, y + 4), ForeColor = labelColor });
        _txtCarrierId = new TextBox
        {
            Location = new Point(90, y),
            Width = 200,
            Text = carrierId,
            BackColor = Color.FromArgb(180, 230, 240),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_txtCarrierId);

        y += 36;
        Controls.Add(new Label { Text = "4.SlotIDList :", AutoSize = true, Location = new Point(12, y + 4), ForeColor = labelColor });
        _txtSlotIdList = new TextBox
        {
            Location = new Point(90, y),
            Width = 200,
            Text = string.Join(",", defaultSlotIds),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_txtSlotIdList);

        var noteBox = new GroupBox
        {
            Location = new Point(0, y + 4),
            Size = new Size(480, 140),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        noteBox.Controls.Add(new Label
        {
            Text = "Note:\r\n\r\n(1) Enter SlotID List in one of these formats:\r\n    single) or 1,3,5 (comma-separated) or 1-10 (range).\r\n(2) Ensure your input is within the predefined SlotID values in ContentMap.\r\nReference - Predefined SlotID List in Content Map:",
            AutoSize = true,
            Location = new Point(12, 28),
            ForeColor = Theme.ThemeHelper.TextMid
        });
        noteBox.Controls.Add(new TextBox
        {
            Location = new Point(14, 118),
            Width = 300,
            Text = string.Join(",", contentMapSlotIds),
            ReadOnly = true,
            BackColor = Color.FromArgb(180, 230, 240),
            BorderStyle = BorderStyle.FixedSingle
        });
        Controls.Add(noteBox);

        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(222, 336), Size = new Size(100, 30) };
        var btnSend = new Button { Text = "Send", Location = new Point(332, 336), Size = new Size(100, 30) };
        btnSend.Click += (_, _) => OnSend();
        Controls.Add(btnCancel);
        Controls.Add(btnSend);

        Theme.ThemeHelper.ApplyButtonTheme(this);
        btnCancel.BackColor = Color.FromArgb(210, 218, 228);
        btnCancel.ForeColor = Theme.ThemeHelper.TextDark;

        AcceptButton = btnSend;
        CancelButton = btnCancel;
    }

    public IReadOnlyList<string> GetSlotIds()
    {
        return ParseSlotIds(_txtSlotIdList.Text);
    }

    private void OnSend()
    {
        ProcessJobId = _txtProcessJobId.Text.Trim();
        Ppid = _txtPpid.Text.Trim();
        CarrierId = _txtCarrierId.Text.Trim();
        SlotIdList = _txtSlotIdList.Text.Trim();

        if (string.IsNullOrWhiteSpace(ProcessJobId))
        {
            MessageBox.Show(this, "PRJOBID is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Ppid))
        {
            MessageBox.Show(this, "PPID is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    private static IReadOnlyList<string> ParseSlotIds(string raw)
    {
        var slots = new List<int>();
        foreach (var part in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var range = Regex.Match(part, @"^(\d+)\s*-\s*(\d+)$");
            if (range.Success && int.TryParse(range.Groups[1].Value, out var start) && int.TryParse(range.Groups[2].Value, out var end))
            {
                if (start > end)
                {
                    (start, end) = (end, start);
                }

                for (var i = start; i <= end; i++)
                {
                    slots.Add(i);
                }

                continue;
            }

            foreach (Match match in Regex.Matches(part, @"\d+").Cast<Match>())
            {
                if (int.TryParse(match.Value, out var slot))
                {
                    slots.Add(slot);
                }
            }
        }

        return slots
            .Where(x => x > 0 && x <= byte.MaxValue)
            .Distinct()
            .Select(x => x.ToString())
            .ToArray();
    }
}