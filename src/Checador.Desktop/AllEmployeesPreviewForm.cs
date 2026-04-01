using Checador.Domain.Models;
using Checador.Infrastructure.Access;

namespace Checador.Desktop;

public sealed class AllEmployeesPreviewForm : Form
{
    private readonly IReadOnlyList<ReportPreviewResult> _previews;
    private readonly IReadOnlyList<EmployeePreviewRow> _rows;
    private readonly SqlDeviceSyncService? _syncService;
    private readonly TextBox _txtFilter;
    private readonly ComboBox _cmbStatusFilter;
    private readonly Button _btnClearFilters;
    private readonly Label _lblEmployeesCounter;
    private readonly Button _btnPrevEmployee;
    private readonly Button _btnNextEmployee;
    private readonly TextBox _txtJumpUserId;
    private readonly Button _btnJumpUserId;
    private readonly Label _lblSummary;
    private readonly Label _lblKpiEmployees;
    private readonly Label _lblKpiWorked;
    private readonly Label _lblKpiExtra;
    private readonly Label _lblKpiIncidences;
    private readonly DataGridView _gridEmployees;
    private readonly DataGridView _gridDays;
    private readonly Label _lblDetailTitle;
    private readonly Label _lblDetailPeriod;
    private readonly Label _lblDetailHours;
    private readonly Label _lblDetailExtra;
    private readonly Label _lblDetailIncidences;
    private readonly Label _lblDetailNoRecord;
    private readonly Button _btnOpenFullPreview;
    private readonly ComboBox _cmbQuickSchedule;
    private readonly CheckBox _chkQuickOvertime;
    private readonly Button _btnSaveQuickRh;
    private readonly Label _lblQuickRhStatus;

    private bool _bindingEmployees;
    private bool _bindingQuickRh;
    private List<WorkScheduleItem> _workSchedules = new();
    private readonly Dictionary<int, EmployeeAdminSnapshot> _snapshotByUser = new();
    private ReportPreviewResult? _currentPreview;

    public AllEmployeesPreviewForm(IReadOnlyList<ReportPreviewResult> previews, SqlDeviceSyncService? syncService = null)
    {
        _syncService = syncService;

        _previews = previews
            .OrderBy(p => p.UserId)
            .ToList();

        _rows = previews
            .OrderBy(p => p.UserId)
            .Select(p => EmployeePreviewRow.FromPreview(p))
            .ToList();

        Text = $"Vista previa general · {_rows.Count} empleados";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1200;
        Height = 760;
        MinimumSize = new Size(980, 620);
        BackColor = Color.FromArgb(244, 247, 252);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var topPanel = new RoundedGroupBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Text = "Resumen general",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        var lblTitle = new Label
        {
            Text = "Vista previa masiva · revisa uno por uno sin generar Excel",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 48, 72),
            Location = new Point(14, 22)
        };

        _lblSummary = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(78, 90, 109),
            Location = new Point(16, 44)
        };

        var kpiPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 590,
            Padding = new Padding(0, 18, 12, 8)
        };

        var kpiFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };

        kpiFlow.Controls.Add(CreateMetricCard("Empleados", out _lblKpiEmployees, Color.FromArgb(232, 243, 255), Color.FromArgb(17, 80, 148), 138));
        kpiFlow.Controls.Add(CreateMetricCard("Horas", out _lblKpiWorked, Color.FromArgb(227, 241, 255), Color.FromArgb(14, 79, 149), 138));
        kpiFlow.Controls.Add(CreateMetricCard("Extra", out _lblKpiExtra, Color.FromArgb(245, 232, 255), Color.FromArgb(90, 49, 129), 138));
        kpiFlow.Controls.Add(CreateMetricCard("Incidencias", out _lblKpiIncidences, Color.FromArgb(255, 236, 236), Color.FromArgb(156, 45, 45), 138));

        kpiPanel.Controls.Add(kpiFlow);

        topPanel.Controls.Add(lblTitle);
        topPanel.Controls.Add(_lblSummary);
        topPanel.Controls.Add(kpiPanel);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 460,
            SplitterWidth = 8,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(226, 233, 244),
            Panel1MinSize = 410
        };

        var lblFilter = new Label
        {
            Text = "Buscar empleado (nombre o USERID)",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(90, 100, 115),
            Margin = new Padding(0, 0, 0, 0)
        };

        _txtFilter = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            PlaceholderText = "Ejemplo: 14 o AGAPITO"
        };
        _txtFilter.TextChanged += (_, _) => RefreshGrid();

        _cmbStatusFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0)
        };
        _cmbStatusFilter.Items.AddRange(new object[] { "TODOS", "OK", "REVISAR", "SIN_REGISTRO" });
        _cmbStatusFilter.SelectedIndex = 0;
        _cmbStatusFilter.SelectedIndexChanged += (_, _) => RefreshGrid();

        _btnClearFilters = new Button
        {
            Text = "Limpiar filtros",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0)
        };
        ButtonStyles.ApplySecondary(_btnClearFilters);
        _btnClearFilters.Click += (_, _) => ClearFilters();

        _txtJumpUserId = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0),
            PlaceholderText = "USERID"
        };
        _txtJumpUserId.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                JumpToUserId();
            }
        };

        _btnJumpUserId = new Button
        {
            Text = "Ir",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0)
        };
        ButtonStyles.ApplySecondary(_btnJumpUserId);
        _btnJumpUserId.Click += (_, _) => JumpToUserId();

        _gridEmployees = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            ScrollBars = ScrollBars.Vertical,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            EnableHeadersVisualStyles = false,
            GridColor = Color.FromArgb(230, 236, 245)
        };
        _gridEmployees.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 246, 252);
        _gridEmployees.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 45, 66);
        _gridEmployees.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _gridEmployees.ColumnHeadersHeight = 36;
        _gridEmployees.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _gridEmployees.DefaultCellStyle.ForeColor = Color.FromArgb(38, 49, 66);
        _gridEmployees.DefaultCellStyle.SelectionBackColor = Color.FromArgb(227, 239, 255);
        _gridEmployees.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 30, 45);
        _gridEmployees.RowTemplate.Height = 30;
        _gridEmployees.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);
        _gridEmployees.SelectionChanged += (_, _) => OnSelectedEmployeeChanged();
        _gridEmployees.CellDoubleClick += (_, _) => OpenSelectedEmployeePreview();
        _gridEmployees.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down)
            {
                MoveEmployeeSelection(+1);
            }
            else if (e.KeyCode == Keys.Up)
            {
                MoveEmployeeSelection(-1);
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                MoveEmployeeSelection(+5);
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                MoveEmployeeSelection(-5);
            }
        };

        var leftPanel = new RoundedGroupBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Text = "Empleados",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10, 4, 10, 8),
            BackColor = Color.Transparent
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolsRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0)
        };
        toolsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        toolsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        toolsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        toolsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));

        toolsRow.Controls.Add(_txtFilter, 0, 0);
        toolsRow.Controls.Add(_cmbStatusFilter, 1, 0);
        toolsRow.Controls.Add(_txtJumpUserId, 2, 0);
        toolsRow.Controls.Add(_btnJumpUserId, 3, 0);
        toolsRow.Controls.Add(_btnClearFilters, 4, 0);

        var leftTitle = new Label
        {
            Text = "Empleados",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(38, 52, 80),
            Margin = new Padding(0, 6, 0, 0)
        };

        _lblEmployeesCounter = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 8.8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(88, 101, 123),
            Text = "Mostrando 0/0",
            Margin = new Padding(8, 7, 0, 0)
        };

        _btnPrevEmployee = new Button
        {
            Text = "<",
            Width = 34,
            Height = 24,
            Margin = new Padding(0, 3, 4, 0)
        };
        ButtonStyles.ApplySecondary(_btnPrevEmployee);
        _btnPrevEmployee.Click += (_, _) => MoveEmployeeSelection(-1);

        _btnNextEmployee = new Button
        {
            Text = ">",
            Width = 34,
            Height = 24,
            Margin = new Padding(0, 3, 0, 0)
        };
        ButtonStyles.ApplySecondary(_btnNextEmployee);
        _btnNextEmployee.Click += (_, _) => MoveEmployeeSelection(+1);

        var navigationRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            Margin = new Padding(0)
        };
        navigationRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        navigationRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        navigationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        navigationRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var navButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        navButtons.Controls.Add(_btnPrevEmployee);
        navButtons.Controls.Add(_btnNextEmployee);

        navigationRow.Controls.Add(leftTitle, 0, 0);
        navigationRow.Controls.Add(_lblEmployeesCounter, 1, 0);
        navigationRow.Controls.Add(new Label { AutoSize = true }, 2, 0);
        navigationRow.Controls.Add(navButtons, 3, 0);

        _gridEmployees.Dock = DockStyle.Fill;
        _gridEmployees.Margin = new Padding(0, 4, 0, 0);

        leftLayout.Controls.Add(lblFilter, 0, 0);
        leftLayout.Controls.Add(toolsRow, 0, 1);
        leftLayout.Controls.Add(navigationRow, 0, 2);
        leftLayout.Controls.Add(_gridEmployees, 0, 3);
        leftPanel.Controls.Add(leftLayout);

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 228));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var detailHeader = new RoundedGroupBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Text = "Detalle del empleado seleccionado",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        var detailRoot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(8)
        };
        detailRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        detailRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        detailRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        detailRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        detailRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        detailRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lblDetailTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 78),
            Text = "Selecciona un empleado"
        };

        _lblDetailPeriod = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(95, 106, 122),
            Text = ""
        };

        _btnOpenFullPreview = new Button
        {
            Text = "Ver completo",
            Width = 122,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Enabled = false
        };
        _btnOpenFullPreview.Click += (_, _) => OpenSelectedEmployeePreview();

        _cmbQuickSchedule = new ComboBox
        {
            Width = 210,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false,
            FlatStyle = FlatStyle.Flat
        };

        _chkQuickOvertime = new CheckBox
        {
            AutoSize = true,
            Text = "Autorizar horas extra",
            Font = new Font("Segoe UI", 8.8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(45, 61, 89),
            Enabled = false,
            Margin = new Padding(8, 8, 0, 0)
        };

        _btnSaveQuickRh = new Button
        {
            Text = "Guardar RH",
            Width = 114,
            Height = 30,
            Enabled = false
        };
        ButtonStyles.ApplyPrimary(_btnSaveQuickRh);
        _btnSaveQuickRh.Click += async (_, _) => await SaveQuickRhChangesAsync();

        _lblQuickRhStatus = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(89, 103, 126),
            Text = ""
        };

        var quickRhLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        quickRhLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        quickRhLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var quickRhTop = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        var lblQuick = new Label
        {
            AutoSize = true,
            Text = "Ajuste rapido RH:",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 59, 88),
            Margin = new Padding(0, 8, 6, 0)
        };

        quickRhTop.Controls.Add(lblQuick);
        quickRhTop.Controls.Add(_cmbQuickSchedule);
        quickRhTop.Controls.Add(_chkQuickOvertime);
        quickRhTop.Controls.Add(_btnSaveQuickRh);

        quickRhLayout.Controls.Add(quickRhTop, 0, 0);
        quickRhLayout.Controls.Add(_lblQuickRhStatus, 0, 1);

        var cardHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Margin = new Padding(0)
        };

        cardHost.Controls.Add(CreateMetricCard("Horas", out _lblDetailHours, Color.FromArgb(227, 241, 255), Color.FromArgb(14, 79, 149)));
        cardHost.Controls.Add(CreateMetricCard("Extra", out _lblDetailExtra, Color.FromArgb(245, 232, 255), Color.FromArgb(90, 49, 129)));
        cardHost.Controls.Add(CreateMetricCard("Incidencias", out _lblDetailIncidences, Color.FromArgb(255, 236, 236), Color.FromArgb(156, 45, 45)));
        cardHost.Controls.Add(CreateMetricCard("Sin registro", out _lblDetailNoRecord, Color.FromArgb(255, 246, 231), Color.FromArgb(134, 88, 18)));

        detailRoot.Controls.Add(_lblDetailTitle, 0, 0);
        detailRoot.Controls.Add(_lblDetailPeriod, 0, 1);
        detailRoot.Controls.Add(_btnOpenFullPreview, 1, 0);
        detailRoot.SetRowSpan(_btnOpenFullPreview, 2);
        detailRoot.Controls.Add(quickRhLayout, 0, 2);
        detailRoot.SetColumnSpan(quickRhLayout, 2);
        detailRoot.Controls.Add(cardHost, 0, 3);
        detailRoot.SetColumnSpan(cardHost, 2);

        detailHeader.Controls.Add(detailRoot);

        _gridDays = new DataGridView
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
            BorderStyle = BorderStyle.FixedSingle,
            EnableHeadersVisualStyles = false,
            GridColor = Color.FromArgb(230, 236, 245)
        };
        _gridDays.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 246, 252);
        _gridDays.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 45, 66);
        _gridDays.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _gridDays.ColumnHeadersHeight = 34;
        _gridDays.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _gridDays.DefaultCellStyle.ForeColor = Color.FromArgb(38, 49, 66);
        _gridDays.DefaultCellStyle.SelectionBackColor = Color.FromArgb(227, 239, 255);
        _gridDays.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 30, 45);
        _gridDays.RowTemplate.Height = 30;
        _gridDays.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);
        _gridDays.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_gridDays.Rows[e.RowIndex].DataBoundItem is not DayGridRow day)
            {
                return;
            }

            var column = _gridDays.Columns[e.ColumnIndex];
            if (column.DataPropertyName == nameof(DayGridRow.Nota) && !string.IsNullOrWhiteSpace(day.Nota))
            {
                e.ToolTipText = day.Nota;
            }
        };

        var hintLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = Color.FromArgb(108, 120, 138),
            Text = "Tip: usa filtros, navega con < > y escribe USERID + Enter para ir directo a cualquier empleado."
        };

        rightLayout.Controls.Add(detailHeader, 0, 0);
        rightLayout.Controls.Add(_gridDays, 0, 1);
        rightLayout.Controls.Add(hintLabel, 0, 2);

        split.Panel1.Controls.Add(leftPanel);
        split.Panel2.Controls.Add(rightLayout);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };

        var btnClose = new Button
        {
            Text = "Cerrar",
            Width = 110,
            Height = 30,
            DialogResult = DialogResult.OK
        };

        footer.Controls.Add(btnClose);

        root.Controls.Add(topPanel, 0, 0);
        root.Controls.Add(split, 0, 1);
        root.Controls.Add(footer, 0, 2);

        AcceptButton = _btnOpenFullPreview;
        CancelButton = btnClose;

        RefreshSummary();
        RefreshGrid();
        _ = InitializeQuickRhEditorAsync();
    }

    private static Panel CreateMetricCard(string title, out Label valueLabel, Color backColor, Color valueColor, int width = 170)
    {
        var card = new Panel
        {
            Width = width,
            Height = 66,
            BackColor = backColor,
            Margin = new Padding(0, 0, 8, 0)
        };

        var titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 8.8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(78, 92, 113),
            Padding = new Padding(8, 6, 8, 0),
            Text = title
        };

        valueLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            ForeColor = valueColor,
            Padding = new Padding(8, 0, 8, 6),
            Text = "0"
        };

        card.Controls.Add(valueLabel);
        card.Controls.Add(titleLabel);
        return card;
    }

    private void RefreshSummary()
    {
        var totalWorked = _rows.Sum(r => r.TotalWorkedHours);
        var totalExtra = _rows.Sum(r => r.TotalExtraHours);
        var withIncidence = _rows.Count(r => r.IncidenceDays > 0);

        _lblKpiEmployees.Text = _rows.Count.ToString();
        _lblKpiWorked.Text = totalWorked.ToString("0.00");
        _lblKpiExtra.Text = totalExtra.ToString("0.00");
        _lblKpiIncidences.Text = withIncidence.ToString();

        _lblSummary.Text =
            $"Empleados: {_rows.Count}  |  Horas: {totalWorked:0.00}  |  Extra: {totalExtra:0.00}  |  Con incidencia: {withIncidence}";
    }

    private void RefreshGrid()
    {
        var filter = _txtFilter.Text.Trim();
        var filteredRows = string.IsNullOrWhiteSpace(filter)
            ? _rows
            : _rows.Where(r =>
                r.UserId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
                || r.EmployeeName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || r.EmployeeDisplay.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var statusFilter = _cmbStatusFilter.SelectedItem?.ToString() ?? "TODOS";
        if (!string.Equals(statusFilter, "TODOS", StringComparison.OrdinalIgnoreCase))
        {
            filteredRows = filteredRows
                .Where(r => string.Equals(r.SummaryStatus, statusFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _lblEmployeesCounter.Text = $"Mostrando {filteredRows.Count}/{_rows.Count}";

        var selectedUserId = _gridEmployees.CurrentRow?.DataBoundItem is GridRow current
            ? current.UserId
            : _currentPreview?.UserId;

        _bindingEmployees = true;
        _gridEmployees.DataSource = filteredRows
            .Select(r => new GridRow
            {
                UserId = r.UserId,
                Empleado = r.EmployeeDisplay,
                Estado = r.SummaryStatus,
                Horas = r.TotalWorkedHours,
                Extra = r.TotalExtraHours,
                DiasConRegistro = r.DaysWithRecords,
                Incidencias = r.IncidenceDays,
                SinRegistro = r.DaysWithoutRecords
            })
            .ToList();
        _bindingEmployees = false;

        if (_gridEmployees.Columns.Contains(nameof(GridRow.UserId)))
        {
            _gridEmployees.Columns[nameof(GridRow.UserId)].HeaderText = "USERID";
            _gridEmployees.Columns[nameof(GridRow.UserId)].FillWeight = 55;
            _gridEmployees.Columns[nameof(GridRow.UserId)].Frozen = true;
        }

        if (_gridEmployees.Columns.Contains(nameof(GridRow.Empleado)))
        {
            _gridEmployees.Columns[nameof(GridRow.Empleado)].FillWeight = 210;
            _gridEmployees.Columns[nameof(GridRow.Empleado)].Frozen = true;
        }

        if (_gridEmployees.Columns.Contains(nameof(GridRow.Estado)))
        {
            _gridEmployees.Columns[nameof(GridRow.Estado)].FillWeight = 90;
        }

        if (_gridEmployees.Columns.Contains(nameof(GridRow.Horas)))
        {
            _gridEmployees.Columns[nameof(GridRow.Horas)].DefaultCellStyle.Format = "0.00";
            _gridEmployees.Columns[nameof(GridRow.Horas)].FillWeight = 70;
        }

        if (_gridEmployees.Columns.Contains(nameof(GridRow.Extra)))
        {
            _gridEmployees.Columns[nameof(GridRow.Extra)].DefaultCellStyle.Format = "0.00";
            _gridEmployees.Columns[nameof(GridRow.Extra)].FillWeight = 70;
        }

        if (_gridEmployees.Columns.Contains(nameof(GridRow.DiasConRegistro)))
        {
            _gridEmployees.Columns[nameof(GridRow.DiasConRegistro)].HeaderText = "Dias con marca";
            _gridEmployees.Columns[nameof(GridRow.DiasConRegistro)].FillWeight = 80;
        }

        if (_gridEmployees.Columns.Contains(nameof(GridRow.Incidencias)))
        {
            _gridEmployees.Columns[nameof(GridRow.Incidencias)].FillWeight = 70;
        }

        if (_gridEmployees.Columns.Contains(nameof(GridRow.SinRegistro)))
        {
            _gridEmployees.Columns[nameof(GridRow.SinRegistro)].HeaderText = "Sin registro";
            _gridEmployees.Columns[nameof(GridRow.SinRegistro)].FillWeight = 75;
        }

        ApplyEmployeeRowColors();

        if (_gridEmployees.Rows.Count == 0)
        {
            ClearDetail();
            UpdateEmployeeNavigationState();
            return;
        }

        var rowIndex = 0;
        if (selectedUserId.HasValue)
        {
            for (var i = 0; i < _gridEmployees.Rows.Count; i++)
            {
                if (_gridEmployees.Rows[i].DataBoundItem is GridRow row && row.UserId == selectedUserId.Value)
                {
                    rowIndex = i;
                    break;
                }
            }
        }

        _gridEmployees.ClearSelection();
        _gridEmployees.Rows[rowIndex].Selected = true;
        if (_gridEmployees.Rows[rowIndex].Cells.Count > 0)
        {
            _gridEmployees.CurrentCell = _gridEmployees.Rows[rowIndex].Cells[0];
        }

        UpdateEmployeeNavigationState();
        OnSelectedEmployeeChanged();
    }

    private void ApplyEmployeeRowColors()
    {
        foreach (DataGridViewRow row in _gridEmployees.Rows)
        {
            if (row.DataBoundItem is not GridRow data)
            {
                continue;
            }

            if (data.Estado == "OK")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(238, 250, 241);
            }
            else if (data.Estado == "REVISAR")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 239, 233);
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 246, 231);
            }
        }
    }

    private void ClearFilters()
    {
        _txtFilter.Text = string.Empty;
        _cmbStatusFilter.SelectedIndex = 0;
        _txtJumpUserId.Text = string.Empty;
        RefreshGrid();
    }

    private void JumpToUserId()
    {
        var raw = _txtJumpUserId.Text.Trim();
        if (!int.TryParse(raw, out var userId))
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                MessageBox.Show("Escribe un USERID numerico para navegar.", "Navegacion", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return;
        }

        if (TrySelectEmployeeRow(userId))
        {
            return;
        }

        MessageBox.Show($"El USERID {userId} no esta en la lista visible actual.", "Navegacion", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void MoveEmployeeSelection(int delta)
    {
        if (_gridEmployees.Rows.Count == 0)
        {
            return;
        }

        var currentIndex = _gridEmployees.CurrentRow?.Index ?? 0;
        var nextIndex = Math.Clamp(currentIndex + delta, 0, _gridEmployees.Rows.Count - 1);
        if (nextIndex == currentIndex)
        {
            return;
        }

        _gridEmployees.ClearSelection();
        _gridEmployees.Rows[nextIndex].Selected = true;
        _gridEmployees.CurrentCell = _gridEmployees.Rows[nextIndex].Cells[0];
        _gridEmployees.FirstDisplayedScrollingRowIndex = Math.Max(0, nextIndex - 3);
        UpdateEmployeeNavigationState();
        OnSelectedEmployeeChanged();
    }

    private bool TrySelectEmployeeRow(int userId)
    {
        for (var i = 0; i < _gridEmployees.Rows.Count; i++)
        {
            if (_gridEmployees.Rows[i].DataBoundItem is not GridRow row || row.UserId != userId)
            {
                continue;
            }

            _gridEmployees.ClearSelection();
            _gridEmployees.Rows[i].Selected = true;
            _gridEmployees.CurrentCell = _gridEmployees.Rows[i].Cells[0];
            _gridEmployees.FirstDisplayedScrollingRowIndex = Math.Max(0, i - 2);
            UpdateEmployeeNavigationState();
            OnSelectedEmployeeChanged();
            return true;
        }

        return false;
    }

    private void UpdateEmployeeNavigationState()
    {
        var hasRows = _gridEmployees.Rows.Count > 0;
        _btnPrevEmployee.Enabled = hasRows && _gridEmployees.CurrentRow is not null && _gridEmployees.CurrentRow.Index > 0;
        _btnNextEmployee.Enabled = hasRows && _gridEmployees.CurrentRow is not null && _gridEmployees.CurrentRow.Index < _gridEmployees.Rows.Count - 1;
        _btnJumpUserId.Enabled = hasRows;
    }

    private void OnSelectedEmployeeChanged()
    {
        if (_bindingEmployees)
        {
            return;
        }

        UpdateEmployeeNavigationState();

        if (_gridEmployees.CurrentRow?.DataBoundItem is not GridRow selected)
        {
            ClearDetail();
            return;
        }

        _currentPreview = _previews.FirstOrDefault(p => p.UserId == selected.UserId);
        if (_currentPreview is null)
        {
            ClearDetail();
            return;
        }

        var rowMetrics = _rows.FirstOrDefault(r => r.UserId == selected.UserId);
        _lblDetailTitle.Text = $"Empleado {selected.UserId} · {_currentPreview.EmployeeName}";
        _lblDetailPeriod.Text = $"Periodo: {_currentPreview.FechaInicio:dd-MM-yyyy} al {_currentPreview.FechaFin:dd-MM-yyyy}";

        _lblDetailHours.Text = (rowMetrics?.TotalWorkedHours ?? 0m).ToString("0.00");
        _lblDetailExtra.Text = (rowMetrics?.TotalExtraHours ?? 0m).ToString("0.00");
        _lblDetailIncidences.Text = (rowMetrics?.IncidenceDays ?? 0).ToString("0");
        _lblDetailNoRecord.Text = (rowMetrics?.DaysWithoutRecords ?? 0).ToString("0");
        _btnOpenFullPreview.Enabled = true;

        _gridDays.DataSource = _currentPreview.Days
            .OrderBy(d => d.Date)
            .Select(d => new DayGridRow
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
            })
            .ToList();

        if (_gridDays.Columns.Contains(nameof(DayGridRow.Fecha)))
        {
            _gridDays.Columns[nameof(DayGridRow.Fecha)].HeaderText = "Fecha";
            _gridDays.Columns[nameof(DayGridRow.Fecha)].DefaultCellStyle.Format = "dd-MM-yyyy";
            _gridDays.Columns[nameof(DayGridRow.Fecha)].FillWeight = 85;
            _gridDays.Columns[nameof(DayGridRow.Fecha)].Frozen = true;
        }

        if (_gridDays.Columns.Contains(nameof(DayGridRow.Horas)))
        {
            _gridDays.Columns[nameof(DayGridRow.Horas)].DefaultCellStyle.Format = "0.00";
            _gridDays.Columns[nameof(DayGridRow.Horas)].FillWeight = 65;
        }

        if (_gridDays.Columns.Contains(nameof(DayGridRow.Extra)))
        {
            _gridDays.Columns[nameof(DayGridRow.Extra)].DefaultCellStyle.Format = "0.00";
            _gridDays.Columns[nameof(DayGridRow.Extra)].FillWeight = 65;
        }

        if (_gridDays.Columns.Contains(nameof(DayGridRow.Estado)))
        {
            _gridDays.Columns[nameof(DayGridRow.Estado)].FillWeight = 95;
        }

        if (_gridDays.Columns.Contains(nameof(DayGridRow.Nota)))
        {
            _gridDays.Columns[nameof(DayGridRow.Nota)].FillWeight = 180;
            _gridDays.Columns[nameof(DayGridRow.Nota)].HeaderText = "Observaciones";
        }

        ApplyDayRowColors();
        _ = LoadQuickRhForSelectedEmployeeAsync(selected.UserId);
    }

    private async Task InitializeQuickRhEditorAsync()
    {
        if (_syncService is null)
        {
            SetQuickRhEditorState(false, "Edicion RH no disponible en este contexto.");
            return;
        }

        try
        {
            _workSchedules = (await _syncService.GetWorkSchedulesAsync())
                .Where(x => x.IsActive)
                .OrderBy(x => x.Code)
                .ToList();

            if (_workSchedules.Count == 0)
            {
                _workSchedules = (await _syncService.GetWorkSchedulesAsync())
                    .OrderBy(x => x.Code)
                    .ToList();
            }

            _bindingQuickRh = true;
            _cmbQuickSchedule.DataSource = _workSchedules
                .Select(QuickScheduleOption.FromSchedule)
                .ToList();
            _cmbQuickSchedule.DisplayMember = nameof(QuickScheduleOption.DisplayText);
            _cmbQuickSchedule.ValueMember = nameof(QuickScheduleOption.Code);
            _bindingQuickRh = false;

            if (_currentPreview is null)
            {
                SetQuickRhEditorState(false, "Selecciona un empleado para editar horario y extras.");
                return;
            }

            await LoadQuickRhForSelectedEmployeeAsync(_currentPreview.UserId);
        }
        catch (Exception ex)
        {
            _bindingQuickRh = false;
            SetQuickRhEditorState(false, $"No se pudieron cargar horarios RH: {ex.Message}");
        }
    }

    private async Task LoadQuickRhForSelectedEmployeeAsync(int userId)
    {
        if (_bindingQuickRh || _syncService is null)
        {
            return;
        }

        if (_workSchedules.Count == 0)
        {
            SetQuickRhEditorState(false, "No hay horarios RH disponibles para editar.");
            return;
        }

        try
        {
            if (!_snapshotByUser.TryGetValue(userId, out var snapshot))
            {
                var loaded = await _syncService.GetEmployeeAdminSnapshotAsync(userId);
                if (loaded is not null)
                {
                    _snapshotByUser[userId] = loaded;
                    snapshot = loaded;
                }
            }

            _bindingQuickRh = true;
            if (snapshot?.WorkScheduleCode is int scheduleCode && _workSchedules.Any(x => x.Code == scheduleCode))
            {
                _cmbQuickSchedule.SelectedValue = scheduleCode;
            }
            else
            {
                _cmbQuickSchedule.SelectedIndex = _cmbQuickSchedule.Items.Count > 0 ? 0 : -1;
            }

            _chkQuickOvertime.Checked = snapshot?.OvertimeAuthorized ?? false;
            _bindingQuickRh = false;

            SetQuickRhEditorState(true,
                $"Actual: {FormatScheduleCode(snapshot?.WorkScheduleCode)} · Extra {(snapshot?.OvertimeAuthorized == true ? "SI" : "NO")}");
        }
        catch (Exception ex)
        {
            _bindingQuickRh = false;
            SetQuickRhEditorState(false, $"No se pudo cargar configuracion RH del empleado: {ex.Message}");
        }
    }

    private async Task SaveQuickRhChangesAsync()
    {
        if (_bindingQuickRh || _syncService is null || _currentPreview is null)
        {
            return;
        }

        if (_cmbQuickSchedule.SelectedItem is not QuickScheduleOption selectedSchedule)
        {
            MessageBox.Show("Selecciona un horario valido antes de guardar.", "Configuracion RH", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var userId = _currentPreview.UserId;

        if (!_snapshotByUser.TryGetValue(userId, out var snapshot))
        {
            snapshot = await _syncService.GetEmployeeAdminSnapshotAsync(userId);
            if (snapshot is not null)
            {
                _snapshotByUser[userId] = snapshot;
            }
        }

        var newScheduleCode = selectedSchedule.Code;
        var newOvertimeAuthorized = _chkQuickOvertime.Checked;
        var sameSchedule = snapshot?.WorkScheduleCode == newScheduleCode;
        var sameOvertime = (snapshot?.OvertimeAuthorized ?? false) == newOvertimeAuthorized;
        if (sameSchedule && sameOvertime)
        {
            _lblQuickRhStatus.Text = "Sin cambios pendientes por guardar.";
            return;
        }

        var currentName = !string.IsNullOrWhiteSpace(snapshot?.SqlName)
            ? snapshot!.SqlName!
            : _currentPreview.EmployeeName;

        try
        {
            SetQuickRhEditorState(false, "Guardando cambios RH...");

            var result = await _syncService.RenameEmployeeAsync(
                userId,
                currentName,
                snapshot?.Department,
                snapshot?.Position,
                snapshot?.PervasiveDept,
                snapshot?.PervasiveShift,
                newScheduleCode,
                newOvertimeAuthorized);

            var savedSnapshot = new EmployeeAdminSnapshot(
                userId,
                snapshot?.SqlName ?? currentName,
                snapshot?.Department,
                snapshot?.Position,
                snapshot?.PervasiveDept,
                snapshot?.PervasiveShift,
                newScheduleCode,
                newOvertimeAuthorized);

            _snapshotByUser[userId] = savedSnapshot;
            SetQuickRhEditorState(true,
                $"Guardado: {FormatScheduleCode(newScheduleCode)} · Extra {(newOvertimeAuthorized ? "SI" : "NO")}. Reejecuta vista previa para recalcular.");

            if (!result.UpdatedOnDevice)
            {
                MessageBox.Show(
                    $"Se guardo en SQL pero el checador reporto aviso:\n{result.DeviceMessage}",
                    "Configuracion RH",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            SetQuickRhEditorState(true, $"Error al guardar RH: {ex.Message}");
            MessageBox.Show($"No se pudieron guardar los cambios RH.\n\n{ex.Message}", "Configuracion RH", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetQuickRhEditorState(bool enabled, string statusText)
    {
        var canEdit = enabled && _syncService is not null && _workSchedules.Count > 0 && _currentPreview is not null;
        _cmbQuickSchedule.Enabled = canEdit;
        _chkQuickOvertime.Enabled = canEdit;
        _btnSaveQuickRh.Enabled = canEdit;
        _lblQuickRhStatus.Text = statusText;
    }

    private string FormatScheduleCode(int? code)
    {
        if (!code.HasValue)
        {
            return "(sin horario)";
        }

        var schedule = _workSchedules.FirstOrDefault(x => x.Code == code.Value);
        if (schedule is null)
        {
            return $"Horario {code.Value}";
        }

        return $"{schedule.Code} - {schedule.Name} ({schedule.StartTime:hh\\:mm}-{schedule.EndTime:hh\\:mm})";
    }

    private void ApplyDayRowColors()
    {
        foreach (DataGridViewRow row in _gridDays.Rows)
        {
            if (row.DataBoundItem is not DayGridRow day)
            {
                continue;
            }

            if (day.Estado is "OK" or "JORNADA_CONTINUA")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(238, 250, 241);
            }
            else if (day.Estado == "SIN_REGISTRO")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 246, 231);
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 236, 236);
            }
        }
    }

    private void ClearDetail()
    {
        _currentPreview = null;
        _lblDetailTitle.Text = "Selecciona un empleado";
        _lblDetailPeriod.Text = string.Empty;
        _lblDetailHours.Text = "0.00";
        _lblDetailExtra.Text = "0.00";
        _lblDetailIncidences.Text = "0";
        _lblDetailNoRecord.Text = "0";
        _btnOpenFullPreview.Enabled = false;
        _gridDays.DataSource = null;
        SetQuickRhEditorState(false, "Selecciona un empleado para editar horario y extras.");
    }

    private void OpenSelectedEmployeePreview()
    {
        if (_currentPreview is null)
        {
            MessageBox.Show("Selecciona un empleado para abrir la vista previa.", "Vista previa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var previewForm = new ReportPreviewForm(_currentPreview);
        previewForm.DatosHorasPorDia = _currentPreview.Days
            .Select(d => (d.Date.ToString("dd/MM"), Convert.ToDouble(d.HorasTrabajadas ?? 0m)))
            .ToList();
        previewForm.ShowDialog(this);
    }

    private sealed class GridRow
    {
        public int UserId { get; init; }
        public string Empleado { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
        public decimal Horas { get; init; }
        public decimal Extra { get; init; }
        public int DiasConRegistro { get; init; }
        public int Incidencias { get; init; }
        public int SinRegistro { get; init; }
    }

    private sealed class DayGridRow
    {
        public DateTime Fecha { get; init; }
        public string Entrada { get; init; } = string.Empty;
        public string SalidaComida { get; init; } = string.Empty;
        public string EntradaComida { get; init; } = string.Empty;
        public string Salida { get; init; } = string.Empty;
        public decimal? Horas { get; init; }
        public decimal? Extra { get; init; }
        public string Estado { get; init; } = string.Empty;
        public string Nota { get; init; } = string.Empty;
    }

    private sealed class QuickScheduleOption
    {
        public int Code { get; init; }
        public string DisplayText { get; init; } = string.Empty;

        public static QuickScheduleOption FromSchedule(WorkScheduleItem schedule)
        {
            return new QuickScheduleOption
            {
                Code = schedule.Code,
                DisplayText = $"{schedule.Code} - {schedule.Name} ({schedule.StartTime:hh\\:mm}-{schedule.EndTime:hh\\:mm}, {schedule.StandardHours:0.##}h)"
            };
        }
    }

    private sealed class EmployeePreviewRow
    {
        public required int UserId { get; init; }
        public required string EmployeeName { get; init; }
        public required string EmployeeDisplay { get; init; }
        public required ReportPreviewResult Preview { get; init; }
        public required decimal TotalWorkedHours { get; init; }
        public required decimal TotalExtraHours { get; init; }
        public required int DaysWithRecords { get; init; }
        public required int DaysWithoutRecords { get; init; }
        public required int IncidenceDays { get; init; }
        public required string SummaryStatus { get; init; }

        public static EmployeePreviewRow FromPreview(ReportPreviewResult preview)
        {
            var daysWithRecords = preview.Days.Count(d => d.Estatus != "SIN_REGISTRO");
            var daysWithoutRecords = preview.Days.Count(d => d.Estatus == "SIN_REGISTRO");
            var incidenceDays = preview.Days.Count(d => d.Estatus is "INCIDENCIA" or "INCOMPLETO");

            var summaryStatus = incidenceDays > 0
                ? "REVISAR"
                : (daysWithRecords == 0 ? "SIN_REGISTRO" : "OK");

            return new EmployeePreviewRow
            {
                UserId = preview.UserId,
                EmployeeName = preview.EmployeeName,
                EmployeeDisplay = $"{preview.UserId} - {preview.EmployeeName}",
                Preview = preview,
                TotalWorkedHours = preview.TotalWorkedHours,
                TotalExtraHours = preview.TotalExtraHours,
                DaysWithRecords = daysWithRecords,
                DaysWithoutRecords = daysWithoutRecords,
                IncidenceDays = incidenceDays,
                SummaryStatus = summaryStatus
            };
        }
    }
}
