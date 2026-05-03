using System.Drawing;

namespace HostSimTester.App.Dialogs;

public sealed class UnloadCarrierDialog : Form
{
    public enum UnloadCommandKind
    {
        CarrierRelease,
        RemoteCommand
    }

    public UnloadCommandKind CommandKind { get; private set; } = UnloadCommandKind.CarrierRelease;
    public byte PortId { get; private set; } = 1;
    public string CarrierId { get; private set; } = string.Empty;
    public string RemoteCommand { get; private set; } = string.Empty;
    public string CpName { get; private set; } = string.Empty;
    public string CpType { get; private set; } = "U1";
    public string CpValue { get; private set; } = string.Empty;

    private readonly NumericUpDown _nudPortId;
    private readonly TextBox _txtCarrierId;
    private readonly TextBox _txtRemoteCommand;
    private readonly TextBox _txtCpName;
    private readonly ComboBox _cmbCpType;
    private readonly TextBox _txtCpValue;

    public UnloadCarrierDialog(string carrierId, byte portId)
    {
        Text = "Unload Carrier";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(580, 470);
        BackColor = Theme.ThemeHelper.IceSurface;
        Font = new Font("Microsoft JhengHei UI", 9F);

        var grpCarrierRelease = new GroupBox
        {
            Text = "Host Send S3F17 CarrierRelease",
            Location = new Point(8, 6),
            Size = new Size(564, 180),
            ForeColor = Theme.ThemeHelper.TextDark
        };

        grpCarrierRelease.Controls.Add(new Label { Text = "PortID :", AutoSize = true, Location = new Point(20, 38), ForeColor = Theme.ThemeHelper.TextDark });
        _nudPortId = new NumericUpDown { Minimum = 1, Maximum = 4, Value = portId, Location = new Point(100, 34), Width = 150 };
        grpCarrierRelease.Controls.Add(_nudPortId);

        grpCarrierRelease.Controls.Add(new Label { Text = "CarrierID :", AutoSize = true, Location = new Point(20, 88), ForeColor = Theme.ThemeHelper.TextDark });
        _txtCarrierId = new TextBox { Text = carrierId, Location = new Point(100, 84), Width = 150 };
        grpCarrierRelease.Controls.Add(_txtCarrierId);

        var btnCarrierRelease = new Button
        {
            Text = "S3F17\r\nCarrierRelease",
            Location = new Point(432, 84),
            Size = new Size(100, 52)
        };
        btnCarrierRelease.Click += (_, _) => Submit(UnloadCommandKind.CarrierRelease);
        grpCarrierRelease.Controls.Add(btnCarrierRelease);

        var grpRemote = new GroupBox
        {
            Text = "Host Send S2F41 to Unload Carrier",
            Location = new Point(8, 196),
            Size = new Size(564, 220),
            ForeColor = Theme.ThemeHelper.TextDark
        };

        grpRemote.Controls.Add(new Label { Text = "Command :", AutoSize = true, Location = new Point(20, 30), ForeColor = Theme.ThemeHelper.TextDark });
        _txtRemoteCommand = new TextBox { Location = new Point(100, 26), Width = 150 };
        grpRemote.Controls.Add(_txtRemoteCommand);

        grpRemote.Controls.Add(new Label { Text = "ex. UNLOCK or UNDOCK or UNCLAMP", AutoSize = true, Location = new Point(20, 56), ForeColor = Theme.ThemeHelper.TextMid });
        grpRemote.Controls.Add(new Label { Text = "Parameters List", AutoSize = true, Location = new Point(20, 94), ForeColor = Theme.ThemeHelper.TextDark });

        grpRemote.Controls.Add(new Label { Text = "CPNAME :", AutoSize = true, Location = new Point(20, 126), ForeColor = Theme.ThemeHelper.TextDark });
        _txtCpName = new TextBox { Location = new Point(100, 122), Width = 150 };
        grpRemote.Controls.Add(_txtCpName);

        grpRemote.Controls.Add(new Label { Text = "CPVAL(Type) :", AutoSize = true, Location = new Point(20, 158), ForeColor = Theme.ThemeHelper.TextDark });
        _cmbCpType = new ComboBox { Location = new Point(100, 154), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbCpType.Items.AddRange(["A", "B", "Boolean", "I1", "I2", "I4", "I8", "U1", "U2", "U4", "U8", "F4", "F8"]);
        _cmbCpType.SelectedItem = "U1";
        grpRemote.Controls.Add(_cmbCpType);

        grpRemote.Controls.Add(new Label { Text = "CPVAL(Value) :", AutoSize = true, Location = new Point(20, 190), ForeColor = Theme.ThemeHelper.TextDark });
        _txtCpValue = new TextBox { Location = new Point(100, 186), Width = 150 };
        grpRemote.Controls.Add(_txtCpValue);

        var btnRemoteCommand = new Button
        {
            Text = "S2F41 Remote\r\nCommand",
            Location = new Point(432, 84),
            Size = new Size(100, 52)
        };
        btnRemoteCommand.Click += (_, _) => Submit(UnloadCommandKind.RemoteCommand);
        grpRemote.Controls.Add(btnRemoteCommand);

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(436, 430),
            Size = new Size(100, 28)
        };

        Controls.Add(grpCarrierRelease);
        Controls.Add(grpRemote);
        Controls.Add(btnCancel);
        CancelButton = btnCancel;

        Theme.ThemeHelper.ApplyButtonTheme(grpCarrierRelease);
        Theme.ThemeHelper.ApplyButtonTheme(grpRemote);
        Theme.ThemeHelper.ApplyButtonTheme(this);
    }

    private void Submit(UnloadCommandKind kind)
    {
        CommandKind = kind;
        PortId = (byte)_nudPortId.Value;
        CarrierId = _txtCarrierId.Text.Trim();
        RemoteCommand = _txtRemoteCommand.Text.Trim();
        CpName = _txtCpName.Text.Trim();
        CpType = _cmbCpType.SelectedItem?.ToString() ?? "U1";
        CpValue = _txtCpValue.Text.Trim();

        if (kind == UnloadCommandKind.CarrierRelease && string.IsNullOrWhiteSpace(CarrierId))
        {
            MessageBox.Show(this, "CarrierID is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (kind == UnloadCommandKind.RemoteCommand && string.IsNullOrWhiteSpace(RemoteCommand))
        {
            MessageBox.Show(this, "Command is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}