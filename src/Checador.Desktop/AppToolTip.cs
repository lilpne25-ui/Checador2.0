using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Checador.Desktop;

public class AppToolTip : ToolTip
{
    public AppToolTip()
    {
        Configure();
    }

    public AppToolTip(IContainer container)
        : base(container)
    {
        Configure();
    }

    private void Configure()
    {
        OwnerDraw = true;
        Draw += OnDraw;
        Popup += OnPopup;
        AutoPopDelay = 5000;
        InitialDelay = 500;
        ReshowDelay = 300;
        ShowAlways = true;
        UseAnimation = true;
        UseFading = false; // Evita flickering en WinForms.
    }

    private void OnPopup(object? sender, PopupEventArgs e)
    {
        var text = GetToolTip(e.AssociatedControl);
        using var font = new Font("Segoe UI", 9f);
        var size = TextRenderer.MeasureText(text, font);
        e.ToolTipSize = new Size(size.Width + 24, size.Height + 16);
    }

    private void OnDraw(object? sender, DrawToolTipEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool dark = ThemeManager.IsDark;
        var bg = dark ? Color.FromArgb(44, 44, 44) : Color.White;
        var border = dark ? Color.FromArgb(80, 80, 80) : Color.FromArgb(200, 200, 200);
        var text = dark ? Color.FromArgb(242, 242, 242) : Color.FromArgb(30, 30, 30);

        var rect = new RectangleF(0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);

        using var bgBrush = new SolidBrush(bg);
        using var borderPen = new Pen(border, 1f);
        using var path = RoundedRect(rect, 6);
        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);

        using var font = new Font("Segoe UI", 9f);
        using var textBrush = new SolidBrush(text);
        var textRect = new RectangleF(12, 8, rect.Width - 24, rect.Height - 16);
        g.DrawString(e.ToolTipText, font, textBrush, textRect);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        return path;
    }
}
