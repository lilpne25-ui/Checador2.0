using System.Drawing.Drawing2D;

namespace Checador.Desktop;

public class StatusPillLabel : Label
{
    public Color PillBackColor { get; set; } = Color.FromArgb(228, 244, 255);
    public Color PillForeColor { get; set; } = Color.FromArgb(0, 91, 172);
    public int CornerRadius { get; set; } = 14;
    public Padding InternalPadding { get; set; } = new(10, 4, 10, 4);

    public StatusPillLabel()
    {
        AutoSize = false;
        Height = 28;
        TextAlign = ContentAlignment.MiddleCenter;
        Font = new Font("Segoe UI", 8.7F, FontStyle.Bold);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = BuildPath(rect, CornerRadius);
        using var back = new SolidBrush(PillBackColor);

        e.Graphics.FillPath(back, path);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(InternalPadding.Left, 0, Width - InternalPadding.Horizontal, Height),
            PillForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath BuildPath(Rectangle rect, int radius)
    {
        var r = Math.Max(1, radius);
        var d = r * 2;

        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
