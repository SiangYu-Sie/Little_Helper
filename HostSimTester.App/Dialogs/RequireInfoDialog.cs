using HostSimTester.App.Models;
using HostSimTester.App.Theme;

namespace HostSimTester.App.Dialogs;

public sealed class RequireInfoDialog : Form
{
    private readonly NumericUpDown _nudDeviceId;
    private readonly TextBox _txtIp;
    private readonly NumericUpDown _nudPort;

    public ConnectionSettings? Result { get; private set; }

    public RequireInfoDialog(ConnectionSettings defaults)
    {
        Text = "Provide Required Connection Info";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 460;
        Height = 260;

        ThemeHelper.ApplyTheme(this);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Device ID", ForeColor = ThemeHelper.TextMid, AutoSize = true }, 0, 0);
        _nudDeviceId = new NumericUpDown { Minimum = 0, Maximum = 65535, Value = defaults.DeviceId, Dock = DockStyle.Fill };
        layout.Controls.Add(_nudDeviceId, 1, 0);

        layout.Controls.Add(new Label { Text = "IP Address", ForeColor = ThemeHelper.TextMid, AutoSize = true }, 0, 1);
        _txtIp = new TextBox { Text = defaults.IpAddress, Dock = DockStyle.Fill };
        layout.Controls.Add(_txtIp, 1, 1);

        layout.Controls.Add(new Label { Text = "Port", ForeColor = ThemeHelper.TextMid, AutoSize = true }, 0, 2);
        _nudPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = defaults.Port, Dock = DockStyle.Fill };
        layout.Controls.Add(_nudPort, 1, 2);

        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        pnlButtons.Controls.Add(btnOk);
        pnlButtons.Controls.Add(btnCancel);
        layout.Controls.Add(pnlButtons, 1, 3);

        Controls.Add(layout);
        ThemeHelper.ApplyButtonTheme(pnlButtons);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        btnOk.Click += (_, _) =>
        {
            Result = new ConnectionSettings
            {
                DeviceId = (ushort)_nudDeviceId.Value,
                IpAddress = _txtIp.Text.Trim(),
                Port = (int)_nudPort.Value
            };
        };
    }
}
