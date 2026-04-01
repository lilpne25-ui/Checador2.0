using System.Drawing.Drawing2D;

namespace Checador.Desktop;

public class RoundedGroupBox : GroupBox
{
    public int CornerRadius { get; set; } = 14;
    public Color BorderColor { get; set; } = Color.FromArgb(220, 228, 238);
    public float BorderThickness { get; set; } = 1.2f;
    public bool ShowShadow { get; set; } = true;
    public int ShadowDepth { get; set; } = 5;
    public int ShadowOpacity { get; set; } = 24;

    public RoundedGroupBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.UserPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.White;
        ForeColor = Color.FromArgb(24, 39, 58);
        Padding = new Padding(10);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);

        var text = Text ?? string.Empty;
        var textSize = TextRenderer.MeasureText(text, Font);
        var textRect = new Rectangle(14, 0, textSize.Width + 8, textSize.Height);

        var rect = new Rectangle(0, textRect.Height / 2, Width - 1, Height - textRect.Height / 2 - 1);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        if (ShowShadow)
        {
            DrawSoftShadow(e.Graphics, rect, CornerRadius, ShadowDepth, ShadowOpacity);
        }

        using var path = CreateRoundedPath(rect, CornerRadius);
        using var backBrush = new SolidBrush(BackColor);
        using var borderPen = new Pen(BorderColor, BorderThickness);

        e.Graphics.FillPath(backBrush, path);

        using (var eraseBrush = new SolidBrush(Parent?.BackColor ?? SystemColors.Control))
        {
            e.Graphics.FillRectangle(eraseBrush, textRect);
        }

        e.Graphics.DrawPath(borderPen, path);
        TextRenderer.DrawText(
            e.Graphics,
            text,
            Font,
            new Point(textRect.Left + 2, textRect.Top),
            ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
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

    private static void DrawSoftShadow(Graphics g, Rectangle rect, int radius, int depth, int opacity)
    {
        var levels = Math.Max(2, depth);
        for (var i = 1; i <= levels; i++)
        {
            var alpha = Math.Max(2, opacity - i * (opacity / levels));
            var shadowRect = new Rectangle(rect.X + i, rect.Y + i, rect.Width, rect.Height);
            using var shadowPath = CreateRoundedPath(shadowRect, radius);
            using var shadowBrush = new SolidBrush(Color.FromArgb(alpha, 40, 52, 72));
            g.FillPath(shadowBrush, shadowPath);
        }
    }
}
