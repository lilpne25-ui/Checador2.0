using Checador.Infrastructure.Access;
using System.Drawing.Drawing2D;

namespace Checador.Desktop;

public sealed class PervasivePreviewForm : Form
{
    private readonly DataGridView _grid;

    public PervasivePreviewForm(IReadOnlyList<PervasivePayloadPreviewItem> data)
    {
        ThemeManager.ThemeChanged += (s, e) =>
        {
            if (IsHandleCreated)
            {
                Invoke(() => ThemeManager.ApplyToForm(this));
            }
        };
        Load += (_, _) => ThemeManager.ApplyToForm(this);

        Text = "Preview completo · Payload Pervasive";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1300;
        Height = 760;
        MinimumSize = new Size(1100, 620);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var inserts = data.Count(x => x.Action == "INSERT");
        var updates = data.Count(x => x.Action == "UPDATE");
        var skips = data.Count(x => x.Action == "SKIP");
        var guardSkips = data.Count(x => x.Action == "SKIP_GUARD");
        var abortGuards = data.Count(x => x.Action == "ABORT_GUARD");

        var summary = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"Total filas: {data.Count}   |   INSERT: {inserts}   |   UPDATE: {updates}   |   SKIP: {skips}   |   SKIP_GUARD: {guardSkips}   |   ABORT_GUARD: {abortGuards}"
        };

        var legend = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(8),
            Margin = new Padding(0)
        };
        legend.Controls.Add(CreateLegendChip("INSERT", Color.FromArgb(223, 246, 221)));
        legend.Controls.Add(CreateLegendChip("UPDATE", Color.FromArgb(255, 244, 206)));
        legend.Controls.Add(CreateLegendChip("SKIP", Color.FromArgb(243, 242, 241)));
        legend.Controls.Add(CreateLegendChip("ABORT", Color.FromArgb(253, 231, 233)));

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            RowHeadersVisible = false,
            DataSource = data.ToList()
        };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);
        _grid.CellFormatting += dgvPervasive_CellFormatting;

        ConfigureColumns();

        var btnClose = new Button
        {
            Text = "Cerrar",
            Width = 100,
            Height = 32,
            Anchor = AnchorStyles.Right,
            DialogResult = DialogResult.OK
        };

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        footer.Controls.Add(btnClose);

        root.Controls.Add(summary, 0, 0);
        root.Controls.Add(legend, 0, 1);
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(footer, 0, 3);

        AcceptButton = btnClose;
        CancelButton = btnClose;
    }

    private void dgvPervasive_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        if (!_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.Action)))
        {
            return;
        }

        var row = _grid.Rows[e.RowIndex];
        var op = row.Cells[nameof(PervasivePayloadPreviewItem.Action)].Value?.ToString()?.Trim().ToUpperInvariant() ?? string.Empty;

        switch (op)
        {
            case "INSERT":
                row.DefaultCellStyle.BackColor = Color.FromArgb(223, 246, 221);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(16, 124, 16);
                break;
            case "UPDATE":
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 244, 206);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(148, 108, 0);
                break;
            case "SKIP":
            case "SKIP_GUARD":
                row.DefaultCellStyle.BackColor = Color.FromArgb(243, 242, 241);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
                break;
            case "ABORT_GUARD":
                row.DefaultCellStyle.BackColor = Color.FromArgb(253, 231, 233);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(168, 0, 0);
                break;
            default:
                row.DefaultCellStyle.BackColor = Color.Empty;
                row.DefaultCellStyle.ForeColor = Color.Empty;
                break;
        }
    }

    private static Control CreateLegendChip(string text, Color color)
    {
        var chip = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 16, 0),
            Padding = new Padding(0)
        };

        var dot = new Panel
        {
            Width = 12,
            Height = 12,
            Margin = new Padding(0, 3, 6, 0)
        };
        dot.Paint += (_, args) =>
        {
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            args.Graphics.FillEllipse(brush, 0, 0, 11, 11);
        };

        var label = new Label
        {
            AutoSize = true,
            Text = text,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(52, 64, 82)
        };

        chip.Controls.Add(dot);
        chip.Controls.Add(label);
        return chip;
    }

    private void ConfigureColumns()
    {
        if (_grid.Columns.Count == 0)
        {
            return;
        }

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.UserId)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.UserId)].HeaderText = "USERID";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.Action)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.Action)].HeaderText = "Acción";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.Employee)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.Employee)].HeaderText = "EMPLOYEE";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.EmployeeName)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.EmployeeName)].HeaderText = "NAME";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.ChargeDate)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.ChargeDate)].HeaderText = "CHARGE_DATE";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.InTsMin)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.InTsMin)].HeaderText = "IN_TS_MIN";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.OutTsMin)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.OutTsMin)].HeaderText = "OUT_TS_MIN";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.DateIn)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.DateIn)].HeaderText = "DATE_IN";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.TimeIn)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.TimeIn)].HeaderText = "TIME_IN";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.DateOut)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.DateOut)].HeaderText = "DATE_OUT";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.TimeOut)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.TimeOut)].HeaderText = "TIME_OUT";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.Dept)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.Dept)].HeaderText = "DEPT";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.Shift)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.Shift)].HeaderText = "SHIFT";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.ClockedIn)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.ClockedIn)].HeaderText = "CLOCKED_IN";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.LastChangeProgram)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.LastChangeProgram)].HeaderText = "LAST_CHG_PROG";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.ActualDate)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.ActualDate)].HeaderText = "ACTUAL_DATE";

        if (_grid.Columns.Contains(nameof(PervasivePayloadPreviewItem.Notes)))
            _grid.Columns[nameof(PervasivePayloadPreviewItem.Notes)].HeaderText = "Notas";
    }
}
