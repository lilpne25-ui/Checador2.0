using Checador.Domain.Models;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinForms;
using SkiaSharp;

namespace Checador.Desktop;

public sealed class ReportPreviewForm : Form
{
    private readonly DataGridView _grid;
    private readonly CartesianChart chartHoras;

    public List<(string Fecha, double Horas)> DatosHorasPorDia { get; set; } = new();

    public ReportPreviewForm(ReportPreviewResult preview)
    {
        DatosHorasPorDia = preview.Days
            .Select(d => (d.Date.ToString("dd/MM"), Convert.ToDouble(d.HorasTrabajadas ?? 0m)))
            .ToList();

        Text = $"Vista previa · {preview.UserId} - {preview.EmployeeName}";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1060;
        Height = 700;
        MinimumSize = new Size(980, 620);
        BackColor = Color.FromArgb(244, 247, 252);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var header = new RoundedGroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Resumen para Recursos Humanos",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6)
        };
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblEmployee = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            Padding = new Padding(8),
            Text =
                $"Empleado {preview.UserId} · {preview.EmployeeName}{Environment.NewLine}" +
                $"Periodo: {preview.FechaInicio:dd-MM-yyyy} al {preview.FechaFin:dd-MM-yyyy}"
        };

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };

        cards.Controls.Add(CreateCard("Empresa", preview.Company ?? "Sin dato", Color.FromArgb(232, 242, 252), Color.FromArgb(32, 73, 124)));
        cards.Controls.Add(CreateCard("Area", preview.Area ?? "Sin dato", Color.FromArgb(236, 249, 241), Color.FromArgb(22, 100, 47)));
        cards.Controls.Add(CreateCard("Puesto", preview.Position ?? "Sin dato", Color.FromArgb(255, 245, 228), Color.FromArgb(140, 95, 16)));
        cards.Controls.Add(CreateCard("Horas semana", preview.TotalWorkedHours.ToString("0.00"), Color.FromArgb(241, 242, 255), Color.FromArgb(62, 64, 149)));
        cards.Controls.Add(CreateCard("Horas extra", preview.TotalExtraHours.ToString("0.00"), Color.FromArgb(255, 236, 244), Color.FromArgb(140, 37, 102)));

        headerLayout.Controls.Add(lblEmployee, 0, 0);
        headerLayout.Controls.Add(cards, 0, 1);
        header.Controls.Add(headerLayout);
        root.Controls.Add(header, 0, 0);

        var chartHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0)
        };

        chartHoras = new CartesianChart
        {
            Name = "chartHoras",
            Size = new Size(Width - 40, 120),
            Height = 120,
            Dock = DockStyle.None,
            Margin = new Padding(0, 8, 0, 8),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = ThemeManager.Bg,
            Location = new Point(0, 8)
        };
        chartHost.Controls.Add(chartHoras);
        chartHost.Resize += (_, _) =>
        {
            chartHoras.Width = Math.Max(280, chartHost.ClientSize.Width);
        };

        ThemeManager.ThemeChanged += (s, e) =>
        {
            if (IsHandleCreated)
            {
                Invoke(() =>
                {
                    ThemeManager.ApplyToForm(this);
                    chartHoras.BackColor = ThemeManager.Bg;
                    CargarChart();
                });
            }
        };
        Load += (_, _) =>
        {
            ThemeManager.ApplyToForm(this);
            CargarChart();
        };

        root.Controls.Add(chartHost, 0, 1);

        var gridContainer = new RoundedGroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Detalle diario",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            GridColor = Color.FromArgb(230, 236, 245)
        };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 246, 252);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 45, 66);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 34;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.DefaultCellStyle.ForeColor = Color.FromArgb(38, 49, 66);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(227, 239, 255);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 30, 45);
        _grid.RowTemplate.Height = 30;

        _grid.DataSource = preview.Days.Select(d => new
        {
            Fecha = d.Date.ToDateTime(TimeOnly.MinValue),
            Entrada = d.Entrada?.ToString("HH:mm") ?? "-",
            SalidaComida = d.SalidaComida?.ToString("HH:mm") ?? "-",
            EntradaComida = d.EntradaComida?.ToString("HH:mm") ?? "-",
            Salida = d.Salida?.ToString("HH:mm") ?? "-",
            Horas = d.HorasTrabajadas,
            Extra = d.HorasExtra,
            Estado = d.Estatus,
            Nota = d.Observaciones ?? string.Empty
        }).ToList();

        if (_grid.Columns.Contains("Fecha"))
        {
            _grid.Columns["Fecha"].DefaultCellStyle.Format = "dd-MM-yyyy";
            _grid.Columns["Fecha"].HeaderText = "Fecha";
            _grid.Columns["Fecha"].FillWeight = 90;
        }
        if (_grid.Columns.Contains("Entrada")) _grid.Columns["Entrada"].HeaderText = "Entrada";
        if (_grid.Columns.Contains("SalidaComida")) _grid.Columns["SalidaComida"].HeaderText = "Salida comida";
        if (_grid.Columns.Contains("EntradaComida")) _grid.Columns["EntradaComida"].HeaderText = "Regreso comida";
        if (_grid.Columns.Contains("Salida")) _grid.Columns["Salida"].HeaderText = "Salida";
        if (_grid.Columns.Contains("Horas"))
        {
            _grid.Columns["Horas"].HeaderText = "Horas trabajadas";
            _grid.Columns["Horas"].DefaultCellStyle.Format = "0.00";
            _grid.Columns["Horas"].FillWeight = 90;
        }
        if (_grid.Columns.Contains("Extra"))
        {
            _grid.Columns["Extra"].HeaderText = "Horas extra";
            _grid.Columns["Extra"].DefaultCellStyle.Format = "0.00";
            _grid.Columns["Extra"].FillWeight = 90;
        }
        if (_grid.Columns.Contains("Estado"))
        {
            _grid.Columns["Estado"].HeaderText = "Estado";
            _grid.Columns["Estado"].FillWeight = 95;
        }
        if (_grid.Columns.Contains("Nota"))
        {
            _grid.Columns["Nota"].HeaderText = "Observaciones";
            _grid.Columns["Nota"].FillWeight = 180;
        }

        _grid.CellFormatting += (_, e) =>
        {
            if (!_grid.Columns.Contains("Estado") || e.RowIndex < 0)
            {
                return;
            }

            var estado = Convert.ToString(_grid.Rows[e.RowIndex].Cells["Estado"].Value) ?? string.Empty;
            if (estado.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(238, 250, 241);
            }
            else if (estado.Equals("SIN_REGISTRO", StringComparison.OrdinalIgnoreCase))
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 246, 231);
            }
            else if (estado.Equals("INCOMPLETO", StringComparison.OrdinalIgnoreCase))
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 236, 236);
            }
        };

        gridContainer.Controls.Add(_grid);
        root.Controls.Add(gridContainer, 0, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnClose = new Button
        {
            Text = "Cerrar",
            Width = 100,
            Height = 30,
            DialogResult = DialogResult.OK
        };
        footer.Controls.Add(btnClose);
        root.Controls.Add(footer, 0, 3);

        AcceptButton = btnClose;
    }

    private void CargarChart()
    {
        if (DatosHorasPorDia == null || DatosHorasPorDia.Count == 0)
        {
            chartHoras.Visible = false;
            return;
        }

        chartHoras.Visible = true;

        var valores = DatosHorasPorDia.Select(d => d.Horas).ToArray();
        var etiquetas = DatosHorasPorDia.Select(d => d.Fecha).ToArray();

        var barColor = ThemeManager.IsDark
            ? SKColor.Parse("#378ADD")
            : SKColor.Parse("#0078D4");

        chartHoras.Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = valores,
                Fill = new SolidColorPaint(barColor),
                Stroke = null,
                MaxBarWidth = 28,
                Padding = 2,
                DataLabelsSize = 9,
                DataLabelsPaint = new SolidColorPaint(
                    ThemeManager.IsDark ? SKColors.LightGray : SKColors.Gray),
                DataLabelsPosition = DataLabelsPosition.Top,
                DataLabelsFormatter = p => p.Coordinate.PrimaryValue > 0
                    ? p.Coordinate.PrimaryValue.ToString("N1")
                    : string.Empty
            }
        };

        chartHoras.XAxes = new[]
        {
            new Axis
            {
                Labels = etiquetas,
                TextSize = 9,
                LabelsPaint = new SolidColorPaint(
                    ThemeManager.IsDark ? SKColors.LightGray : SKColors.Gray),
                TicksPaint = null,
                SeparatorsPaint = null
            }
        };

        chartHoras.YAxes = new[]
        {
            new Axis
            {
                IsVisible = false,
                MinLimit = 0
            }
        };

        chartHoras.LegendPosition = LegendPosition.Hidden;
        chartHoras.TooltipPosition = TooltipPosition.Hidden;
        chartHoras.BackColor = ThemeManager.Bg;
    }

    private static Panel CreateCard(string title, string value, Color back, Color fore)
    {
        var card = new Panel
        {
            Width = 188,
            Height = 58,
            Margin = new Padding(0, 0, 10, 0),
            BackColor = back
        };

        var lblTitle = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 20,
            Padding = new Padding(8, 4, 8, 0),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(70, 82, 102),
            Tag = "secondary",
            Text = title
        };

        var lblValue = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 0, 8, 6),
            Font = new Font("Segoe UI Semibold", 9.4F, FontStyle.Bold),
            ForeColor = fore,
            Tag = "semantic",
            Text = value
        };

        card.Controls.Add(lblValue);
        card.Controls.Add(lblTitle);
        return card;
    }
}
