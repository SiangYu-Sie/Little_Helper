using System.Drawing;

namespace HostSimTester.App.Dialogs;

public sealed class ControlJobCommandDialog : Form
{
    private readonly TextBox _txtControlJobId;
    private readonly TextBox _txtCommand;
    private readonly TextBox _txtActionCode;

    public string ControlJobId { get; private set; } = string.Empty;
    public byte ControlJobCommand { get; private set; }
    public byte ActionCode { get; private set; } = 1;

    public ControlJobCommandDialog(string controlJobId, byte command, byte actionCode)
    {
        Text = "CJ Command";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(484, 258);
        BackColor = Color.White;
        Font = new Font("Microsoft JhengHei UI", 9F);

        Controls.Add(new Label { Text = "CJ ID :", AutoSize = true, Location = new Point(12, 84), ForeColor = Color.Black });
        _txtControlJobId = new TextBox { Text = controlJobId, Width = 100, Location = new Point(185, 81) };
        Controls.Add(_txtControlJobId);

        Controls.Add(new Label { Text = "CJ Command :", AutoSize = true, Location = new Point(12, 112), ForeColor = Color.Black });
        _txtCommand = new TextBox
        {
            Text = command.ToString(),
            Width = 100,
            Location = new Point(185, 109),
            BackColor = Color.FromArgb(145, 225, 235)
        };
        Controls.Add(_txtCommand);

        Controls.Add(new Label { Text = "ex. CJAbort or CJStop", AutoSize = true, Location = new Point(185, 132), ForeColor = Color.Black, Font = new Font("Microsoft JhengHei UI", 7.5F) });

        Controls.Add(new Label { Text = "Action Code :", AutoSize = true, Location = new Point(12, 165), ForeColor = Color.Black });
        _txtActionCode = new TextBox
        {
            Text = actionCode.ToString(),
            Width = 100,
            Location = new Point(185, 162),
            BackColor = Color.FromArgb(145, 225, 235)
        };
        Controls.Add(_txtActionCode);

        Controls.Add(new Label
        {
            Text = "1.remove process job associated with this control job",
            AutoSize = true,
            Location = new Point(12, 207),
            ForeColor = Color.Black
        });

        var btnAbort = new Button
        {
            Text = "S16F27 ITCJCmd\r\nReqABORT_Remove",
            Width = 150,
            Height = 51,
            Location = new Point(327, 33),
            Enabled = command == 7
        };
        btnAbort.Click += (_, _) => Complete(7);
        Controls.Add(btnAbort);

        var btnStop = new Button
        {
            Text = "S16F27 ITCJCmd\r\nReq_STOP_Remove",
            Width = 150,
            Height = 51,
            Location = new Point(327, 102),
            Enabled = command == 6
        };
        btnStop.Click += (_, _) => Complete(6);
        Controls.Add(btnStop);

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 100,
            Height = 32,
            Location = new Point(352, 198)
        };
        Controls.Add(btnCancel);
        CancelButton = btnCancel;
    }

    private void Complete(byte fallbackCommand)
    {
        ControlJobId = _txtControlJobId.Text.Trim();
        if (string.IsNullOrWhiteSpace(ControlJobId))
        {
            MessageBox.Show(this, "CJ ID is required.", "CJ Command", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ControlJobCommand = byte.TryParse(_txtCommand.Text.Trim(), out var command) ? command : fallbackCommand;
        ActionCode = byte.TryParse(_txtActionCode.Text.Trim(), out var actionCode) ? actionCode : (byte)1;
        DialogResult = DialogResult.OK;
        Close();
    }
}