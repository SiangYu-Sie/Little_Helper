using System.Drawing;

namespace HostSimTester.App.Dialogs;

public sealed class ProcessJobCommandDialog : Form
{
    private readonly TextBox _txtProcessJobId;
    private readonly TextBox _txtCommand;

    public string ProcessJobId { get; private set; } = string.Empty;
    public string Command { get; private set; } = string.Empty;

    public ProcessJobCommandDialog(string processJobId, string command)
    {
        Text = "PJ Command";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(482, 260);
        BackColor = Color.White;
        Font = new Font("Microsoft JhengHei UI", 9F);

        Controls.Add(new Label
        {
            Text = "PJ ID :",
            AutoSize = true,
            Location = new Point(95, 101),
            ForeColor = Color.Black
        });

        _txtProcessJobId = new TextBox
        {
            Text = processJobId,
            Width = 200,
            Location = new Point(187, 98)
        };
        Controls.Add(_txtProcessJobId);

        Controls.Add(new Label
        {
            Text = "PJ Command :",
            AutoSize = true,
            Location = new Point(95, 129),
            ForeColor = Color.Black
        });

        _txtCommand = new TextBox
        {
            Text = command,
            Width = 200,
            Location = new Point(187, 126)
        };
        Controls.Add(_txtCommand);

        var btnSend = new Button
        {
            Text = "Send",
            Width = 50,
            Height = 24,
            Location = new Point(181, 155)
        };
        btnSend.Click += (_, _) => Complete();
        Controls.Add(btnSend);

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 50,
            Height = 24,
            Location = new Point(251, 155)
        };
        Controls.Add(btnCancel);

        AcceptButton = btnSend;
        CancelButton = btnCancel;
    }

    private void Complete()
    {
        ProcessJobId = _txtProcessJobId.Text.Trim();
        Command = _txtCommand.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(ProcessJobId) || string.IsNullOrWhiteSpace(Command))
        {
            MessageBox.Show(this, "PJ ID and PJ Command are required.", "PJ Command", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}