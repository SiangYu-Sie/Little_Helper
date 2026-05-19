using HostSimTester.App.Theme;

namespace HostSimTester.App.Dialogs;

public sealed class IssueReportDialog : Form
{
    private readonly TextBox _txtSubject;
    private readonly TextBox _txtDescription;
    private readonly CheckBox _chkIncludeLogs;

    public IssueReportDialogResult? Result { get; private set; }

    public IssueReportDialog(string apiUrl, string currentTab)
    {
        Text = "Issue Report";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 640;
        Height = 520;

        ThemeHelper.ApplyTheme(this);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 8
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Report API Endpoint:",
            ForeColor = ThemeHelper.TextMid,
            AutoSize = true
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(apiUrl) ? "(not configured)" : apiUrl,
            ForeColor = ThemeHelper.TextDark,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 8)
        }, 0, 1);

        layout.Controls.Add(new Label { Text = "Subject", ForeColor = ThemeHelper.TextMid, AutoSize = true }, 0, 2);
        _txtSubject = new TextBox
        {
            Dock = DockStyle.Top,
            Text = $"Issue on {currentTab}"
        };
        layout.Controls.Add(_txtSubject, 0, 3);

        layout.Controls.Add(new Label { Text = "Description", ForeColor = ThemeHelper.TextMid, AutoSize = true, Margin = new Padding(0, 10, 0, 4) }, 0, 4);
        _txtDescription = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = ThemeHelper.TableBg
        };
        layout.Controls.Add(_txtDescription, 0, 5);

        _chkIncludeLogs = new CheckBox
        {
            Text = "Include recent UI logs (last ~220 lines)",
            Checked = true,
            ForeColor = ThemeHelper.TextMid,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        layout.Controls.Add(_chkIncludeLogs, 0, 6);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 10, 0, 0)
        };
        var btnSend = new Button { Text = "Send", DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        buttonPanel.Controls.Add(btnSend);
        buttonPanel.Controls.Add(btnCancel);
        layout.Controls.Add(buttonPanel, 0, 7);

        Controls.Add(layout);
        ThemeHelper.ApplyButtonTheme(buttonPanel);

        AcceptButton = btnSend;
        CancelButton = btnCancel;

        btnSend.Click += (_, _) =>
        {
            var subject = _txtSubject.Text.Trim();
            var description = _txtDescription.Text.Trim();

            if (string.IsNullOrWhiteSpace(subject))
            {
                MessageBox.Show(this, "Please input subject.", "Issue Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show(this, "Please input description.", "Issue Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }

            Result = new IssueReportDialogResult
            {
                Subject = subject,
                Description = description,
                IncludeRecentLogs = _chkIncludeLogs.Checked
            };
        };
    }
}

public sealed class IssueReportDialogResult
{
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IncludeRecentLogs { get; init; }
}
