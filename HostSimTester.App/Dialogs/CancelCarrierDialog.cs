using System.Drawing;

namespace HostSimTester.App.Dialogs;

public sealed class CancelCarrierDialog : Form
{
    private readonly NumericUpDown _nudPortId;
    private readonly TextBox _txtCarrierId;

    public string CarrierId { get; private set; } = string.Empty;
    public byte PortId { get; private set; } = 1;
    public bool IsCancelCarrierAtPort { get; private set; }

    public CancelCarrierDialog(string carrierId, byte portId)
    {
        Text = "CancelCarrier";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(486, 260);
        BackColor = Color.White;
        Font = new Font("Microsoft JhengHei UI", 9F);

        Controls.Add(new Label
        {
            Text = "Port ID :",
            AutoSize = true,
            Location = new Point(47, 83),
            ForeColor = Color.Black
        });

        _nudPortId = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 8,
            Value = Math.Min(Math.Max(portId, (byte)1), (byte)8),
            Width = 198,
            Location = new Point(97, 80)
        };
        Controls.Add(_nudPortId);

        Controls.Add(new Label
        {
            Text = "Carrier ID :",
            AutoSize = true,
            Location = new Point(32, 112),
            ForeColor = Color.Black
        });

        _txtCarrierId = new TextBox
        {
            Text = carrierId,
            Width = 198,
            Location = new Point(97, 109)
        };
        Controls.Add(_txtCarrierId);

        var btnCancelCarrier = new Button
        {
            Text = "S3F17 CancelCarrier",
            Width = 116,
            Height = 50,
            Location = new Point(22, 137)
        };
        btnCancelCarrier.Click += (_, _) => Complete(false);
        Controls.Add(btnCancelCarrier);

        var btnCancelCarrierAtPort = new Button
        {
            Text = "S3F17 CancelCarrierAtPort",
            Width = 154,
            Height = 50,
            Location = new Point(148, 137)
        };
        btnCancelCarrierAtPort.Click += (_, _) => Complete(true);
        Controls.Add(btnCancelCarrierAtPort);

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 100,
            Height = 50,
            Location = new Point(358, 110)
        };
        Controls.Add(btnCancel);
        CancelButton = btnCancel;
    }

    private void Complete(bool isCancelCarrierAtPort)
    {
        CarrierId = _txtCarrierId.Text.Trim();
        PortId = (byte)_nudPortId.Value;
        IsCancelCarrierAtPort = isCancelCarrierAtPort;
        DialogResult = DialogResult.OK;
        Close();
    }
}