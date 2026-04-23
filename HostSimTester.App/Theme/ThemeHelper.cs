using System.Drawing;
using System.Windows.Forms;

namespace HostSimTester.App.Theme;

public static class ThemeHelper
{
    public static readonly Color IceSurface = Color.FromArgb(240, 245, 250);
    public static readonly Color NavyPanel = Color.FromArgb(31, 78, 121);
    public static readonly Color CobaltBlue = Color.FromArgb(41, 128, 185);
    public static readonly Color DeepBlue = Color.FromArgb(31, 97, 141);
    public static readonly Color DangerRed = Color.FromArgb(160, 60, 60);
    public static readonly Color TableBg = Color.FromArgb(248, 251, 254);
    public static readonly Color GroupHeaderBg = Color.FromArgb(214, 232, 248);
    public static readonly Color TextDark = Color.FromArgb(31, 78, 121);
    public static readonly Color TextMid = Color.FromArgb(44, 62, 80);
    public static readonly Color LogBg = Color.FromArgb(15, 28, 48);
    public static readonly Color LogText = Color.FromArgb(120, 200, 240);
    public static readonly Color LogWarn = Color.FromArgb(255, 168, 100);
    public static readonly Color LogError = Color.FromArgb(230, 120, 120);
    public static readonly Color StatusText = Color.FromArgb(189, 215, 238);

    public static void ApplyTheme(Form form)
    {
        form.BackColor = IceSurface;
        form.Font = new Font("Microsoft JhengHei UI", 9F);
    }

    public static void ApplyButtonTheme(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is Button btn)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = CobaltBlue;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = DeepBlue;
                btn.Font = new Font("Microsoft JhengHei UI", 8.5F);
                btn.Height = 30;
            }
        }
    }
}
