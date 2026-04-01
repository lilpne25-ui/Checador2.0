using Checador.Infrastructure.Access;

namespace Checador.Desktop;

public sealed class RenameEmployeeForm : Form
{
    public string EmpleadoId { get; set; } = string.Empty;
    public string NombreActual { get; set; } = string.Empty;
    public string NombreDispositivo { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public string Puesto { get; set; } = string.Empty;
    public string PervasiveDept { get; set; } = string.Empty;
    public string PervasiveShift { get; set; } = string.Empty;
    public string CodigoHorario { get; set; } = string.Empty;
    public bool AutorizaHorasExtra { get; set; }

    public string NuevoNombre { get; set; } = string.Empty;

    private readonly Label _lblHeaderTitle;
    private readonly Label _lblHeaderSubtitle;
    private readonly TextBox _txtNombreSql;
    private readonly TextBox _txtNombreDispositivo;
    private readonly TextBox _txtDepartamento;
    private readonly TextBox _txtPuesto;
    private readonly ComboBox _cmbCodigoHorario;
    private readonly CheckBox _chkAutorizaHorasExtra;
    private readonly ComboBox _cmbPervasiveDept;
    private readonly TextBox _txtPervasiveDeptCode;
    private readonly ComboBox _cmbPervasiveShift;
    private readonly ToolTip _toolTip;
    private readonly Button _btnGuardar;
    private readonly Button _btnCancelar;
    private readonly List<ComboOption> _scheduleOptions = new();

    public RenameEmployeeForm()
    {
        Text = "Renombrar empleado";
        Width = 480;
        Height = 460;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Color.White;

        _toolTip = new ToolTip();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        _lblHeaderTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 39, 58),
            Text = "Empleado"
        };
        _lblHeaderSubtitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(104, 114, 128),
            Text = "Actualizar datos del catálogo"
        };
        headerPanel.Controls.Add(_lblHeaderSubtitle);
        headerPanel.Controls.Add(_lblHeaderTitle);
        root.Controls.Add(headerPanel, 0, 0);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        root.Controls.Add(tabs, 0, 1);

        var tabIdentidad = new TabPage("Identidad") { Padding = new Padding(12) };
        var tabHorario = new TabPage("Horario") { Padding = new Padding(12) };
        var tabPervasive = new TabPage("Pervasive") { Padding = new Padding(12) };
        tabs.TabPages.AddRange(new[] { tabIdentidad, tabHorario, tabPervasive });

        _txtNombreSql = CreateTextBox();
        _txtNombreDispositivo = CreateTextBox();
        _txtDepartamento = CreateTextBox();
        _txtPuesto = CreateTextBox();
        _cmbCodigoHorario = CreateComboBox();
        _chkAutorizaHorasExtra = new CheckBox
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Text = "Autoriza horas extra"
        };
        _cmbPervasiveDept = CreateComboBox();
        _txtPervasiveDeptCode = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 30,
            ReadOnly = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        _cmbPervasiveShift = CreateComboBox();

        tabIdentidad.Controls.Add(CreateIdentityLayout());
        tabHorario.Controls.Add(CreateHorarioLayout());
        tabPervasive.Controls.Add(CreatePervasiveLayout());

        var footerPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        var footerButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 220,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        _btnGuardar = new Button
        {
            Text = "Guardar",
            Width = 96,
            Height = 34,
            Margin = new Padding(8, 6, 0, 0),
            DialogResult = DialogResult.OK
        };
        ButtonStyles.ApplyPrimary(_btnGuardar);
        _btnGuardar.Click += (_, _) => SaveOutput();

        _btnCancelar = new Button
        {
            Text = "Cancelar",
            Width = 96,
            Height = 34,
            Margin = new Padding(0, 6, 0, 0),
            DialogResult = DialogResult.Cancel
        };
        ButtonStyles.ApplySecondary(_btnCancelar);

        footerButtons.Controls.Add(_btnGuardar);
        footerButtons.Controls.Add(_btnCancelar);
        footerPanel.Controls.Add(footerButtons);
        root.Controls.Add(footerPanel, 0, 2);

        _cmbPervasiveDept.SelectedIndexChanged += (_, _) => SyncPervasiveDeptCode();

        _toolTip.SetToolTip(_cmbCodigoHorario, "Administra los horarios desde Admin SQL → Horarios");
        AcceptButton = _btnGuardar;
        CancelButton = _btnCancelar;
    }

    public void SetHorarioOptions(IReadOnlyList<WorkScheduleItem> schedules)
    {
        _scheduleOptions.Clear();
        _scheduleOptions.Add(new ComboOption(string.Empty, "(sin seleccionar)"));
        foreach (var item in schedules.OrderBy(x => x.Code))
        {
            _scheduleOptions.Add(new ComboOption(
                item.Code.ToString(),
                $"{item.Code} - {item.Name} ({item.StartTime:hh\\:mm}-{item.EndTime:hh\\:mm}, {item.StandardHours:0.##}h)"));
        }

        var selectedCode = CodigoHorario?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(selectedCode) && _scheduleOptions.All(x => !string.Equals(x.Code, selectedCode, StringComparison.OrdinalIgnoreCase)))
        {
            _scheduleOptions.Add(new ComboOption(selectedCode, $"{selectedCode} - Actual (no catalogado)"));
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        _lblHeaderTitle.Text = $"{EmpleadoId} · {NombreActual}";
        _txtNombreSql.Text = NombreActual ?? string.Empty;
        _txtNombreDispositivo.Text = NombreDispositivo ?? string.Empty;
        _txtDepartamento.Text = Departamento ?? string.Empty;
        _txtPuesto.Text = Puesto ?? string.Empty;
        _chkAutorizaHorasExtra.Checked = AutorizaHorasExtra;

        BindScheduleOptions();
        BindPervasiveOptions();

        _txtNombreSql.Focus();
        _txtNombreSql.SelectAll();
    }

    private Control CreateIdentityLayout()
    {
        var layout = CreateFieldLayout();
        AddField(layout, "Nombre en SQL", _txtNombreSql);
        AddField(layout, "Nombre en dispositivo", _txtNombreDispositivo);
        AddField(layout, "Departamento (Excel/RH)", _txtDepartamento);
        AddField(layout, "Puesto (Excel/RH)", _txtPuesto);
        return layout;
    }

    private Control CreateHorarioLayout()
    {
        var layout = CreateFieldLayout();
        AddField(layout, "Código de horario", _cmbCodigoHorario);
        layout.Controls.Add(_chkAutorizaHorasExtra, 0, layout.RowCount++);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        var infoLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(108, 117, 128),
            Text = "Horario 1/2/3 define entrada, salida y horas estándar."
        };
        layout.Controls.Add(infoLabel, 0, layout.RowCount++);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        return layout;
    }

    private Control CreatePervasiveLayout()
    {
        var layout = CreateFieldLayout();
        AddField(layout, "DEPT (descripción)", _cmbPervasiveDept);
        AddField(layout, "Código DEPT", _txtPervasiveDeptCode);
        AddField(layout, "SHIFT (código)", _cmbPervasiveShift);

        var infoLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(108, 117, 128),
            Text = "DEPT usa descripción + código automático. SHIFT maneja solo código."
        };
        layout.Controls.Add(infoLabel, 0, layout.RowCount++);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        return layout;
    }

    private static TableLayoutPanel CreateFieldLayout()
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
            AutoScroll = true
        };
    }

    private static void AddField(TableLayoutPanel layout, string labelText, Control input)
    {
        var label = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Text = labelText,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(52, 64, 82)
        };
        input.Dock = DockStyle.Top;
        input.Height = 30;
        input.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        layout.Controls.Add(label, 0, layout.RowCount++);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.Controls.Add(input, 0, layout.RowCount++);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
    }

    private void BindScheduleOptions()
    {
        if (_scheduleOptions.Count == 0)
        {
            var currentCode = CodigoHorario?.Trim() ?? string.Empty;
            _scheduleOptions.Add(new ComboOption(string.Empty, "(sin seleccionar)"));
            _scheduleOptions.Add(new ComboOption("1", "1 - Horario 1"));
            _scheduleOptions.Add(new ComboOption("2", "2 - Horario 2"));
            _scheduleOptions.Add(new ComboOption("3", "3 - Horario 3"));
            if (!string.IsNullOrWhiteSpace(currentCode) && _scheduleOptions.All(x => x.Code != currentCode))
            {
                _scheduleOptions.Add(new ComboOption(currentCode, $"{currentCode} - Actual (no catalogado)"));
            }
        }

        _cmbCodigoHorario.DisplayMember = nameof(ComboOption.Text);
        _cmbCodigoHorario.ValueMember = nameof(ComboOption.Code);
        _cmbCodigoHorario.DataSource = _scheduleOptions.ToList();
        _cmbCodigoHorario.SelectedValue = CodigoHorario?.Trim() ?? string.Empty;
    }

    private void BindPervasiveOptions()
    {
        var deptOptions = BuildPervasiveDeptOptions(PervasiveDept);
        _cmbPervasiveDept.DisplayMember = nameof(ComboOption.Text);
        _cmbPervasiveDept.ValueMember = nameof(ComboOption.Code);
        _cmbPervasiveDept.DataSource = deptOptions;
        _cmbPervasiveDept.SelectedValue = ResolveOptionCode(deptOptions, PervasiveDept);
        SyncPervasiveDeptCode();

        var shiftOptions = BuildPervasiveShiftOptions(PervasiveShift);
        _cmbPervasiveShift.DisplayMember = nameof(ComboOption.Text);
        _cmbPervasiveShift.ValueMember = nameof(ComboOption.Code);
        _cmbPervasiveShift.DataSource = shiftOptions;
        _cmbPervasiveShift.SelectedValue = ResolveOptionCode(shiftOptions, PervasiveShift);
    }

    private static List<ComboOption> BuildPervasiveDeptOptions(string? initialValue)
    {
        var options = new List<ComboOption>
        {
            new(string.Empty, "(sin seleccionar)"),
            new("0", "RHH"),
            new("1", "PROD"),
            new("2", "ADMI"),
            new("3", "LOGI"),
            new("4", "VENT"),
            new("5", "MANF"),
            new("6", "ENSA"),
            new("7", "CALI"),
            new("8", "DIR"),
            new("9", "MTTO"),
            new("A", "PRACT")
        };

        EnsureCurrentOption(options, initialValue);
        return options;
    }

    private static List<ComboOption> BuildPervasiveShiftOptions(string? initialValue)
    {
        var options = new List<ComboOption>
        {
            new(string.Empty, "(sin seleccionar)"),
            new("0", "0"),
            new("1", "1"),
            new("2", "2"),
            new("3", "3"),
            new("4", "4"),
            new("5", "5"),
            new("6", "6"),
            new("7", "7"),
            new("8", "8"),
            new("9", "9"),
            new("A", "A")
        };

        EnsureCurrentOption(options, initialValue);
        return options;
    }

    private static void EnsureCurrentOption(List<ComboOption> options, string? currentValue)
    {
        var normalized = currentValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var exists = options.Any(x =>
            string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Text, normalized, StringComparison.OrdinalIgnoreCase));
        if (!exists)
        {
            options.Insert(1, new ComboOption(normalized, $"Actual: {normalized}"));
        }
    }

    private static string ResolveOptionCode(List<ComboOption> options, string? currentValue)
    {
        var normalized = currentValue?.Trim() ?? string.Empty;
        var selected = options.FirstOrDefault(x =>
            string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Text, normalized, StringComparison.OrdinalIgnoreCase));
        return selected?.Code ?? string.Empty;
    }

    private void SyncPervasiveDeptCode()
    {
        _txtPervasiveDeptCode.Text = _cmbPervasiveDept.SelectedValue as string ?? string.Empty;
    }

    private void SaveOutput()
    {
        NuevoNombre = _txtNombreSql.Text.Trim();
        NombreActual = _txtNombreSql.Text.Trim();
        NombreDispositivo = _txtNombreDispositivo.Text.Trim();
        Departamento = _txtDepartamento.Text.Trim();
        Puesto = _txtPuesto.Text.Trim();
        PervasiveDept = _cmbPervasiveDept.SelectedValue as string ?? string.Empty;
        PervasiveShift = _cmbPervasiveShift.SelectedValue as string ?? string.Empty;
        CodigoHorario = _cmbCodigoHorario.SelectedValue as string ?? string.Empty;
        AutorizaHorasExtra = _chkAutorizaHorasExtra.Checked;
    }

    private static TextBox CreateTextBox()
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private static ComboBox CreateComboBox()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
    }

    private sealed record ComboOption(string Code, string Text);
}
