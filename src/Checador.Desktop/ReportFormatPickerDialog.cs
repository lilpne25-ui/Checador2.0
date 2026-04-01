using Checador.Domain.Models;

namespace Checador.Desktop;

public sealed class ReportFormatPickerDialog : Form
{
    private ReportOutputFormat? _selectedFormat;

    public static ReportOutputFormat? ShowPicker(IWin32Window? owner)
    {
        using var dialog = new ReportFormatPickerDialog();
        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return result == DialogResult.OK ? dialog._selectedFormat : null;
    }

    private ReportFormatPickerDialog()
    {
        Text = "Generar reporte";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 300);
        BackColor = Color.FromArgb(245, 248, 253);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16, 14, 16, 14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var lblTitle = new Label
        {
            Text = "Selecciona el formato del reporte",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 45, 74),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var lblSubtitle = new Label
        {
            Text = "Ambas opciones usan los mismos datos procesados; cambia solo el formato final.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(92, 104, 123),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 0)
        };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        options.Controls.Add(CreateFormatCard(
            title: "PDF",
            description: "Formato bloqueado para compartir e imprimir.\nLa salida replica el mismo layout del Excel.",
            buttonText: "Generar PDF",
            isPrimary: true,
            format: ReportOutputFormat.Pdf), 0, 0);

        options.Controls.Add(CreateFormatCard(
            title: "Excel",
            description: "Archivo editable (.xlsx/.xlsm).\nConserva plantilla y celdas para ajustes manuales.",
            buttonText: "Generar Excel",
            isPrimary: false,
            format: ReportOutputFormat.Excel), 1, 0);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0, 8, 0, 0)
        };

        var btnCancel = new Button
        {
            Text = "Cancelar",
            Width = 112,
            Height = 30,
            DialogResult = DialogResult.Cancel
        };
        ButtonStyles.ApplySecondary(btnCancel);

        footer.Controls.Add(btnCancel);

        root.Controls.Add(lblTitle, 0, 0);
        root.Controls.Add(lblSubtitle, 0, 1);
        root.Controls.Add(options, 0, 2);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);

        CancelButton = btnCancel;
    }

    private Control CreateFormatCard(
        string title,
        string description,
        string buttonText,
        bool isPrimary,
        ReportOutputFormat format)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(isPrimary ? 0 : 8, 0, isPrimary ? 8 : 0, 0),
            Padding = new Padding(14)
        };

        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(218, 226, 237));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var lblTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI Semibold", 11.2f, FontStyle.Bold),
            ForeColor = Color.FromArgb(26, 47, 78),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var lblDescription = new Label
        {
            Dock = DockStyle.Fill,
            Text = description,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(82, 96, 120),
            TextAlign = ContentAlignment.TopLeft
        };

        var btnSelect = new Button
        {
            Text = buttonText,
            Dock = DockStyle.Fill,
            Height = 32
        };

        if (isPrimary)
        {
            ButtonStyles.ApplyPrimary(btnSelect);
        }
        else
        {
            ButtonStyles.ApplySecondary(btnSelect);
        }

        btnSelect.Click += (_, _) =>
        {
            _selectedFormat = format;
            DialogResult = DialogResult.OK;
            Close();
        };

        layout.Controls.Add(lblTitle, 0, 0);
        layout.Controls.Add(lblDescription, 0, 1);
        layout.Controls.Add(btnSelect, 0, 2);

        card.Controls.Add(layout);
        return card;
    }
}
