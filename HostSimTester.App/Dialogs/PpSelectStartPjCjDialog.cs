using System.Drawing;
using System.Text.RegularExpressions;

namespace HostSimTester.App.Dialogs;

public sealed class PpSelectStartPjCjDialog : Form
{
    public string PpSelectRcmd { get; private set; } = "PP-SELECT";
    public string PpStartRcmd { get; private set; } = "SLOTMAP-L";
    public IReadOnlyList<(string Name, string Type, string Value)> PpSelectCpItems { get; private set; } = Array.Empty<(string, string, string)>();
    public IReadOnlyList<(string Name, string Type, string Value)> PpStartCpItems { get; private set; } = Array.Empty<(string, string, string)>();

    public string ProcessJobId { get; private set; } = string.Empty;
    public string Ppid { get; private set; } = string.Empty;
    public string CarrierId { get; private set; } = string.Empty;
    public string SlotIdList { get; private set; } = string.Empty;
    public string ControlJobId { get; private set; } = string.Empty;
    public string ProcessOrderMgmt { get; private set; } = "2";

    private readonly TextBox _txtPjId;
    private readonly TextBox _txtPpid;
    private readonly TextBox _txtCarrierId;
    private readonly TextBox _txtSlotIdList;
    private readonly TextBox _txtCjId;
    private readonly TextBox _txtProcessOrder;
    private readonly TextBox _txtPpSelectRcmd;
    private readonly TextBox _txtPpStartRcmd;
    private readonly DataGridView _gridPpSelectCp;
    private readonly DataGridView _gridPpStartCp;

    public PpSelectStartPjCjDialog(
        string carrierId,
        string processJobId,
        string ppid,
        string controlJobId,
        IEnumerable<string>? defaultSlotIds = null)
    {
        Text = "PPSELECT/START or PJ/CJ Creation";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(790, 650);
        BackColor = Theme.ThemeHelper.IceSurface;
        Font = new Font("Microsoft JhengHei UI", 9F);

        var tab = new TabControl
        {
            Location = new Point(0, 0),
            Size = new Size(790, 604),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        var tabPpSelectStart = new TabPage("PPSELECT/START")
        {
            BackColor = Theme.ThemeHelper.IceSurface
        };

        var tabPjCj = new TabPage("PJ/CJ")
        {
            BackColor = Theme.ThemeHelper.IceSurface
        };

        tab.TabPages.Add(tabPpSelectStart);
        tab.TabPages.Add(tabPjCj);
        Controls.Add(tab);

        BuildPpSelectStartTab(tabPpSelectStart, out _txtPpSelectRcmd, out _gridPpSelectCp, out _txtPpStartRcmd, out _gridPpStartCp);

        AddCpRow(_gridPpSelectCp, "LOADPORT-ID", "A", "'1'");
        AddCpRow(_gridPpSelectCp, "RECIPE-ID", "A", ppid);
        AddCpRow(_gridPpStartCp, "LOADPORT-ID", "A", "'1'");

        var grpProcess = new GroupBox
        {
            Text = "Create a Process Job",
            Location = new Point(10, 8),
            Size = new Size(760, 170),
            ForeColor = Theme.ThemeHelper.TextDark
        };

        grpProcess.Controls.Add(new Label { Text = "PRJOBID(Process Job ID) :", AutoSize = true, Location = new Point(12, 35), ForeColor = Theme.ThemeHelper.TextDark });
        _txtPjId = new TextBox { Location = new Point(160, 32), Width = 200, Text = processJobId };
        grpProcess.Controls.Add(_txtPjId);

        grpProcess.Controls.Add(new Label { Text = "PPID :", AutoSize = true, Location = new Point(12, 68), ForeColor = Theme.ThemeHelper.TextDark });
        _txtPpid = new TextBox { Location = new Point(160, 65), Width = 200, Text = ppid };
        grpProcess.Controls.Add(_txtPpid);

        grpProcess.Controls.Add(new Label { Text = "CarrierID :", AutoSize = true, Location = new Point(12, 101), ForeColor = Theme.ThemeHelper.TextDark });
        _txtCarrierId = new TextBox { Location = new Point(160, 98), Width = 200, Text = carrierId, BackColor = Color.FromArgb(180, 230, 240) };
        grpProcess.Controls.Add(_txtCarrierId);

        grpProcess.Controls.Add(new Label { Text = "SlotID List :", AutoSize = true, Location = new Point(12, 134), ForeColor = Theme.ThemeHelper.TextDark });
        _txtSlotIdList = new TextBox { Location = new Point(160, 131), Width = 260, Text = FormatSlotIdList(defaultSlotIds), BackColor = Color.FromArgb(180, 230, 240) };
        grpProcess.Controls.Add(_txtSlotIdList);

        var grpControl = new GroupBox
        {
            Text = "Create a Control Job",
            Location = new Point(10, 186),
            Size = new Size(760, 130),
            ForeColor = Theme.ThemeHelper.TextDark
        };

        grpControl.Controls.Add(new Label { Text = "OBJSPEC :", AutoSize = true, Location = new Point(12, 33), ForeColor = Theme.ThemeHelper.TextDark });
        grpControl.Controls.Add(new TextBox { Location = new Point(160, 30), Width = 200, Text = "Equipment", ReadOnly = true });

        grpControl.Controls.Add(new Label { Text = "ControlJobID (CJID) :", AutoSize = true, Location = new Point(12, 66), ForeColor = Theme.ThemeHelper.TextDark });
        _txtCjId = new TextBox { Location = new Point(160, 63), Width = 200, Text = controlJobId };
        grpControl.Controls.Add(_txtCjId);

        grpControl.Controls.Add(new Label { Text = "ProcessOrderMgmt :", AutoSize = true, Location = new Point(12, 99), ForeColor = Theme.ThemeHelper.TextDark });
        _txtProcessOrder = new TextBox { Location = new Point(160, 96), Width = 200, Text = "2" };
        grpControl.Controls.Add(_txtProcessOrder);

        tabPjCj.Controls.Add(grpProcess);
        tabPjCj.Controls.Add(grpControl);

        var bottomStrip = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = Theme.ThemeHelper.IceSurface
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 90,
            Height = 28,
            Location = new Point(594, 8)
        };

        var btnSend = new Button
        {
            Text = "Send",
            Width = 90,
            Height = 28,
            Location = new Point(690, 8)
        };
        btnSend.Click += (_, _) => OnSend();

        bottomStrip.Controls.Add(btnCancel);
        bottomStrip.Controls.Add(btnSend);
        Controls.Add(bottomStrip);
        Controls.Add(tab);

        AcceptButton = btnSend;
        CancelButton = btnCancel;
    }

    public IReadOnlyList<string> GetSlotIds()
    {
        var source = _txtSlotIdList.Text.Trim();
        if (string.IsNullOrWhiteSpace(source))
        {
            return Array.Empty<string>();
        }

        var matches = Regex.Matches(source, @"\d+")
            .Select(m => m.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .ToArray();

        return matches;
    }

    private static void BuildPpSelectStartTab(
        TabPage tab,
        out TextBox txtPpSelectRcmd,
        out DataGridView outGridPpSelectCp,
        out TextBox txtPpStartRcmd,
        out DataGridView outGridPpStartCp)
    {
        var left = new GroupBox
        {
            Text = "1) PP-SELECT",
            Location = new Point(6, 8),
            Size = new Size(380, 520),
            ForeColor = Theme.ThemeHelper.TextDark
        };

        left.Controls.Add(new Label { Text = "RCMD :", AutoSize = true, Location = new Point(24, 34), ForeColor = Theme.ThemeHelper.TextDark });
        txtPpSelectRcmd = new TextBox { Location = new Point(94, 30), Width = 200, Text = "PP-SELECT" };
        left.Controls.Add(txtPpSelectRcmd);

        var gridPpSelectCp = BuildCpGrid(new Point(16, 72), new Size(344, 390));
        left.Controls.Add(gridPpSelectCp);
        var btnAddPpSelect = new Button { Text = "Add New Item", Width = 120, Height = 28, Location = new Point(118, 470) };
        btnAddPpSelect.Click += (_, _) => AddCpRow(gridPpSelectCp, string.Empty, "A", string.Empty);
        left.Controls.Add(btnAddPpSelect);

        var right = new GroupBox
        {
            Text = "2) PP-START",
            Location = new Point(392, 8),
            Size = new Size(380, 520),
            ForeColor = Theme.ThemeHelper.TextDark
        };

        right.Controls.Add(new Label { Text = "RCMD :", AutoSize = true, Location = new Point(24, 34), ForeColor = Theme.ThemeHelper.TextDark });
        txtPpStartRcmd = new TextBox { Location = new Point(94, 30), Width = 200, Text = "SLOTMAP-L" };
        right.Controls.Add(txtPpStartRcmd);

        var gridPpStartCp = BuildCpGrid(new Point(16, 72), new Size(344, 390));
        right.Controls.Add(gridPpStartCp);
        var btnAddPpStart = new Button { Text = "Add New Item", Width = 120, Height = 28, Location = new Point(118, 470) };
        btnAddPpStart.Click += (_, _) => AddCpRow(gridPpStartCp, string.Empty, "A", string.Empty);
        right.Controls.Add(btnAddPpStart);

        tab.Controls.Add(left);
        tab.Controls.Add(right);

        outGridPpSelectCp = gridPpSelectCp;
        outGridPpStartCp = gridPpStartCp;
    }

    private static DataGridView BuildCpGrid(Point location, Size size)
    {
        var grid = new DataGridView
        {
            Location = location,
            Size = size,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            EditMode = DataGridViewEditMode.EditOnEnter,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft JhengHei UI", 8.5F)
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CPName", HeaderText = "CPNAME", Width = 120 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CPType", HeaderText = "CPVAL(Type)", Width = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CPValue", HeaderText = "CPValue", Width = 120 });
        return grid;
    }

    private static void AddCpRow(DataGridView grid, string name, string type, string value)
    {
        grid.Rows.Add(name, type, value);
    }

    private static string FormatSlotIdList(IEnumerable<string>? slotIds)
    {
        var normalizedSlotIds = (slotIds ?? Array.Empty<string>())
            .Select(slotId => slotId.Trim())
            .Where(slotId => !string.IsNullOrWhiteSpace(slotId))
            .Distinct()
            .ToArray();

        return "[" + string.Join(",", normalizedSlotIds.Select(slotId => $"\"{slotId}\"")) + "]";
    }

    private static IReadOnlyList<(string Name, string Type, string Value)> CollectCpItems(DataGridView grid)
    {
        var list = new List<(string Name, string Type, string Value)>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var name = row.Cells["CPName"].Value?.ToString()?.Trim() ?? string.Empty;
            var type = row.Cells["CPType"].Value?.ToString()?.Trim() ?? string.Empty;
            var value = row.Cells["CPValue"].Value?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            list.Add((name, type, value));
        }

        return list;
    }

    private void OnSend()
    {
        CarrierId = _txtCarrierId.Text.Trim();
        ProcessJobId = _txtPjId.Text.Trim();
        Ppid = _txtPpid.Text.Trim();
        SlotIdList = _txtSlotIdList.Text.Trim();
        ControlJobId = _txtCjId.Text.Trim();
        ProcessOrderMgmt = _txtProcessOrder.Text.Trim();
        PpSelectRcmd = _txtPpSelectRcmd.Text.Trim();
        PpStartRcmd = _txtPpStartRcmd.Text.Trim();
        PpSelectCpItems = CollectCpItems(_gridPpSelectCp);
        PpStartCpItems = CollectCpItems(_gridPpStartCp);

        if (string.IsNullOrWhiteSpace(CarrierId))
        {
            MessageBox.Show(this, "CarrierID is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
