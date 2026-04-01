using Checador.Domain.Models;
using System.Drawing.Drawing2D;

namespace Checador.Desktop;

public sealed partial class KpiSummaryForm : Form
{
    private int _empleadosProcesados;
    private int _empleadosSinRegistro;
    private double _horasTotales;
    private double _horasExtra;
    private string _carpetaSalida = string.Empty;
    private string _resumenAdicional = string.Empty;
    private int _reportesGenerados;
    private bool _uiReady;

    public KpiSummaryForm(ReportRunSummary summary)
    {
        InitializeComponent();
        ThemeManager.ThemeChanged += (s, e) =>
        {
            if (IsHandleCreated)
            {
                Invoke(() => ThemeManager.ApplyToForm(this));
            }
        };
        Load += (_, _) => ThemeManager.ApplyToForm(this);
        ApplyVisualDefaults();
        MapFromSummary(summary);
        _uiReady = true;
        RefreshView();
    }

    public int EmpleadosProcesados
    {
        get => _empleadosProcesados;
        set
        {
            _empleadosProcesados = value;
            RefreshView();
        }
    }

    public int EmpleadosSinRegistro
    {
        get => _empleadosSinRegistro;
        set
        {
            _empleadosSinRegistro = value;
            RefreshView();
        }
    }

    public double HorasTotales
    {
        get => _horasTotales;
        set
        {
            _horasTotales = value;
            RefreshView();
        }
    }

    public double HorasExtra
    {
        get => _horasExtra;
        set
        {
            _horasExtra = value;
            RefreshView();
        }
    }

    public string CarpetaSalida
    {
        get => _carpetaSalida;
        set
        {
            _carpetaSalida = value ?? string.Empty;
            RefreshView();
        }
    }

    public string ResumenAdicional
    {
        get => _resumenAdicional;
        set
        {
            _resumenAdicional = value ?? string.Empty;
            RefreshView();
        }
    }

    private void ApplyVisualDefaults()
    {
        Text = "Resumen KPI de corrida";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;

        ButtonStyles.ApplyPrimary(btnClose);
        btnClose.DialogResult = DialogResult.OK;
        AcceptButton = btnClose;
        CancelButton = btnClose;

        picHeaderIcon.Paint += (_, e) => DrawHeaderStatusIcon(e.Graphics, picHeaderIcon.ClientRectangle);
    }

    private void MapFromSummary(ReportRunSummary summary)
    {
        EmpleadosProcesados = summary.EmployeesProcessed;
        EmpleadosSinRegistro = summary.EmployeesWithoutRecords;
        HorasTotales = (double)summary.TotalWorkedHours;
        HorasExtra = (double)summary.TotalExtraHours;
        _reportesGenerados = summary.ReportPaths.Count;
        CarpetaSalida = ResolveOutputFolder(summary.ReportPaths);
        ResumenAdicional = BuildDefaultSubtitle(summary.ReportPaths.Count);
    }

    private void RefreshView()
    {
        if (!_uiReady)
        {
            return;
        }

        lblHeaderSubtitle.Text = string.IsNullOrWhiteSpace(ResumenAdicional)
            ? "Resumen de la corrida actual."
            : ResumenAdicional;

        lblProcessedValue.Text = EmpleadosProcesados.ToString("0");
        lblMissingValue.Text = EmpleadosSinRegistro.ToString("0");
        lblWorkedValue.Text = HorasTotales.ToString("0.00");
        lblExtraValue.Text = HorasExtra.ToString("0.00");

        lblReportsValue.Text = _reportesGenerados.ToString("0");
        lblFolderValue.Text = string.IsNullOrWhiteSpace(CarpetaSalida) ? "N/D" : CarpetaSalida;
    }

    private static string ResolveOutputFolder(IReadOnlyList<string> reportPaths)
    {
        if (reportPaths.Count == 0)
        {
            return string.Empty;
        }

        return Path.GetDirectoryName(reportPaths[0]) ?? string.Empty;
    }

    private static string BuildDefaultSubtitle(int reportCount)
    {
        return reportCount > 0
            ? $"Se generaron {reportCount} reportes correctamente."
            : "La corrida terminó sin archivos generados.";
    }

    private static void DrawHeaderStatusIcon(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var diameter = Math.Min(bounds.Width, bounds.Height) - 4;
        var circle = new Rectangle(
            bounds.X + (bounds.Width - diameter) / 2,
            bounds.Y + (bounds.Height - diameter) / 2,
            diameter,
            diameter);

        using var brush = new SolidBrush(Color.FromArgb(0x10, 0x7C, 0x10));
        using var pen = new Pen(Color.White, 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        g.FillEllipse(brush, circle);

        var x1 = circle.Left + circle.Width * 0.28f;
        var y1 = circle.Top + circle.Height * 0.54f;
        var x2 = circle.Left + circle.Width * 0.45f;
        var y2 = circle.Top + circle.Height * 0.70f;
        var x3 = circle.Left + circle.Width * 0.74f;
        var y3 = circle.Top + circle.Height * 0.36f;

        g.DrawLines(pen, new[]
        {
            new PointF(x1, y1),
            new PointF(x2, y2),
            new PointF(x3, y3)
        });
    }
}
