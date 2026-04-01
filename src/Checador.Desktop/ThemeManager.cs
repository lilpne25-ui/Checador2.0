namespace Checador.Desktop;

public static class ThemeManager
{
    // Primarios
    public static readonly Color Primary = Color.FromArgb(0, 120, 212);
    public static readonly Color PrimaryHover = Color.FromArgb(16, 110, 190);
    public static readonly Color Success = Color.FromArgb(16, 124, 16);
    public static readonly Color Warning = Color.FromArgb(148, 108, 0);
    public static readonly Color Danger = Color.FromArgb(196, 43, 28);
    public static readonly Color Muted = Color.FromArgb(121, 119, 117);

    // Tema Claro
    public static readonly Color LightBg = Color.White;
    public static readonly Color LightBgSecondary = Color.FromArgb(246, 246, 249);
    public static readonly Color LightBgTertiary = Color.FromArgb(237, 237, 240);
    public static readonly Color LightText = Color.FromArgb(30, 30, 30);
    public static readonly Color LightTextSecond = Color.FromArgb(110, 110, 110);
    public static readonly Color LightBorder = Color.FromArgb(224, 224, 224);

    // Tema Oscuro
    public static readonly Color DarkBg = Color.FromArgb(32, 32, 32);
    public static readonly Color DarkBgSecondary = Color.FromArgb(44, 44, 44);
    public static readonly Color DarkBgTertiary = Color.FromArgb(56, 56, 56);
    public static readonly Color DarkText = Color.FromArgb(242, 242, 242);
    public static readonly Color DarkTextSecond = Color.FromArgb(160, 160, 160);
    public static readonly Color DarkBorder = Color.FromArgb(68, 68, 68);

    public static string CurrentTheme { get; private set; } = "Claro";
    public static bool IsDark => CurrentTheme == "Oscuro";
    public static event EventHandler? ThemeChanged;

    public static Color Bg => IsDark ? DarkBg : LightBg;
    public static Color BgSecondary => IsDark ? DarkBgSecondary : LightBgSecondary;
    public static Color BgTertiary => IsDark ? DarkBgTertiary : LightBgTertiary;
    public static Color Text => IsDark ? DarkText : LightText;
    public static Color TextSecond => IsDark ? DarkTextSecond : LightTextSecond;
    public static Color Border => IsDark ? DarkBorder : LightBorder;

    public static void Apply(string theme)
    {
        if (CurrentTheme == theme)
        {
            return;
        }

        CurrentTheme = theme;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void ApplyToForm(Form form)
    {
        form.BackColor = Bg;
        form.ForeColor = Text;
        ApplyToControls(form.Controls);
    }

    private static void ApplyToControls(Control.ControlCollection controls)
    {
        foreach (Control ctrl in controls)
        {
            switch (ctrl)
            {
                case TabPage tp:
                    tp.BackColor = Bg;
                    break;
                case TabControl tc:
                    tc.BackColor = Bg;
                    break;
                case Panel p when p.Name.StartsWith("pnlMetric") || p.Name.StartsWith("pnlCard"):
                    p.BackColor = BgSecondary;
                    break;
                case Panel p:
                    if (p.BackColor != Color.Transparent)
                    {
                        p.BackColor = Bg;
                    }
                    break;
                case Label lbl when lbl.Tag?.ToString() == "secondary":
                    lbl.ForeColor = TextSecond;
                    break;
                case Label lbl when lbl.Tag?.ToString() == "semantic":
                    break;
                case Label lbl:
                    lbl.ForeColor = Text;
                    break;
                case TextBox tb:
                    tb.BackColor = BgSecondary;
                    tb.ForeColor = Text;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case RichTextBox rtb:
                    rtb.BackColor = BgSecondary;
                    rtb.ForeColor = Text;
                    break;
                case DataGridView dgv:
                    ApplyToDgv(dgv);
                    break;
                case GroupBox gb:
                    gb.ForeColor = TextSecond;
                    break;
            }

            if (ctrl.HasChildren)
            {
                ApplyToControls(ctrl.Controls);
            }
        }
    }

    public static void ApplyToDgv(DataGridView dgv)
    {
        dgv.BackgroundColor = BgSecondary;
        dgv.GridColor = Border;
        dgv.DefaultCellStyle.BackColor = Bg;
        dgv.DefaultCellStyle.ForeColor = Text;
        dgv.DefaultCellStyle.SelectionBackColor = Primary;
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
        dgv.AlternatingRowsDefaultCellStyle.BackColor = BgSecondary;
        dgv.ColumnHeadersDefaultCellStyle.BackColor = BgTertiary;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        dgv.EnableHeadersVisualStyles = false;
    }
}
