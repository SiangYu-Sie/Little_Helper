using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace HostSimTester.App.Dialogs;

/// <summary>
/// S3F17 ProceedWithCarrier 設定對話視窗。
/// </summary>
public sealed class S3F17ProceedDialog : Form
{
    // ── 結果屬性 ──────────────────────────────────────────────────────────────
    public string CarrierId { get; private set; } = string.Empty;
    public byte PortId { get; private set; } = 1;

    // 1) ContentMap
    public bool IncludeContentMap { get; private set; } = true;
    public string ContentMapCattrid { get; private set; } = "ContentMap";
    public IReadOnlyList<(string LotId, string WaferId)> SlotEntries { get; private set; }
        = Array.Empty<(string, string)>();
    public IReadOnlyList<string> OccupiedSlotIds { get; private set; } = Array.Empty<string>();

    // 2) SlotMap
    public bool IncludeSlotMap { get; private set; } = false;
    public string SlotMapCattrid { get; private set; } = "SlotMap";
    public bool SlotMapFormat1Empty3Correct { get; private set; } = true;

    // 3) Usage
    public bool IncludeUsage { get; private set; } = false;
    public string UsageCattrid { get; private set; } = "Usage";
    public string UsageType { get; private set; } = "A";
    public string UsageValue { get; private set; } = string.Empty;

    // 4) Capacity
    public bool IncludeCapacity { get; private set; } = false;
    public string CapacityCattrid { get; private set; } = "Capacity";
    public string CapacityType { get; private set; } = "U1";
    public string CapacityValue { get; private set; } = "25";

    // 5) SubstrateCount
    public bool IncludeSubstrateCount { get; private set; } = false;
    public string SubstrateCountCattrid { get; private set; } = "SubstrateCount";
    public string SubstrateCountType { get; private set; } = "U1";
    public string SubstrateCountValue { get; private set; } = string.Empty;

    // ── 內部控制項 ────────────────────────────────────────────────────────────
    private readonly TextBox _txtCarrierId;
    private readonly NumericUpDown _nudPort;

    // 1) ContentMap
    private readonly RadioButton _rdoContentMapYes;
    private readonly RadioButton _rdoContentMapNo;
    private readonly TextBox _txtContentMapCattrid;
    private readonly DataGridView _grid;

    // 2) SlotMap
    private readonly RadioButton _rdoSlotMapYes;
    private readonly RadioButton _rdoSlotMapNo;
    private readonly TextBox _txtSlotMapCattrid;
    private readonly RadioButton _rdoSlotMap1Empty3Correct;
    private readonly RadioButton _rdoSlotMap0Empty1Correct;
    private readonly Panel _slotMapChoicePanel;

    // 3) Usage
    private readonly RadioButton _rdoUsageYes;
    private readonly RadioButton _rdoUsageNo;
    private readonly TextBox _txtUsageCattrid;
    private readonly ComboBox _cmbUsageType;
    private readonly TextBox _txtUsageValue;

    // 4) Capacity
    private readonly RadioButton _rdoCapacityYes;
    private readonly RadioButton _rdoCapacityNo;
    private readonly TextBox _txtCapacityCattrid;
    private readonly ComboBox _cmbCapacityType;
    private readonly TextBox _txtCapacityValue;

    // 5) SubstrateCount
    private readonly RadioButton _rdoSubstrateCountYes;
    private readonly RadioButton _rdoSubstrateCountNo;
    private readonly TextBox _txtSubstrateCountCattrid;
    private readonly ComboBox _cmbSubstrateCountType;
    private readonly TextBox _txtSubstrateCountValue;

    private const int SlotCount = 25;

    // ── 跨對話視窗的持久化 Slot 資料 ─────────────────────────────────────────
    private static readonly (string LotId, string WaferId)[] s_savedSlots =
        new (string, string)[SlotCount];

    private static readonly string[] SecsTypes =
        ["A", "B", "Boolean", "I1", "I2", "I4", "I8", "U1", "U2", "U4", "U8", "F4", "F8"];

    public S3F17ProceedDialog(string carrierId, byte portId)
    {
        Text = "S3F17 ProceedWithCarrier";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Theme.ThemeHelper.IceSurface;
        Font = new Font("Microsoft JhengHei UI", 9F);
        ClientSize = new Size(760, 740);

        // ── Header: CarrierID / PortID ────────────────────────────────────────
        var lblCid = new Label
        {
            Text = "CarrierID",
            AutoSize = true,
            Location = new Point(12, 18),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        _txtCarrierId = new TextBox
        {
            Text = carrierId,
            Width = 160,
            Location = new Point(84, 15),
            BorderStyle = BorderStyle.FixedSingle
        };
        var lblPort = new Label
        {
            Text = "PortID :",
            AutoSize = true,
            Location = new Point(262, 18),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        _nudPort = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 8,
            Value = portId,
            Width = 60,
            Location = new Point(316, 15)
        };
        Controls.Add(lblCid);
        Controls.Add(_txtCarrierId);
        Controls.Add(lblPort);
        Controls.Add(_nudPort);

        // ── Left Panel ────────────────────────────────────────────────────────
        var leftPanel = new Panel
        {
            Location = new Point(12, 46),
            Size = new Size(368, 648),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };
        Controls.Add(leftPanel);

        leftPanel.Controls.Add(new Label
        {
            Text = "S3,F17 Message Format Setting:",
            Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
            ForeColor = Theme.ThemeHelper.TextDark,
            AutoSize = true,
            Location = new Point(8, 6)
        });

        // ── Section 1: ContentMap (直接在 leftPanel 中，形成獨立 radio 群組) ──
        leftPanel.Controls.Add(new Label
        {
            Text = "1) Need Content Map in S3,F17",
            ForeColor = Theme.ThemeHelper.TextDark,
            AutoSize = true,
            Location = new Point(8, 28)
        });

        _rdoContentMapYes = new RadioButton
        {
            Text = "Yes, CATTRID(Attribute Name) =",
            Checked = true,
            AutoSize = true,
            Location = new Point(16, 48),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        _txtContentMapCattrid = new TextBox
        {
            Text = "ContentMap",
            Width = 98,
            Location = new Point(228, 46),
            BorderStyle = BorderStyle.FixedSingle
        };
        _rdoContentMapNo = new RadioButton
        {
            Text = "NO",
            AutoSize = true,
            Location = new Point(16, 72),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        leftPanel.Controls.Add(_rdoContentMapYes);
        leftPanel.Controls.Add(_txtContentMapCattrid);
        leftPanel.Controls.Add(_rdoContentMapNo);

        _rdoContentMapYes.CheckedChanged += (_, _) =>
        {
            _txtContentMapCattrid.Enabled = _rdoContentMapYes.Checked;
            _grid.Enabled = _rdoContentMapYes.Checked;
        };

        // ── Section 2: SlotMap (獨立 sub-Panel，保持 radio 群組隔離) ──────────
        var sec2Panel = new Panel
        {
            Location = new Point(0, 96),
            Size = new Size(366, 150),
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        leftPanel.Controls.Add(sec2Panel);

        sec2Panel.Controls.Add(new Label
        {
            Text = "2) Need Slot Map in S3,F17",
            ForeColor = Theme.ThemeHelper.TextDark,
            AutoSize = true,
            Location = new Point(8, 2)
        });

        _rdoSlotMapYes = new RadioButton
        {
            Text = "YES",
            AutoSize = true,
            Location = new Point(16, 22),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        sec2Panel.Controls.Add(_rdoSlotMapYes);

        sec2Panel.Controls.Add(new Label
        {
            Text = "CATTRID (Attribute name) =",
            AutoSize = true,
            Location = new Point(28, 44),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _txtSlotMapCattrid = new TextBox
        {
            Text = "SlotMap",
            Width = 90,
            Location = new Point(190, 42),
            Enabled = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        sec2Panel.Controls.Add(_txtSlotMapCattrid);

        sec2Panel.Controls.Add(new Label
        {
            Text = "Choose SLOTMAP form",
            AutoSize = true,
            Location = new Point(28, 64),
            ForeColor = Theme.ThemeHelper.TextDark
        });

        _slotMapChoicePanel = new Panel
        {
            Location = new Point(28, 82),
            Size = new Size(330, 42),
            BackColor = Color.White,
            Enabled = false
        };
        _rdoSlotMap1Empty3Correct = new RadioButton
        {
            Text = "1=Empty  3=Correctly occupied",
            Checked = true,
            AutoSize = true,
            Location = new Point(0, 2),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        _rdoSlotMap0Empty1Correct = new RadioButton
        {
            Text = "0=Empty  1=Correctly occupied",
            AutoSize = true,
            Location = new Point(0, 22),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        _slotMapChoicePanel.Controls.Add(_rdoSlotMap1Empty3Correct);
        _slotMapChoicePanel.Controls.Add(_rdoSlotMap0Empty1Correct);
        sec2Panel.Controls.Add(_slotMapChoicePanel);

        _rdoSlotMapNo = new RadioButton
        {
            Text = "NO",
            Checked = true,
            AutoSize = true,
            Location = new Point(16, 126),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        sec2Panel.Controls.Add(_rdoSlotMapNo);

        _rdoSlotMapYes.CheckedChanged += (_, _) =>
        {
            _txtSlotMapCattrid.Enabled = _rdoSlotMapYes.Checked;
            _slotMapChoicePanel.Enabled = _rdoSlotMapYes.Checked;
        };

        // ── Section 3: Usage ──────────────────────────────────────────────────
        var sec3Panel = new Panel
        {
            Location = new Point(0, 248),
            Size = new Size(366, 128),
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        leftPanel.Controls.Add(sec3Panel);

        sec3Panel.Controls.Add(new Label
        {
            Text = "3) Need Usage in S3,F17",
            ForeColor = Theme.ThemeHelper.TextDark,
            AutoSize = true,
            Location = new Point(8, 2)
        });
        _rdoUsageYes = new RadioButton
        {
            Text = "YES",
            AutoSize = true,
            Location = new Point(16, 22),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        sec3Panel.Controls.Add(_rdoUsageYes);

        sec3Panel.Controls.Add(new Label
        {
            Text = "CATTRID (Attribute Name) =",
            AutoSize = true,
            Location = new Point(28, 44),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _txtUsageCattrid = new TextBox
        {
            Text = "Usage",
            Width = 90,
            Location = new Point(190, 42),
            Enabled = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        sec3Panel.Controls.Add(_txtUsageCattrid);

        sec3Panel.Controls.Add(new Label
        {
            Text = "Usage(Type) =",
            AutoSize = true,
            Location = new Point(28, 64),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _cmbUsageType = new ComboBox
        {
            Width = 90,
            Location = new Point(124, 62),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        _cmbUsageType.Items.AddRange(SecsTypes);
        _cmbUsageType.SelectedItem = "A";
        sec3Panel.Controls.Add(_cmbUsageType);

        sec3Panel.Controls.Add(new Label
        {
            Text = "Usage(Value) =",
            AutoSize = true,
            Location = new Point(28, 86),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _txtUsageValue = new TextBox
        {
            Text = string.Empty,
            Width = 90,
            Location = new Point(124, 84),
            Enabled = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        sec3Panel.Controls.Add(_txtUsageValue);

        _rdoUsageNo = new RadioButton
        {
            Text = "NO",
            Checked = true,
            AutoSize = true,
            Location = new Point(16, 108),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        sec3Panel.Controls.Add(_rdoUsageNo);

        _rdoUsageYes.CheckedChanged += (_, _) =>
        {
            bool on = _rdoUsageYes.Checked;
            _txtUsageCattrid.Enabled = on;
            _cmbUsageType.Enabled = on;
            _txtUsageValue.Enabled = on;
        };

        // ── Section 4: Capacity ───────────────────────────────────────────────
        var sec4Panel = new Panel
        {
            Location = new Point(0, 378),
            Size = new Size(366, 128),
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        leftPanel.Controls.Add(sec4Panel);

        sec4Panel.Controls.Add(new Label
        {
            Text = "4) Need Capacity in S3,F17",
            ForeColor = Theme.ThemeHelper.TextDark,
            AutoSize = true,
            Location = new Point(8, 2)
        });
        _rdoCapacityYes = new RadioButton
        {
            Text = "YES",
            AutoSize = true,
            Location = new Point(16, 22),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        sec4Panel.Controls.Add(_rdoCapacityYes);

        sec4Panel.Controls.Add(new Label
        {
            Text = "CATTRID (Attribute Name) =",
            AutoSize = true,
            Location = new Point(28, 44),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _txtCapacityCattrid = new TextBox
        {
            Text = "Capacity",
            Width = 90,
            Location = new Point(190, 42),
            Enabled = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        sec4Panel.Controls.Add(_txtCapacityCattrid);

        sec4Panel.Controls.Add(new Label
        {
            Text = "Usage(Type) =",
            AutoSize = true,
            Location = new Point(28, 64),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _cmbCapacityType = new ComboBox
        {
            Width = 90,
            Location = new Point(124, 62),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        _cmbCapacityType.Items.AddRange(SecsTypes);
        _cmbCapacityType.SelectedItem = "U1";
        sec4Panel.Controls.Add(_cmbCapacityType);

        sec4Panel.Controls.Add(new Label
        {
            Text = "Usage(Value) =",
            AutoSize = true,
            Location = new Point(28, 86),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _txtCapacityValue = new TextBox
        {
            Text = "25",
            Width = 90,
            Location = new Point(124, 84),
            Enabled = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        sec4Panel.Controls.Add(_txtCapacityValue);

        _rdoCapacityNo = new RadioButton
        {
            Text = "NO",
            Checked = true,
            AutoSize = true,
            Location = new Point(16, 108),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        sec4Panel.Controls.Add(_rdoCapacityNo);

        _rdoCapacityYes.CheckedChanged += (_, _) =>
        {
            bool on = _rdoCapacityYes.Checked;
            _txtCapacityCattrid.Enabled = on;
            _cmbCapacityType.Enabled = on;
            _txtCapacityValue.Enabled = on;
        };

        // ── Section 5: SubstrateCount ─────────────────────────────────────────
        var sec5Panel = new Panel
        {
            Location = new Point(0, 508),
            Size = new Size(366, 128),
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        leftPanel.Controls.Add(sec5Panel);

        sec5Panel.Controls.Add(new Label
        {
            Text = "5) Need SubstrateCount in S3,F17",
            ForeColor = Theme.ThemeHelper.TextDark,
            AutoSize = true,
            Location = new Point(8, 2)
        });
        _rdoSubstrateCountYes = new RadioButton
        {
            Text = "YES",
            AutoSize = true,
            Location = new Point(16, 22),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        sec5Panel.Controls.Add(_rdoSubstrateCountYes);

        sec5Panel.Controls.Add(new Label
        {
            Text = "CATTRID (Attribute Name) =",
            AutoSize = true,
            Location = new Point(28, 44),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _txtSubstrateCountCattrid = new TextBox
        {
            Text = "SubstrateCount",
            Width = 90,
            Location = new Point(190, 42),
            Enabled = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        sec5Panel.Controls.Add(_txtSubstrateCountCattrid);

        sec5Panel.Controls.Add(new Label
        {
            Text = "SubstrateCount(Type) =",
            AutoSize = true,
            Location = new Point(28, 64),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _cmbSubstrateCountType = new ComboBox
        {
            Width = 90,
            Location = new Point(178, 62),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        _cmbSubstrateCountType.Items.AddRange(SecsTypes);
        _cmbSubstrateCountType.SelectedItem = "U1";
        sec5Panel.Controls.Add(_cmbSubstrateCountType);

        sec5Panel.Controls.Add(new Label
        {
            Text = "SubstrateCount(Value) =",
            AutoSize = true,
            Location = new Point(28, 86),
            ForeColor = Theme.ThemeHelper.TextDark
        });
        _txtSubstrateCountValue = new TextBox
        {
            Text = string.Empty,
            Width = 90,
            Location = new Point(178, 84),
            Enabled = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        sec5Panel.Controls.Add(_txtSubstrateCountValue);

        _rdoSubstrateCountNo = new RadioButton
        {
            Text = "NO",
            Checked = true,
            AutoSize = true,
            Location = new Point(16, 108),
            ForeColor = Theme.ThemeHelper.TextDark
        };
        sec5Panel.Controls.Add(_rdoSubstrateCountNo);

        _rdoSubstrateCountYes.CheckedChanged += (_, _) =>
        {
            bool on = _rdoSubstrateCountYes.Checked;
            _txtSubstrateCountCattrid.Enabled = on;
            _cmbSubstrateCountType.Enabled = on;
            _txtSubstrateCountValue.Enabled = on;
        };

        // ── Right Panel: Slot Grid (Slot1~Slot25  LotID / WaferID) ───────────
        _grid = new DataGridView
        {
            Location = new Point(392, 46),
            Size = new Size(358, 648),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersWidth = 62,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 24,
            RowTemplate = { Height = 22 },
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            EditMode = DataGridViewEditMode.EditOnEnter,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Microsoft JhengHei UI", 8.5F)
        };

        var colLot = new DataGridViewTextBoxColumn { Name = "LotID", HeaderText = "LotID", Width = 134 };
        var colWafer = new DataGridViewTextBoxColumn { Name = "WaferID", HeaderText = "WaferID", Width = 134 };
        _grid.Columns.Add(colLot);
        _grid.Columns.Add(colWafer);

        // 還原上次儲存的 Slot 值
        for (int i = 1; i <= SlotCount; i++)
        {
            int row = _grid.Rows.Add(s_savedSlots[i - 1].LotId, s_savedSlots[i - 1].WaferId);
            _grid.Rows[row].HeaderCell!.Value = $"Slot{i}";
        }

        Controls.Add(_grid);

        // ── Buttons ───────────────────────────────────────────────────────────
        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 80,
            Height = 30,
            Location = new Point(580, 700),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(120, 130, 140),
            ForeColor = Color.White
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(90, 100, 110);

        var btnSend = new Button
        {
            Text = "Send",
            Width = 80,
            Height = 30,
            Location = new Point(668, 700),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.ThemeHelper.CobaltBlue,
            ForeColor = Color.White
        };
        btnSend.FlatAppearance.BorderColor = Theme.ThemeHelper.DeepBlue;
        btnSend.Click += OnSendClick;

        Controls.Add(btnCancel);
        Controls.Add(btnSend);

        AcceptButton = btnSend;
        CancelButton = btnCancel;
    }

    private void OnSendClick(object? sender, EventArgs e)
    {
        CarrierId = _txtCarrierId.Text.Trim();
        PortId = (byte)_nudPort.Value;

        // 1) ContentMap
        IncludeContentMap = _rdoContentMapYes.Checked;
        ContentMapCattrid = _txtContentMapCattrid.Text.Trim();
        // 無論是否 IncludeContentMap，都先將 Grid 值存回 static 以便下次還原
        for (int i = 0; i < _grid.Rows.Count && i < SlotCount; i++)
        {
            s_savedSlots[i] = (
                _grid.Rows[i].Cells["LotID"].Value?.ToString() ?? string.Empty,
                _grid.Rows[i].Cells["WaferID"].Value?.ToString() ?? string.Empty);
        }

        if (IncludeContentMap)
        {
            var entries = new List<(string, string)>(SlotCount);
            var occupiedSlotIds = new List<string>(SlotCount);
            for (int slotIndex = 0; slotIndex < _grid.Rows.Count; slotIndex++)
            {
                var row = _grid.Rows[slotIndex];
                var lotId = row.Cells["LotID"].Value?.ToString() ?? string.Empty;
                var waferId = row.Cells["WaferID"].Value?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(lotId) && string.IsNullOrWhiteSpace(waferId))
                {
                    continue;
                }

                entries.Add((lotId, waferId));
                occupiedSlotIds.Add((slotIndex + 1).ToString());
            }
            SlotEntries = entries;
            OccupiedSlotIds = occupiedSlotIds;
        }
        else
        {
            SlotEntries = Array.Empty<(string, string)>();
            OccupiedSlotIds = Array.Empty<string>();
        }

        // 2) SlotMap
        IncludeSlotMap = _rdoSlotMapYes.Checked;
        SlotMapCattrid = _txtSlotMapCattrid.Text.Trim();
        SlotMapFormat1Empty3Correct = _rdoSlotMap1Empty3Correct.Checked;

        // 3) Usage
        IncludeUsage = _rdoUsageYes.Checked;
        UsageCattrid = _txtUsageCattrid.Text.Trim();
        UsageType = _cmbUsageType.SelectedItem?.ToString() ?? "A";
        UsageValue = _txtUsageValue.Text.Trim();

        // 4) Capacity
        IncludeCapacity = _rdoCapacityYes.Checked;
        CapacityCattrid = _txtCapacityCattrid.Text.Trim();
        CapacityType = _cmbCapacityType.SelectedItem?.ToString() ?? "U1";
        CapacityValue = _txtCapacityValue.Text.Trim();

        // 5) SubstrateCount
        IncludeSubstrateCount = _rdoSubstrateCountYes.Checked;
        SubstrateCountCattrid = _txtSubstrateCountCattrid.Text.Trim();
        SubstrateCountType = _cmbSubstrateCountType.SelectedItem?.ToString() ?? "U1";
        SubstrateCountValue = _txtSubstrateCountValue.Text.Trim();

        DialogResult = DialogResult.OK;
        Close();
    }
}