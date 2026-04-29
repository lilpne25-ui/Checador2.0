using Checador.Infrastructure.Access;

namespace Checador.Desktop;

public sealed partial class SyncAdminForm : Form
{
    private const string SensitiveChangePassword = "Innovaxpw*d1";

    private static readonly PervasiveCodeOption[] PervasiveCodeOptions =
    {
        new("", "(sin seleccionar)"),
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

    private static readonly PervasiveCodeOption[] PervasiveShiftOptions =
    {
        new("", "(sin seleccionar)"),
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

    private readonly SqlDeviceSyncService _syncService;
    private readonly Dictionary<Button, Size> _buttonBaseSizes = new();
    private readonly TableLayoutPanel _rootLayout;
    private readonly FlowLayoutPanel _actionPanel;
    private readonly TabControl _tabs;
    private readonly TabPage _tabOperacion;
    private readonly TabPage _tabEmpleados;
    private readonly TabPage _tabConfiguracion;
    private readonly TableLayoutPanel _tabOperacionLayout;
    private readonly TableLayoutPanel _tabEmpleadosLayout;
    private readonly TableLayoutPanel _tabConfiguracionLayout;
    private readonly FlowLayoutPanel _employeeActionsPanel;
    private readonly FlowLayoutPanel _configActionsPanel;
    private readonly FlowLayoutPanel _autoSyncPanel;
    private readonly TableLayoutPanel _monitorPanel;

    private readonly Button _btnDownloadLogs;
    private readonly Button _btnRefreshEmployees;
    private readonly Button _btnRenameEmployee;
    private readonly Button _btnAutoSyncGui;
    private readonly Button _btnWorkSchedules;
    private readonly Button _btnDeleteEmployee;
    private readonly Button _btnOvertimeFormat;
    private readonly Button _btnSyncDeviceTime;
    private readonly Button _btnPreviewPervasive;
    private readonly Button _btnNightShiftCleanup;
    private readonly Button _btnChangeEmployeeRange;
    private readonly Button _btnRefreshStats;
    private readonly Button _btnSecretManualPunch;

    private readonly CheckBox _chkAutoSync;
    private readonly NumericUpDown _numAutoSyncMinutes;
    private readonly System.Windows.Forms.Timer _autoSyncTimer;
    private readonly System.Windows.Forms.Timer _liveMonitorTimer;

    private readonly Label _lblStats;
    private readonly StatusPillLabel _lblLiveConnection;
    private readonly StatusPillLabel _lblLiveSync;
    private readonly Label _lblLiveClock;
    private readonly Label _lblLiveUsers;
    private readonly Label _lblLiveSql;
    private readonly Label _lblLiveDetail;
    private readonly ListBox _lstRecentLogs;
    private readonly Label _lblPervasivePreview;
    private readonly DataGridView _dgvPervasivePreview;
    private readonly DataGridView _dgvEmployees;
    private readonly TextBox _txtLog;
    private readonly OvertimeWorkOrderFormatGenerator _overtimeFormatGenerator = new();

    private bool _busy;
    private bool _secretManualPunchUnlocked;
    private AutoSyncControlForm? _autoSyncControlForm;

    public SyncAdminForm(
        SqlDeviceSyncService syncService,
        int autoSyncMinutes = 15)
    {
        _syncService = syncService;
        ThemeManager.ThemeChanged += (s, e) =>
        {
            if (IsHandleCreated)
            {
                Invoke(() => ThemeManager.ApplyToForm(this));
            }
        };
        Load += (_, _) => ThemeManager.ApplyToForm(this);

        Text = "Administracion Checador - Sync SQL";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1200;
        Height = 760;
        MinimumSize = new Size(1080, 680);

        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(_rootLayout);

        var topBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12, 8, 12, 8)
        };

        var lblIcon = new Label
        {
            AutoSize = true,
            Text = "\uE90F",
            Font = new Font("Segoe MDL2 Assets", 19F, FontStyle.Regular),
            ForeColor = Color.FromArgb(0, 120, 215),
            Location = new Point(8, 10)
        };

        var lblTopTitle = new Label
        {
            AutoSize = true,
            Text = "Centro de administracion - Checador 2.0",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 39, 58),
            Location = new Point(48, 9)
        };

        var lblTopSubtitle = new Label
        {
            AutoSize = true,
            Text = "Sincroniza, monitorea y administra el reloj en tiempo real.",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(98, 110, 126),
            Tag = "secondary",
            Location = new Point(50, 33)
        };

        topBar.Controls.Add(lblIcon);
        topBar.Controls.Add(lblTopTitle);
        topBar.Controls.Add(lblTopSubtitle);
        _rootLayout.Controls.Add(topBar, 0, 0);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        _tabOperacion = new TabPage("Operación") { Padding = new Padding(12) };
        _tabEmpleados = new TabPage("Empleados") { Padding = new Padding(12) };
        _tabConfiguracion = new TabPage("Configuración") { Padding = new Padding(12) };
        _tabs.TabPages.AddRange(new[] { _tabOperacion, _tabEmpleados, _tabConfiguracion });
        _rootLayout.Controls.Add(_tabs, 0, 1);

        _tabOperacionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        _tabOperacionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        _tabOperacionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        _tabOperacionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _tabOperacionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        _tabOperacion.Controls.Add(_tabOperacionLayout);

        _tabEmpleadosLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        _tabEmpleadosLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        _tabEmpleadosLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _tabEmpleados.Controls.Add(_tabEmpleadosLayout);

        _tabConfiguracionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        _tabConfiguracionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        _tabConfiguracionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _tabConfiguracion.Controls.Add(_tabConfiguracionLayout);

        _actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true
        };

        _btnDownloadLogs = CreateButton("Descargar registros", btnDownloadLogs_Click, 170);
        _btnRefreshEmployees = CreateButton("Actualizar catalogo", btnRefreshEmployees_Click, 160);
        _btnRenameEmployee = CreateButton("Renombrar empleado", btnRenameEmployee_Click, 170);
        _btnAutoSyncGui = CreateButton("AutoSync GUI externo", btnAutoSyncGui_Click, 165);
        _btnWorkSchedules = CreateButton("Administrar horarios", btnWorkSchedules_Click, 160);
        _btnDeleteEmployee = CreateButton("Eliminar empleado", btnDeleteEmployee_Click, 145);
        _btnOvertimeFormat = CreateButton("Formato OT horas extra", btnOvertimeFormat_Click, 185);
        _btnSyncDeviceTime = CreateButton("Sincronizar hora", btnSyncDeviceTime_Click, 150);
        _btnPreviewPervasive = CreateButton("Preview Pervasive", btnPreviewPervasive_Click, 160);
        _btnNightShiftCleanup = CreateButton("Limpieza nocturna", btnNightShiftCleanup_Click, 165);
        _btnNightShiftCleanup.Visible = false;
        _btnChangeEmployeeRange = CreateButton("Cambio no. (rango)", btnChangeEmployeeRange_Click, 145);
        _btnRefreshStats = CreateButton("Refrescar", btnRefreshStats_Click, 120);
        _btnSecretManualPunch = CreateButton("Alta manual", btnSecretManualPunch_Click, 135);
        _btnSecretManualPunch.Visible = false;
        _btnSecretManualPunch.Enabled = false;

        _chkAutoSync = new CheckBox
        {
            Text = "Auto-sync cada",
            AutoSize = true,
            Padding = new Padding(8, 10, 0, 0)
        };
        _chkAutoSync.CheckedChanged += chkAutoSync_CheckedChanged;

        _numAutoSyncMinutes = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 240,
            Value = Math.Clamp(autoSyncMinutes, 1, 240),
            Width = 60
        };

        var lblMinutes = new Label
        {
            Text = "min",
            AutoSize = true,
            Tag = "secondary",
            Padding = new Padding(0, 10, 0, 0)
        };

        _lblStats = new Label
        {
            Dock = DockStyle.Fill,
            Height = 72,
            BorderStyle = BorderStyle.None,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 0, 0, 8),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Text = "Estado SQL: inicializando..."
        };

        _lblLiveConnection = new StatusPillLabel
        {
            Width = 250,
            Height = 28,
            Text = "Reloj: inicializando...",
            PillBackColor = Color.FromArgb(232, 242, 252),
            PillForeColor = Color.FromArgb(32, 73, 124),
            Tag = "semantic"
        };

        _lblLiveSync = new StatusPillLabel
        {
            Width = 230,
            Height = 28,
            Text = "Sync: N/D",
            PillBackColor = Color.FromArgb(242, 244, 248),
            PillForeColor = Color.FromArgb(64, 74, 88),
            Tag = "semantic"
        };

        _lblLiveClock = new Label
        {
            AutoSize = false,
            Height = 30,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Text = "Hora reloj: --"
        };

        _lblLiveUsers = new Label
        {
            AutoSize = false,
            Height = 30,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212),
            Tag = "semantic",
            Text = "Usuarios reloj: --"
        };

        _lblLiveSql = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 54,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(244, 248, 253),
            Padding = new Padding(12, 8, 12, 8),
            Tag = "semantic",
            Text = "Ultimo sync empleados: --"
        };

        _lblLiveDetail = new Label
        {
            AutoSize = false,
            Height = 28,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = Color.DimGray,
            Tag = "secondary",
            Text = "Detalle: --"
        };

        _lstRecentLogs = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 8F),
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0)
        };

        _lblPervasivePreview = new Label
        {
            AutoSize = false,
            Width = 1080,
            Height = 22,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(42, 52, 65),
            Text = "Previsualización payload Pervasive (antes de insertar):"
        };

        _dgvPervasivePreview = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            Margin = new Padding(0)
        };
        _dgvPervasivePreview.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);

        _actionPanel.Controls.AddRange(new Control[]
        {
            _btnDownloadLogs,
            _btnSyncDeviceTime,
            _btnPreviewPervasive,
            _btnNightShiftCleanup,
            _btnRefreshStats,
            _btnSecretManualPunch
        });
        _actionPanel.WrapContents = false;
        _actionPanel.Margin = new Padding(0, 0, 0, 6);
        _actionPanel.Padding = new Padding(0, 2, 0, 2);

        _autoSyncPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(4, 2, 0, 0)
        };
        _autoSyncPanel.Controls.AddRange(new Control[]
        {
            _chkAutoSync,
            _numAutoSyncMinutes,
            lblMinutes
        });

        _monitorPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0, 0, 0, 8)
        };
        _monitorPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        _monitorPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
        _monitorPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _monitorPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var pnlLiveMonitor = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        var liveCardsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0)
        };
        liveCardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        liveCardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        liveCardsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        liveCardsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        pnlLiveMonitor.Controls.Add(liveCardsGrid);

        _lblLiveClock.Dock = DockStyle.Fill;
        _lblLiveClock.TextAlign = ContentAlignment.MiddleLeft;
        _lblLiveClock.Margin = new Padding(0);

        _lblLiveUsers.Dock = DockStyle.Fill;
        _lblLiveUsers.TextAlign = ContentAlignment.MiddleLeft;
        _lblLiveUsers.Margin = new Padding(0);

        _lblLiveConnection.Dock = DockStyle.Left;
        _lblLiveConnection.Margin = new Padding(0);
        _lblLiveSync.Dock = DockStyle.Left;
        _lblLiveSync.Margin = new Padding(0);

        liveCardsGrid.Controls.Add(CreateLiveMonitorCard("Reloj", _lblLiveConnection), 0, 0);
        liveCardsGrid.Controls.Add(CreateLiveMonitorCard("Hora reloj", _lblLiveClock), 1, 0);
        liveCardsGrid.Controls.Add(CreateLiveMonitorCard("Usuarios", _lblLiveUsers), 0, 1);
        liveCardsGrid.Controls.Add(CreateLiveMonitorCard("Ultimo sync", _lblLiveSync), 1, 1);

        _lblLiveDetail.Dock = DockStyle.Fill;
        _lblLiveDetail.Padding = new Padding(2, 4, 0, 0);

        _monitorPanel.Controls.Add(_lblStats, 0, 0);
        _monitorPanel.Controls.Add(pnlLiveMonitor, 0, 1);
        _monitorPanel.Controls.Add(_lblLiveDetail, 0, 2);
        _monitorPanel.Controls.Add(_lstRecentLogs, 0, 3);

        _tabOperacionLayout.Controls.Add(_actionPanel, 0, 0);
        _tabOperacionLayout.Controls.Add(_autoSyncPanel, 0, 1);
        _tabOperacionLayout.Controls.Add(_monitorPanel, 0, 2);
        _tabOperacionLayout.Controls.Add(_lblLiveSql, 0, 3);

        _employeeActionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0)
        };
        _employeeActionsPanel.Controls.AddRange(new Control[]
        {
            _btnRefreshEmployees,
            _btnRenameEmployee,
            _btnOvertimeFormat
        });

        var riskSeparator = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 1,
            BackColor = Color.FromArgb(224, 224, 224),
            Margin = new Padding(0, 2, 0, 2)
        };

        var riskLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 20,
            Text = "Zona de riesgo — requiere confirmación",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 80, 20),
            Tag = "semantic",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 1, 0, 1)
        };

        var riskActionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0)
        };
        riskActionsPanel.Controls.AddRange(new Control[]
        {
            _btnDeleteEmployee,
            _btnChangeEmployeeRange
        });

        var employeeActionsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0)
        };
        employeeActionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        employeeActionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        employeeActionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        employeeActionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        employeeActionsLayout.Controls.Add(_employeeActionsPanel, 0, 0);
        employeeActionsLayout.Controls.Add(riskSeparator, 0, 1);
        employeeActionsLayout.Controls.Add(riskLabel, 0, 2);
        employeeActionsLayout.Controls.Add(riskActionsPanel, 0, 3);

        _tabEmpleadosLayout.Controls.Add(employeeActionsLayout, 0, 0);

        var employeesGroup = new RoundedGroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Catálogo de empleados (r reloj + SQL)",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        _dgvEmployees = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        _dgvEmployees.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);
        employeesGroup.Controls.Add(_dgvEmployees);
        _tabEmpleadosLayout.Controls.Add(employeesGroup, 0, 1);

        _configActionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            Margin = new Padding(0)
        };
        _configActionsPanel.Controls.AddRange(new Control[]
        {
            _btnWorkSchedules,
            _btnAutoSyncGui
        });
        _tabConfiguracionLayout.Controls.Add(_configActionsPanel, 0, 0);

        var logGroup = new RoundedGroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Bitácora / errores",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        _txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F)
        };
        logGroup.Controls.Add(_txtLog);
        _tabConfiguracionLayout.Controls.Add(logGroup, 0, 1);

        _autoSyncTimer = new System.Windows.Forms.Timer();
        _autoSyncTimer.Tick += autoSyncTimer_Tick;

        _liveMonitorTimer = new System.Windows.Forms.Timer
        {
            Interval = 15000
        };
        _liveMonitorTimer.Tick += liveMonitorTimer_Tick;

        KeyPreview = true;
        KeyDown += SyncAdminForm_KeyDown;

        CacheBaseSizes();
        ApplyModernStyle();
        ApplyResponsiveScale();

        Shown += SyncAdminForm_Shown;
        FormClosed += SyncAdminForm_FormClosed;
        Resize += (_, _) => ApplyResponsiveScale();
    }

    private void CacheBaseSizes()
    {
        var buttons = new[]
        {
            _btnDownloadLogs, _btnRefreshEmployees, _btnRenameEmployee, _btnDeleteEmployee,
            _btnOvertimeFormat, _btnAutoSyncGui, _btnWorkSchedules, _btnSyncDeviceTime, _btnPreviewPervasive, _btnNightShiftCleanup, _btnChangeEmployeeRange, _btnRefreshStats,
            _btnSecretManualPunch
        };

        foreach (var b in buttons)
        {
            _buttonBaseSizes[b] = b.Size;
        }
    }

    private static Button CreateButton(string text, EventHandler onClick, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0)
        };
        button.Click += onClick;
        return button;
    }

    private static Panel CreateLiveMonitorCard(string title, Control valueControl)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 245, 248),
            Padding = new Padding(10),
            Margin = new Padding(4),
            BorderStyle = BorderStyle.None
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(120, 120, 120),
            TextAlign = ContentAlignment.MiddleLeft
        };

        valueControl.Dock = DockStyle.Fill;
        valueControl.Margin = new Padding(0);

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(valueControl, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private void ApplyModernStyle()
    {
        BackColor = Color.FromArgb(243, 246, 251);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        var groups = EnumerateDescendants(this)
            .OfType<GroupBox>()
            .ToList();

        foreach (var group in groups)
        {
            group.BackColor = Color.White;
            group.ForeColor = Color.FromArgb(24, 39, 58);
            group.Padding = new Padding(10);
        }

        ButtonStyles.ApplyPrimary(_btnDownloadLogs);
        ButtonStyles.ApplySecondary(_btnRefreshStats);
        ButtonStyles.ApplySecondary(_btnSyncDeviceTime);
        ButtonStyles.ApplySecondary(_btnPreviewPervasive);
        ButtonStyles.ApplySecondary(_btnNightShiftCleanup);

        ButtonStyles.ApplySecondary(_btnRefreshEmployees);
        ButtonStyles.ApplySecondary(_btnRenameEmployee);
        ButtonStyles.ApplySecondary(_btnOvertimeFormat);
        ButtonStyles.ApplyDanger(_btnDeleteEmployee);
        ButtonStyles.ApplyDanger(_btnChangeEmployeeRange);

        ButtonStyles.ApplySecondary(_btnWorkSchedules);
        ButtonStyles.ApplySecondary(_btnAutoSyncGui);
        ButtonStyles.ApplySecondary(_btnSecretManualPunch);

        _chkAutoSync.ForeColor = Color.FromArgb(45, 55, 72);

        _lblStats.BackColor = Color.FromArgb(246, 249, 253);
        _lblStats.ForeColor = Color.FromArgb(42, 52, 65);
        _lblStats.BorderStyle = BorderStyle.None;

        _lblLiveConnection.PillBackColor = Color.FromArgb(232, 242, 252);
        _lblLiveConnection.PillForeColor = Color.FromArgb(32, 73, 124);
        _lblLiveConnection.Invalidate();

        _lblLiveSync.PillBackColor = Color.FromArgb(242, 244, 248);
        _lblLiveSync.PillForeColor = Color.FromArgb(64, 74, 88);
        _lblLiveSync.Invalidate();

        _lblLiveClock.ForeColor = Color.FromArgb(52, 64, 82);
        _lblLiveUsers.ForeColor = Color.FromArgb(0, 120, 212);
        _lblLiveDetail.ForeColor = Color.FromArgb(99, 110, 125);
        _lblLiveSql.ForeColor = Color.FromArgb(52, 64, 82);

        _lstRecentLogs.BackColor = Color.White;
        _lstRecentLogs.BorderStyle = BorderStyle.FixedSingle;
        _lstRecentLogs.ForeColor = Color.FromArgb(38, 48, 61);

        StyleGrid(_dgvEmployees);
        StyleGrid(_dgvPervasivePreview);

        _txtLog.BackColor = Color.FromArgb(248, 250, 253);
        _txtLog.ForeColor = Color.FromArgb(38, 48, 61);
        _txtLog.BorderStyle = BorderStyle.None;
    }

    private static IEnumerable<Control> EnumerateDescendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in EnumerateDescendants(child))
            {
                yield return nested;
            }
        }
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.RowHeadersVisible = false;
        grid.GridColor = Color.FromArgb(235, 240, 247);
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(35, 50, 70);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(227, 239, 255);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 30, 45);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 246, 252);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 45, 66);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        grid.ColumnHeadersHeight = 32;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.RowTemplate.Height = 30;
    }

    private void ApplyResponsiveScale()
    {
        var scale = Math.Clamp(ClientSize.Width / 1200f, 1f, 1.35f);

        _rootLayout.Padding = new Padding((int)Math.Round(12 * scale));

        foreach (var pair in _buttonBaseSizes)
        {
            var b = pair.Key;
            var baseSize = pair.Value;
            b.Size = new Size(
                (int)Math.Round(baseSize.Width * scale),
                (int)Math.Round(baseSize.Height * Math.Min(1.2f, scale)));
            b.Font = new Font(b.Font.FontFamily, Math.Min(10.2f, 9f * scale), b.Font.Style);
        }

        _lblLiveSql.Height = (int)Math.Round(54 * Math.Min(1.2f, scale));
        _lblLiveConnection.Width = (int)Math.Round(250 * Math.Min(1.1f, scale));
        _lblLiveSync.Width = (int)Math.Round(230 * Math.Min(1.1f, scale));
        _lstRecentLogs.Font = new Font("Consolas", 8F, FontStyle.Regular);

        _dgvEmployees.RowTemplate.Height = (int)Math.Round(30 * Math.Min(1.2f, scale));
        _actionPanel.Invalidate();
    }

    private async void SyncAdminForm_Shown(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(async () =>
        {
            await _syncService.EnsureReadyAsync();
            await RefreshStatsAsync();
            await RefreshLiveMonitorAsync();
            await LoadEmployeesAsync();
            _liveMonitorTimer.Start();
            Log("Módulo SQL listo. Puedes ejecutar 'Descargar Registros'.");
        }, "Error inicializando módulo SQL");
    }

    private void SyncAdminForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _autoSyncTimer.Stop();
        _liveMonitorTimer.Stop();
        _autoSyncControlForm?.Close();
    }

    private void SyncAdminForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Control || !e.Shift || e.KeyCode != Keys.M)
        {
            return;
        }

        if (_secretManualPunchUnlocked)
        {
            return;
        }

        _secretManualPunchUnlocked = true;
        _btnSecretManualPunch.Visible = true;
        _btnSecretManualPunch.Enabled = !_busy;
        Log("Modo secreto habilitado: alta manual de asistencias disponible.");
        _actionPanel.PerformLayout();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private async void btnDownloadLogs_Click(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(async () =>
        {
            Log("Iniciando Download Attendance Logs...");
            var result = await _syncService.DownloadAttendanceLogsAsync();
            Log(result.Message);
            await RefreshStatsAsync();
            await RefreshLiveMonitorAsync();
            await LoadEmployeesAsync();

            MessageBox.Show(
                $"✅ Descarga completada\n\n" +
                $"Empleados reloj: {result.DeviceEmployees}\n" +
                $"Marcas leídas: {result.DevicePunches}\n" +
                $"Insertadas: {result.InsertedPunches}\n" +
                $"Duplicadas: {result.DuplicatePunches}",
                "Sync SQL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }, "Error al descargar logs del checador");
    }

    private async void btnRefreshEmployees_Click(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(LoadEmployeesAsync, "Error cargando catálogo de empleados");
    }

    private async void btnRenameEmployee_Click(object? sender, EventArgs e)
    {
        if (_dgvEmployees.SelectedRows.Count == 0)
        {
            MessageBox.Show("Selecciona un empleado en la tabla para renombrarlo.", "Renombrar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedRow = _dgvEmployees.SelectedRows[0];
        var userId = Convert.ToInt32(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.UserId)].Value);
        var deviceName = Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.DeviceName)].Value) ?? string.Empty;
        var gridSqlName = Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.SqlName)].Value) ?? string.Empty;
        var gridDepartment = _dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.Department))
            ? Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.Department)].Value) ?? string.Empty
            : string.Empty;
        var gridPosition = _dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.Position))
            ? Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.Position)].Value) ?? string.Empty
            : string.Empty;
        var gridPervasiveDept = _dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.PervasiveDept))
            ? Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.PervasiveDept)].Value) ?? string.Empty
            : string.Empty;
        var gridPervasiveShift = _dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.PervasiveShift))
            ? Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.PervasiveShift)].Value) ?? string.Empty
            : string.Empty;
        var gridWorkScheduleCode = _dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.WorkScheduleCode))
            ? ParseNullableInt(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.WorkScheduleCode)].Value)
            : null;
        var gridOvertimeAuthorized = _dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.OvertimeAuthorized)) &&
            ParseBoolean(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.OvertimeAuthorized)].Value);

        var sqlSnapshot = await _syncService.GetEmployeeAdminSnapshotAsync(userId);
        var currentSqlName = PreferNonEmpty(sqlSnapshot?.SqlName, gridSqlName);
        var currentDepartment = PreferNonEmpty(sqlSnapshot?.Department, gridDepartment);
        var currentPosition = PreferNonEmpty(sqlSnapshot?.Position, gridPosition);
        var currentPervasiveDept = PreferNonEmpty(sqlSnapshot?.PervasiveDept, gridPervasiveDept);
        var currentPervasiveShift = PreferNonEmpty(sqlSnapshot?.PervasiveShift, gridPervasiveShift);
        var currentWorkScheduleCode = sqlSnapshot?.WorkScheduleCode ?? gridWorkScheduleCode;
        var currentOvertimeAuthorized = sqlSnapshot?.OvertimeAuthorized ?? gridOvertimeAuthorized;
        var workSchedules = await _syncService.GetWorkSchedulesAsync();

        Log($"Precarga Renombrar USERID {userId}: Horario='{FormatScheduleCode(currentWorkScheduleCode)}', DEPT='{(string.IsNullOrWhiteSpace(currentPervasiveDept) ? "(vacío)" : currentPervasiveDept)}', SHIFT='{(string.IsNullOrWhiteSpace(currentPervasiveShift) ? "(vacío)" : currentPervasiveShift)}'.");

        var initialName = string.IsNullOrWhiteSpace(currentSqlName) ? deviceName : currentSqlName;
        using var renameForm = new RenameEmployeeForm
        {
            EmpleadoId = userId.ToString(),
            NombreActual = initialName,
            NombreDispositivo = deviceName,
            Departamento = currentDepartment,
            Puesto = currentPosition,
            PervasiveDept = currentPervasiveDept,
            PervasiveShift = currentPervasiveShift,
            CodigoHorario = currentWorkScheduleCode?.ToString() ?? string.Empty,
            AutorizaHorasExtra = currentOvertimeAuthorized
        };
        renameForm.SetHorarioOptions(workSchedules);

        var dialogResult = renameForm.ShowDialog(this);
        var editData = dialogResult == DialogResult.OK
            ? new EmployeeAdminEditData(
                renameForm.NuevoNombre,
                NullIfWhiteSpace(renameForm.Departamento),
                NullIfWhiteSpace(renameForm.Puesto),
                NullIfWhiteSpace(renameForm.PervasiveDept),
                NullIfWhiteSpace(renameForm.PervasiveShift),
                int.TryParse(renameForm.CodigoHorario, out var scheduleCode) ? scheduleCode : null,
                renameForm.AutorizaHorasExtra)
            : null;
        if (editData is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(editData.Name))
        {
            MessageBox.Show("El nombre no puede quedar vacío.", "Renombrar empleado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var sameName = string.Equals(editData.Name.Trim(), initialName.Trim(), StringComparison.OrdinalIgnoreCase);
        var sameDepartment = string.Equals((editData.Department ?? string.Empty).Trim(), currentDepartment.Trim(), StringComparison.OrdinalIgnoreCase);
        var samePosition = string.Equals((editData.Position ?? string.Empty).Trim(), currentPosition.Trim(), StringComparison.OrdinalIgnoreCase);
        var samePervasiveDept = string.Equals((editData.PervasiveDept ?? string.Empty).Trim(), currentPervasiveDept.Trim(), StringComparison.OrdinalIgnoreCase);
        var samePervasiveShift = string.Equals((editData.PervasiveShift ?? string.Empty).Trim(), currentPervasiveShift.Trim(), StringComparison.OrdinalIgnoreCase);
        var sameWorkSchedule = editData.WorkScheduleCode == currentWorkScheduleCode;
        var sameOvertimeAuthorized = editData.OvertimeAuthorized == currentOvertimeAuthorized;
        if (sameName && sameDepartment && samePosition && samePervasiveDept && samePervasiveShift && sameWorkSchedule && sameOvertimeAuthorized)
        {
            return;
        }

        await SafeUiRunAsync(async () =>
        {
            var result = await _syncService.RenameEmployeeAsync(userId, editData.Name.Trim(), editData.Department, editData.Position, editData.PervasiveDept, editData.PervasiveShift, editData.WorkScheduleCode, editData.OvertimeAuthorized);
            Log($"Actualizado USERID {userId} en SQL. RH Depto: '{editData.Department ?? "(vacío)"}'. RH Puesto: '{editData.Position ?? "(vacío)"}'. Pervasive DEPT: '{editData.PervasiveDept ?? "(vacío)"}'. Pervasive SHIFT: '{editData.PervasiveShift ?? "(vacío)"}'.");
            Log($"Resultado reloj en renombrado USERID {userId}: {result.DeviceMessage}");

            await LoadEmployeesAsync();
            await RefreshStatsAsync();
            await RefreshLiveMonitorAsync();

            var icon = result.UpdatedOnDevice ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            MessageBox.Show(
                $"Datos guardados en SQL.\n\nNombre: {result.NewName}\n\nRH / Excel\n- Departamento: {editData.Department ?? "(vacío)"}\n- Puesto: {editData.Position ?? "(vacío)"}\n\nReglas de jornada\n- Horario: {FormatScheduleCode(editData.WorkScheduleCode)}\n- Horas extra autorizadas: {(editData.OvertimeAuthorized ? "Sí" : "No")}\n\nExtras Pervasive\n- DEPT: {editData.PervasiveDept ?? "(vacío)"}\n- SHIFT: {editData.PervasiveShift ?? "(vacío)"}\n\nDispositivo\n- Nombre en checador: {(result.UpdatedOnDevice ? "Actualizado" : "No confirmado")}\n- Detalle: {result.DeviceMessage}",
                "Actualizar datos RH + Pervasive",
                MessageBoxButtons.OK,
                icon);
        }, "Error renombrando empleado");
    }

    private async void btnOvertimeFormat_Click(object? sender, EventArgs e)
    {
        var suggestedUserId = 1;
        var suggestedName = string.Empty;

        if (_dgvEmployees.SelectedRows.Count > 0)
        {
            var selectedRow = _dgvEmployees.SelectedRows[0];
            suggestedUserId = Convert.ToInt32(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.UserId)].Value);

            var sqlName = Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.SqlName)].Value) ?? string.Empty;
            var deviceName = Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.DeviceName)].Value) ?? string.Empty;
            suggestedName = string.IsNullOrWhiteSpace(sqlName) ? deviceName : sqlName;
        }

        var request = PromptOvertimeFormatData(suggestedUserId, suggestedName);
        if (request is null)
        {
            return;
        }

        var safeOt = string.Concat(request.WorkOrderNumber
            .Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        if (string.IsNullOrWhiteSpace(safeOt))
        {
            safeOt = "OT";
        }

        using var saveDialog = new SaveFileDialog
        {
            Title = "Guardar formato OT de horas extra",
            Filter = "Excel (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            FileName = $"OT_{safeOt}_{request.UserId}_{request.FromDate:yyyyMMdd}_{request.ToDate:yyyyMMdd}.xlsx",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (saveDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await SafeUiRunAsync(() =>
        {
            _overtimeFormatGenerator.Generate(saveDialog.FileName, request);
            Log($"Formato OT generado: {saveDialog.FileName}");

            var openNow = MessageBox.Show(
                "Formato generado correctamente.\n\n¿Deseas abrirlo ahora para imprimirlo o firmarlo?",
                "Formato OT",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (openNow == DialogResult.Yes)
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = saveDialog.FileName,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }

            return Task.CompletedTask;
        }, "Error generando formato OT de horas extra");
    }

    private static OvertimeWorkOrderFormatData? PromptOvertimeFormatData(int suggestedUserId, string suggestedName)
    {
        using var prompt = new Form
        {
            Width = 980,
            Height = 760,
            MinimumSize = new Size(940, 720),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Formato OT - Solicitud y aprobación de horas extra",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            BackColor = Color.FromArgb(245, 248, 252),
            AutoScaleMode = AutoScaleMode.Dpi
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        prompt.Controls.Add(root);

        var topBar = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 8)
        };
        root.Controls.Add(topBar, 0, 0);

        var lblIcon = new Label
        {
            AutoSize = true,
            Text = "\uE90F",
            Font = new Font("Segoe MDL2 Assets", 19F, FontStyle.Regular),
            ForeColor = Color.FromArgb(0, 120, 215),
            Location = new Point(8, 10),
            Tag = "semantic"
        };
        var lblTopTitle = new Label
        {
            AutoSize = true,
            Text = "Formato OT · Solicitud de horas extra",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 39, 58),
            Location = new Point(48, 8)
        };
        var lblTopSubtitle = new Label
        {
            AutoSize = true,
            Text = "Captura OT, rango, días y firmas para formalizar aprobación.",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(98, 110, 126),
            Location = new Point(50, 32),
            Tag = "secondary"
        };
        topBar.Controls.Add(lblIcon);
        topBar.Controls.Add(lblTopTitle);
        topBar.Controls.Add(lblTopSubtitle);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0, 8, 0, 4)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 186));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(content, 0, 1);

        static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Tag = "secondary"
            };
        }

        var grpGeneral = new RoundedGroupBox
        {
            Text = "Datos Generales",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 8)
        };
        content.Controls.Add(grpGeneral, 0, 0);

        var generalGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4
        };
        generalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        generalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        generalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        generalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        generalGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        generalGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        generalGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        generalGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        grpGeneral.Controls.Add(generalGrid);

        var lblUser = CreateFieldLabel("USERID:");
        var numUser = new NumericUpDown
        {
            Width = 130,
            Minimum = 1,
            Maximum = 99999,
            Value = Math.Clamp(suggestedUserId, 1, 99999),
            Anchor = AnchorStyles.Left
        };

        var lblEmployee = CreateFieldLabel("Empleado:");
        var txtEmployee = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = string.IsNullOrWhiteSpace(suggestedName) ? $"Empleado_{suggestedUserId}" : suggestedName
        };

        var lblOt = CreateFieldLabel("Orden OT:");
        var txtOt = new TextBox { Dock = DockStyle.Fill };

        var lblRequestDate = CreateFieldLabel("Fecha solicitud:");
        var dtRequestDate = new DateTimePicker
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today
        };

        var lblFrom = CreateFieldLabel("Desde:");
        var dtFrom = new DateTimePicker
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today
        };

        var lblTo = CreateFieldLabel("Hasta:");
        var dtTo = new DateTimePicker
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today
        };

        var lblSchedule = CreateFieldLabel("Horario:");
        var txtSchedule = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = "Por definir"
        };

        generalGrid.Controls.Add(lblUser, 0, 0);
        generalGrid.Controls.Add(numUser, 1, 0);
        generalGrid.Controls.Add(lblEmployee, 2, 0);
        generalGrid.Controls.Add(txtEmployee, 3, 0);

        generalGrid.Controls.Add(lblOt, 0, 1);
        generalGrid.Controls.Add(txtOt, 1, 1);
        generalGrid.Controls.Add(lblRequestDate, 2, 1);
        generalGrid.Controls.Add(dtRequestDate, 3, 1);

        generalGrid.Controls.Add(lblFrom, 0, 2);
        generalGrid.Controls.Add(dtFrom, 1, 2);
        generalGrid.Controls.Add(lblTo, 2, 2);
        generalGrid.Controls.Add(dtTo, 3, 2);

        generalGrid.Controls.Add(lblSchedule, 0, 3);
        generalGrid.Controls.Add(txtSchedule, 1, 3);
        generalGrid.SetColumnSpan(txtSchedule, 3);

        var grpWork = new RoundedGroupBox
        {
            Text = "Trabajo A Realizar",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 8)
        };
        content.Controls.Add(grpWork, 0, 1);

        var txtWorkDescription = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true
        };
        grpWork.Controls.Add(txtWorkDescription);

        var grpDays = new RoundedGroupBox
        {
            Text = "Días De Trabajo",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 8)
        };
        content.Controls.Add(grpDays, 0, 2);

        var clbDays = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            MultiColumn = true,
            ColumnWidth = 130,
            IntegralHeight = false
        };
        grpDays.Controls.Add(clbDays);

        clbDays.Items.AddRange(new object[]
        {
            "Lunes",
            "Martes",
            "Miércoles",
            "Jueves",
            "Viernes",
            "Sábado",
            "Domingo"
        });
        for (var i = 0; i < 5; i++)
        {
            clbDays.SetItemChecked(i, true);
        }

        var grpSignatures = new RoundedGroupBox
        {
            Text = "Firmas Y Responsables",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 8)
        };
        content.Controls.Add(grpSignatures, 0, 3);

        var signaturesGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3
        };
        signaturesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        signaturesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        signaturesGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        signaturesGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        signaturesGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        grpSignatures.Controls.Add(signaturesGrid);

        var lblBoss = CreateFieldLabel("Jefe inmediato:");
        var txtBoss = new TextBox { Dock = DockStyle.Fill };

        var lblBossSignature = CreateFieldLabel("Firma jefe (texto):");
        var txtBossSignature = new TextBox { Dock = DockStyle.Fill };

        var lblEmployeeSignature = CreateFieldLabel("Firma empleado (texto):");
        var txtEmployeeSignature = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = txtEmployee.Text
        };

        signaturesGrid.Controls.Add(lblBoss, 0, 0);
        signaturesGrid.Controls.Add(txtBoss, 1, 0);
        signaturesGrid.Controls.Add(lblBossSignature, 0, 1);
        signaturesGrid.Controls.Add(txtBossSignature, 1, 1);
        signaturesGrid.Controls.Add(lblEmployeeSignature, 0, 2);
        signaturesGrid.Controls.Add(txtEmployeeSignature, 1, 2);

        var grpNotes = new RoundedGroupBox
        {
            Text = "Observaciones",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10)
        };
        content.Controls.Add(grpNotes, 0, 4);

        var txtNotes = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true
        };
        grpNotes.Controls.Add(txtNotes);

        var btnOk = new Button
        {
            Text = "Generar formato",
            Width = 130,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0),
            DialogResult = DialogResult.OK
        };
        var btnCancel = new Button
        {
            Text = "Cancelar",
            Width = 120,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0),
            DialogResult = DialogResult.Cancel
        };

        ButtonStyles.ApplyPrimary(btnOk);
        ButtonStyles.ApplySecondary(btnCancel);

        var footer = new Panel { Dock = DockStyle.Fill };
        root.Controls.Add(footer, 0, 2);

        var footerFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 8, 0, 0)
        };
        footer.Controls.Add(footerFlow);
        footerFlow.Controls.Add(btnCancel);
        footerFlow.Controls.Add(btnOk);

        var cards = new[]
        {
            grpGeneral,
            grpWork,
            grpDays,
            grpSignatures,
            grpNotes
        };

        void ApplyPromptTheme()
        {
            ThemeManager.ApplyToForm(prompt);

            prompt.BackColor = ThemeManager.Bg;
            root.BackColor = ThemeManager.Bg;
            content.BackColor = ThemeManager.Bg;
            topBar.BackColor = ThemeManager.BgSecondary;
            footer.BackColor = ThemeManager.Bg;
            lblIcon.ForeColor = ThemeManager.Primary;
            lblTopTitle.ForeColor = ThemeManager.Text;
            lblTopSubtitle.ForeColor = ThemeManager.TextSecond;

            foreach (var card in cards)
            {
                card.BackColor = ThemeManager.BgSecondary;
                card.ForeColor = ThemeManager.Text;
                card.BorderColor = ThemeManager.Border;
                card.ShadowOpacity = ThemeManager.IsDark ? 10 : 24;
                card.ShowShadow = !ThemeManager.IsDark;
                card.Invalidate();
            }

            numUser.BackColor = ThemeManager.BgSecondary;
            numUser.ForeColor = ThemeManager.Text;

            var datePickers = new[] { dtRequestDate, dtFrom, dtTo };
            foreach (var picker in datePickers)
            {
                picker.CalendarMonthBackground = ThemeManager.BgSecondary;
                picker.CalendarForeColor = ThemeManager.Text;
                picker.CalendarTitleBackColor = ThemeManager.BgTertiary;
                picker.CalendarTitleForeColor = ThemeManager.Text;
                picker.CalendarTrailingForeColor = ThemeManager.TextSecond;
            }

            clbDays.BackColor = ThemeManager.BgSecondary;
            clbDays.ForeColor = ThemeManager.Text;
            clbDays.BorderStyle = BorderStyle.FixedSingle;
        }

        EventHandler? themeHandler = null;
        themeHandler = (_, _) =>
        {
            if (!prompt.IsHandleCreated || prompt.IsDisposed)
            {
                return;
            }

            prompt.BeginInvoke((Action)ApplyPromptTheme);
        };

        ThemeManager.ThemeChanged += themeHandler;
        prompt.FormClosed += (_, _) => ThemeManager.ThemeChanged -= themeHandler;
        prompt.Shown += (_, _) => ApplyPromptTheme();

        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;

        if (prompt.ShowDialog() != DialogResult.OK)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(txtOt.Text))
        {
            MessageBox.Show("La orden OT es obligatoria.", "Formato OT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (string.IsNullOrWhiteSpace(txtEmployee.Text))
        {
            MessageBox.Show("El nombre del empleado es obligatorio.", "Formato OT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (string.IsNullOrWhiteSpace(txtWorkDescription.Text))
        {
            MessageBox.Show("Describe el trabajo a realizar para la OT.", "Formato OT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (dtFrom.Value.Date > dtTo.Value.Date)
        {
            MessageBox.Show("La fecha 'Desde' no puede ser mayor a la fecha 'Hasta'.", "Formato OT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (string.IsNullOrWhiteSpace(txtBoss.Text))
        {
            MessageBox.Show("El nombre del jefe inmediato es obligatorio.", "Formato OT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (string.IsNullOrWhiteSpace(txtBossSignature.Text))
        {
            MessageBox.Show("La firma del jefe inmediato (texto) es obligatoria.", "Formato OT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (string.IsNullOrWhiteSpace(txtEmployeeSignature.Text))
        {
            MessageBox.Show("La firma del empleado (texto) es obligatoria.", "Formato OT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var checkedDays = clbDays.CheckedItems
            .Cast<string>()
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (checkedDays.Count == 0)
        {
            checkedDays = GetSpanishDayNamesFromRange(dtFrom.Value.Date, dtTo.Value.Date);
        }

        var daysText = checkedDays.Count == 0 ? "Por definir" : string.Join(", ", checkedDays);
        var scheduleText = string.IsNullOrWhiteSpace(txtSchedule.Text) ? "Por definir" : txtSchedule.Text.Trim();

        return new OvertimeWorkOrderFormatData(
            (int)numUser.Value,
            txtEmployee.Text.Trim(),
            txtOt.Text.Trim(),
            txtWorkDescription.Text.Trim(),
            dtRequestDate.Value.Date,
            dtFrom.Value.Date,
            dtTo.Value.Date,
            daysText,
            scheduleText,
            txtBoss.Text.Trim(),
            txtBossSignature.Text.Trim(),
            txtEmployeeSignature.Text.Trim(),
            NullIfWhiteSpace(txtNotes.Text));
    }

    private static List<string> GetSpanishDayNamesFromRange(DateTime fromDate, DateTime toDate)
    {
        var names = new List<string>();
        for (var cursor = fromDate.Date; cursor <= toDate.Date; cursor = cursor.AddDays(1))
        {
            var name = GetSpanishDayName(cursor.DayOfWeek);
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static string GetSpanishDayName(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miércoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            DayOfWeek.Saturday => "Sábado",
            DayOfWeek.Sunday => "Domingo",
            _ => dayOfWeek.ToString()
        };
    }

    private async void btnDeleteEmployee_Click(object? sender, EventArgs e)
    {
        var empleadoSeleccionado = "empleado seleccionado";
        if (_dgvEmployees.SelectedRows.Count > 0)
        {
            var selectedRowForConfirm = _dgvEmployees.SelectedRows[0];
            var selectedUserId = Convert.ToInt32(selectedRowForConfirm.Cells[nameof(SqlEmployeeCatalogItem.UserId)].Value);
            var selectedSqlName = Convert.ToString(selectedRowForConfirm.Cells[nameof(SqlEmployeeCatalogItem.SqlName)].Value);
            var selectedDeviceName = Convert.ToString(selectedRowForConfirm.Cells[nameof(SqlEmployeeCatalogItem.DeviceName)].Value);
            var selectedName = string.IsNullOrWhiteSpace(selectedSqlName) ? selectedDeviceName : selectedSqlName;
            empleadoSeleccionado = string.IsNullOrWhiteSpace(selectedName)
                ? $"USERID {selectedUserId}"
                : $"{selectedName} (USERID {selectedUserId})";
        }

        var confirm = MessageBox.Show(
            $"¿Eliminar a {empleadoSeleccionado} del catálogo SQL?\n\n" +
            "Esta acción no puede deshacerse.\n" +
            "El historial de marcas se conserva.",
            "Confirmar eliminación",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        if (_dgvEmployees.SelectedRows.Count == 0)
        {
            MessageBox.Show("Selecciona un empleado en la tabla para eliminarlo.", "Eliminar empleado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var userId = Convert.ToInt32(_dgvEmployees.SelectedRows[0].Cells[nameof(SqlEmployeeCatalogItem.UserId)].Value);
        var currentName = Convert.ToString(_dgvEmployees.SelectedRows[0].Cells[nameof(SqlEmployeeCatalogItem.DeviceName)].Value) ?? "(sin nombre)";

        var answer = MessageBox.Show(
            $"Vas a eliminar el empleado {userId} - {currentName}.\n\n" +
            "Se eliminará del catálogo SQL y se intentará eliminar del reloj.\n" +
            "El historial de marcas se conservará para reportes.\n\n" +
            "¿Deseas continuar?",
            "Eliminar empleado",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        await SafeUiRunAsync(async () =>
        {
            var result = await _syncService.DeleteEmployeeAsync(userId, deleteHistoryRows: false);
            Log($"Eliminar USERID {userId}. SQL: {(result.DeletedInSql ? "OK" : "No encontrado")}. Dispositivo: {result.DeviceMessage}");

            await LoadEmployeesAsync();
            await RefreshStatsAsync();
            await RefreshLiveMonitorAsync();

            var icon = result.DeletedOnDevice ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            MessageBox.Show(
                $"SQL catálogo: {(result.DeletedInSql ? "Eliminado" : "No existía")}\n" +
                $"Dispositivo: {(result.DeletedOnDevice ? "Eliminado" : "No confirmado")}\n" +
                $"Detalle dispositivo: {result.DeviceMessage}\n" +
                "Historial de marcas: Conservado.",
                "Eliminar empleado",
                MessageBoxButtons.OK,
                icon);
        }, "Error eliminando empleado");
    }

    private void btnAutoSyncGui_Click(object? sender, EventArgs e)
    {
        if (_autoSyncControlForm is not null && !_autoSyncControlForm.IsDisposed)
        {
            _autoSyncControlForm.BringToFront();
            _autoSyncControlForm.Focus();
            return;
        }

        _autoSyncControlForm = new AutoSyncControlForm(_syncService, (int)_numAutoSyncMinutes.Value);
        _autoSyncControlForm.Show(this);
    }

    private async void btnWorkSchedules_Click(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(OpenWorkSchedulesDialogAsync, "Error administrando horarios");
    }

    private async void btnRefreshStats_Click(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(async () =>
        {
            await RefreshStatsAsync();
            await RefreshLiveMonitorAsync();
        }, "Error refrescando estado");
    }

    private async void btnSecretManualPunch_Click(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(OpenManualPunchDialogAsync, "Error insertando asistencia manual");
    }

    private async Task OpenManualPunchDialogAsync()
    {
        var suggestedUserId = 1;
        var suggestedName = string.Empty;

        if (_dgvEmployees.SelectedRows.Count > 0)
        {
            var selectedRow = _dgvEmployees.SelectedRows[0];
            suggestedUserId = Convert.ToInt32(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.UserId)].Value);

            var sqlName = Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.SqlName)].Value) ?? string.Empty;
            var deviceName = Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.DeviceName)].Value) ?? string.Empty;
            suggestedName = string.IsNullOrWhiteSpace(sqlName) ? deviceName : sqlName;
        }

        var request = await PromptManualPunchDataAsync(suggestedUserId, suggestedName);
        if (request is null)
        {
            return;
        }

        if (!PromptPassword("Autorizacion requerida para alta manual", out var typedPassword)
            || !string.Equals(typedPassword, SensitiveChangePassword, StringComparison.Ordinal))
        {
            Log("Alta manual denegada: contrasena invalida.");
            MessageBox.Show("Contrasena incorrecta. Operacion cancelada.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var hasBatch = request.BatchEntries is { Count: > 0 };
        var batchDetail = hasBatch
            ? string.Join("\n", request.BatchEntries!.Select(x => $"- {x.CheckTime:dd/MM/yyyy HH:mm:ss} · {FormatInOutMode(x.InOutMode)}"))
            : "(sin lote)";

        var answer = MessageBox.Show(
            "Se insertara una asistencia manual con este detalle:\n\n" +
            $"USERID: {request.UserId}\n" +
            $"Nombre: {request.EmployeeName}\n" +
            (hasBatch
                ? $"Marcas manuales seleccionadas ({request.BatchEntries!.Count}):\n{batchDetail}\n\n"
                : $"Fecha/hora: {request.CheckTime:dd/MM/yyyy HH:mm:ss}\nModo: {FormatInOutMode(request.InOutMode)}\n\n") +
            (!string.IsNullOrWhiteSpace(request.Notes) ? $"{request.Notes}\n\n" : string.Empty) +
            "Se aplicara el mismo flujo SQL/Pervasive de sincronizacion normal.",
            "Confirmar alta manual",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        var result = hasBatch
            ? await _syncService.InsertManualPunchesAsync(
                request.UserId,
                request.EmployeeName,
                request.BatchEntries!)
            : await _syncService.InsertManualPunchAsync(
                request.UserId,
                request.EmployeeName,
                request.CheckTime,
                request.InOutMode);

        if (hasBatch)
        {
            Log($"Alta manual por lote USERID {request.UserId}. Marcas={request.BatchEntries!.Count}, insertadas={result.InsertedPunches}, duplicadas={result.DuplicatePunches}. {result.Message}");
        }
        else
        {
            Log($"Alta manual USERID {request.UserId} {request.CheckTime:dd/MM/yyyy HH:mm:ss}. Insertadas={result.InsertedPunches}, duplicadas={result.DuplicatePunches}. {result.Message}");
        }

        await RefreshStatsAsync();
        await RefreshLiveMonitorAsync();
        await LoadEmployeesAsync();
        await LoadPervasivePreviewAsync();

        var icon = result.InsertedPunches > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
        MessageBox.Show(
            "Resultado de alta manual:\n\n" +
            $"Insertadas: {result.InsertedPunches}\n" +
            $"Duplicadas: {result.DuplicatePunches}\n\n" +
            result.Message,
            "Alta manual de asistencia",
            MessageBoxButtons.OK,
            icon);
    }

    private async void btnPreviewPervasive_Click(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(OpenPervasivePreviewDialogAsync, "Error cargando preview Pervasive");
    }

    private async void btnNightShiftCleanup_Click(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(OpenNightShiftCleanupDialogAsync, "Error ejecutando limpieza nocturna Pervasive");
    }

    private async void btnChangeEmployeeRange_Click(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "¿Cambiar el número de empleado en el rango seleccionado?\n\n" +
            "Esta operación modifica SOLO MSSQL (PunchLogs + catálogo Employees).\n" +
            "Se hará un preview antes de aplicar.",
            "Confirmar cambio por rango",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var suggestedOldUserId = 1;
        string defaultName = string.Empty;
        string? defaultDept = null;
        string? defaultShift = null;

        if (_dgvEmployees.SelectedRows.Count > 0)
        {
            var selectedRow = _dgvEmployees.SelectedRows[0];
            suggestedOldUserId = Convert.ToInt32(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.UserId)].Value);
            defaultName = Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.SqlName)].Value) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.DeviceName)].Value) ?? string.Empty;
            }

            defaultDept = _dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.PervasiveDept))
                ? Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.PervasiveDept)].Value)
                : null;
            defaultShift = _dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.PervasiveShift))
                ? Convert.ToString(selectedRow.Cells[nameof(SqlEmployeeCatalogItem.PervasiveShift)].Value)
                : null;
        }

        if (!PromptPassword("Autorización requerida para cambio de número por rango", out var typedPassword)
            || !string.Equals(typedPassword, SensitiveChangePassword, StringComparison.Ordinal))
        {
            Log("Cambio por rango denegado: contraseña inválida.");
            MessageBox.Show("Contraseña incorrecta. Operación cancelada.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var request = PromptEmployeeReassignData(suggestedOldUserId, defaultName ?? string.Empty, defaultDept, defaultShift);
        if (request is null)
        {
            return;
        }

        await SafeUiRunAsync(async () =>
        {
            var preview = await _syncService.PreviewEmployeeReassignByDateRangeAsync(
                request.OldUserId,
                request.NewUserId,
                request.FromDate,
                request.ToDate,
                includePervasive: false);

            var previewMessage = BuildEmployeeReassignValidationMessage(preview);
            var answer = MessageBox.Show(
                previewMessage + "\n\n¿Deseas aplicar el cambio ahora?",
                "Validación previa del cambio",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes)
            {
                Log($"Cambio por rango cancelado por usuario. USERID origen={request.OldUserId}, destino={request.NewUserId}.");
                return;
            }

            var result = await _syncService.ReassignEmployeeByDateRangeAsync(
                request.OldUserId,
                request.NewUserId,
                request.FromDate,
                request.ToDate,
                request.NewName,
                null,
                request.NewPervasiveDept,
                request.NewPervasiveShift,
                allowSkipConflicts: true,
                includePervasive: false);

            Log(result.Message);
            await LoadEmployeesAsync();
            await RefreshStatsAsync();
            await RefreshLiveMonitorAsync();
            await LoadPervasivePreviewAsync();

            MessageBox.Show(result.Message, "Cambio por rango aplicado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, "Error aplicando cambio por rango");
    }


    private async void btnSyncDeviceTime_Click(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(async () =>
        {
            var result = await _syncService.SyncDeviceTimeAsync();
            var icon = result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            MessageBox.Show(result.Message, "Sincronizar hora del reloj", MessageBoxButtons.OK, icon);
            Log($"Ajuste de hora reloj: {result.Message}");
            await RefreshLiveMonitorAsync();
        }, "Error sincronizando hora del reloj");
    }

    private void chkAutoSync_CheckedChanged(object? sender, EventArgs e)
    {
        if (_chkAutoSync.Checked)
        {
            var minutes = (int)_numAutoSyncMinutes.Value;
            _autoSyncTimer.Interval = Math.Max(1, minutes) * 60 * 1000;
            _autoSyncTimer.Start();
            Log($"Auto-sync habilitado cada {minutes} minutos.");
        }
        else
        {
            _autoSyncTimer.Stop();
            Log("Auto-sync deshabilitado.");
        }
    }

    private async void autoSyncTimer_Tick(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(async () =>
        {
            var result = await _syncService.DownloadAttendanceLogsAsync();
            Log($"Auto-sync OK: {result.Message}");
            await RefreshStatsAsync();
            await RefreshLiveMonitorAsync();
        }, "Error en auto-sync", showMessageBox: false);
    }

    private async void liveMonitorTimer_Tick(object? sender, EventArgs e)
    {
        await SafeUiRunAsync(RefreshLiveMonitorAsync, "Error actualizando monitor en vivo", showMessageBox: false);
    }

    private async Task LoadEmployeesAsync()
    {
        var data = await _syncService.GetEmployeeCatalogAsync();
        _dgvEmployees.DataSource = data.ToList();
        ConfigureEmployeeGridColumns();
    }

    private async Task RefreshStatsAsync()
    {
        var stats = await _syncService.GetStatsAsync();
        var lastPunch = stats.LastPunch.HasValue ? stats.LastPunch.Value.ToString("dd/MM/yyyy HH:mm:ss") : "Sin marcas";
        var lastSync = stats.LastSync.HasValue ? stats.LastSync.Value.ToString("dd/MM/yyyy HH:mm:ss") : "Sin sync";

        _lblStats.Text =
            "Estado SQL Server" + Environment.NewLine +
            $"Empleados: {stats.TotalEmployees}    Marcas: {stats.TotalPunches}" + Environment.NewLine +
            $"Ultima marca: {lastPunch}" + Environment.NewLine +
            $"Sync general: {lastSync}    Estado: {stats.LastStatus}";

        Log($"Estado sync: {stats.LastStatus}. {stats.LastMessage}");
    }

    private async Task RefreshLiveMonitorAsync()
    {
        var live = await _syncService.GetLiveStatusAsync();
        var recent = await _syncService.GetRecentHistoryAsync(5);

        var deviceStatus = live.DeviceConnected ? "Conectado" : "Sin conexión";
        var deviceTime = live.DeviceTime.HasValue ? live.DeviceTime.Value.ToString("dd/MM/yyyy HH:mm:ss") : "No disponible";
        var deviceUsers = live.DeviceUsers.HasValue ? live.DeviceUsers.Value.ToString() : "N/D";

        _lblLiveConnection.Text = $"Reloj: {deviceStatus}";
        _lblLiveConnection.PillBackColor = live.DeviceConnected ? Color.FromArgb(222, 247, 236) : Color.FromArgb(255, 234, 234);
        _lblLiveConnection.PillForeColor = live.DeviceConnected ? Color.FromArgb(12, 84, 30) : Color.FromArgb(114, 28, 36);
        _lblLiveConnection.Invalidate();

        var isSyncOk = string.Equals(live.LastSyncStatus, "OK", StringComparison.OrdinalIgnoreCase);
        _lblLiveSync.Text = $"Sync: {(string.IsNullOrWhiteSpace(live.LastSyncStatus) ? "N/D" : live.LastSyncStatus)}";
        _lblLiveSync.PillBackColor = isSyncOk ? Color.FromArgb(234, 250, 240) : Color.FromArgb(255, 245, 228);
        _lblLiveSync.PillForeColor = isSyncOk ? Color.FromArgb(22, 106, 48) : Color.FromArgb(140, 95, 16);
        _lblLiveSync.Invalidate();

        _lblLiveClock.Text = $"Hora del reloj: {deviceTime}";
        _lblLiveUsers.Text = $"Usuarios en reloj: {deviceUsers}";
        var syncTime = live.LastSync.HasValue ? live.LastSync.Value.ToString("dd/MM/yyyy HH:mm:ss") : "N/D";
        var syncStatusText = string.IsNullOrWhiteSpace(live.LastSyncStatus) ? "N/D" : live.LastSyncStatus;
        _lblLiveSql.Text =
            $"Ultimo sync empleados: {syncTime}   -   Estado: {syncStatusText}" + Environment.NewLine +
            $"SQL empleados: {live.SqlEmployees}   -   SQL marcas: {live.SqlPunches}";

        var syncOk = string.Equals(live.LastSyncStatus, "OK", StringComparison.OrdinalIgnoreCase);
        _lblLiveSql.BackColor = syncOk ? Color.FromArgb(238, 250, 241) : Color.FromArgb(255, 248, 236);

        _lblLiveDetail.Text = string.IsNullOrWhiteSpace(live.DeviceMessage)
            ? "Detalle: OK"
            : $"Detalle: {live.DeviceMessage}";

        _lstRecentLogs.BeginUpdate();
        try
        {
            _lstRecentLogs.Items.Clear();
            if (recent.Count == 0)
            {
                _lstRecentLogs.Items.Add("Sin registros recientes.");
            }
            else
            {
                foreach (var row in recent)
                {
                    var employeeName = FormatRecentEmployeeName(row.EmployeeName, 26);
                    _lstRecentLogs.Items.Add($"{row.CheckTime:dd/MM HH:mm:ss}   {row.UserId,5}   {employeeName}");
                }
            }
        }
        finally
        {
            _lstRecentLogs.EndUpdate();
        }
    }

    private static string FormatRecentEmployeeName(string? name, int maxChars)
    {
        var value = string.IsNullOrWhiteSpace(name) ? "(sin nombre)" : name.Trim();
        return value.Length <= maxChars ? value : value[..(maxChars - 3)] + "...";
    }

    private async Task LoadPervasivePreviewAsync()
    {
        var data = await _syncService.GetPervasivePayloadPreviewAsync();
        _dgvPervasivePreview.DataSource = data.Take(200).ToList();
        ConfigurePervasivePreviewGridColumns();

        if (data.Count == 0)
        {
            Log("Preview Pervasive: sin datos (espejo deshabilitado o no hay marcas nuevas en rango). ");
            return;
        }

        var inserts = data.Count(x => x.Action == "INSERT");
        var updates = data.Count(x => x.Action == "UPDATE");
        var correctiveUpdates = data.Count(x => x.Action == "UPDATE_CORRECTIVE");
        var skips = data.Count(x => x.Action == "SKIP");
        var guardSkips = data.Count(x => x.Action == "SKIP_GUARD");
        var abortGuards = data.Count(x => x.Action == "ABORT_GUARD");
        Log($"Preview Pervasive (resumen): {data.Count} filas. INSERT={inserts}, UPDATE={updates}, UPDATE_CORRECTIVE={correctiveUpdates}, SKIP={skips}, SKIP_GUARD={guardSkips}, ABORT_GUARD={abortGuards}. Mostrando 200 en panel.");
    }

    private async Task OpenPervasivePreviewDialogAsync()
    {
        var data = await _syncService.GetPervasivePayloadPreviewAsync();
        _dgvPervasivePreview.DataSource = data.Take(200).ToList();
        ConfigurePervasivePreviewGridColumns();

        if (data.Count == 0)
        {
            Log("Preview Pervasive: sin datos (espejo deshabilitado o no hay marcas nuevas en rango). ");
            MessageBox.Show(
                "No hay filas para previsualizar en este momento (espejo deshabilitado o sin marcas nuevas en rango).",
                "Preview Pervasive",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var inserts = data.Count(x => x.Action == "INSERT");
        var updates = data.Count(x => x.Action == "UPDATE");
        var correctiveUpdates = data.Count(x => x.Action == "UPDATE_CORRECTIVE");
        var skips = data.Count(x => x.Action == "SKIP");
        var guardSkips = data.Count(x => x.Action == "SKIP_GUARD");
        var abortGuards = data.Count(x => x.Action == "ABORT_GUARD");
        Log($"Preview Pervasive (completo): {data.Count} filas. INSERT={inserts}, UPDATE={updates}, UPDATE_CORRECTIVE={correctiveUpdates}, SKIP={skips}, SKIP_GUARD={guardSkips}, ABORT_GUARD={abortGuards}.");

        using var dialog = new PervasivePreviewForm(data);
        dialog.ShowDialog(this);
    }

    private async Task OpenNightShiftCleanupDialogAsync()
    {
        int? suggestedUserId = null;
        if (_dgvEmployees.SelectedRows.Count > 0)
        {
            suggestedUserId = Convert.ToInt32(_dgvEmployees.SelectedRows[0].Cells[nameof(SqlEmployeeCatalogItem.UserId)].Value);
        }

        var request = PromptNightShiftCleanupData(suggestedUserId);
        if (request is null)
        {
            return;
        }

        if (!PromptPassword("Autorización requerida para limpieza correctiva nocturna", out var typedPassword)
            || !string.Equals(typedPassword, SensitiveChangePassword, StringComparison.Ordinal))
        {
            Log("Limpieza nocturna denegada: contraseña inválida.");
            MessageBox.Show("Contraseña incorrecta. Operación cancelada.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var scope = request.UserId.HasValue
            ? $"USERID {request.UserId.Value}"
            : "todos los empleados con horario 3";

        var answer = MessageBox.Show(
            "Se ejecutará una limpieza correctiva de renglones partidos en Pervasive.\n\n" +
            $"Rango: {request.FromDate:dd/MM/yyyy} a {request.ToDate:dd/MM/yyyy}\n" +
            $"Alcance: {scope}\n\n" +
            "La operación recalcula espejo para horario 3 y elimina renglones huérfanos enlazados a la salida nocturna previa.\n\n" +
            "¿Deseas continuar?",
            "Confirmar limpieza nocturna Pervasive",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        var result = await _syncService.RunPervasiveNightShiftHistoricalCleanupAsync(
            request.FromDate,
            request.ToDate,
            request.UserId);

        if (result.CandidatePunches == 0 && !request.UserId.HasValue)
        {
            Log("Limpieza nocturna: sin candidatos globales. Sugerencia: ejecuta por USERID específico o asigna Horario 3 en catálogo.");
        }

        Log(result.Message);

        await RefreshStatsAsync();
        await RefreshLiveMonitorAsync();
        await LoadPervasivePreviewAsync();

        var icon = result.Enabled && result.ProcessedPunches > 0
            ? MessageBoxIcon.Information
            : MessageBoxIcon.Warning;

        MessageBox.Show(
            "Resultado limpieza nocturna:\n\n" +
            $"Rango: {result.FromDate:dd/MM/yyyy} a {result.ToDate:dd/MM/yyyy}\n" +
            $"Marcas candidatas: {result.CandidatePunches}\n" +
            $"Marcas procesadas: {result.ProcessedPunches}\n" +
            $"Usuarios afectados: {result.AffectedUsers}\n" +
            $"Renglones huérfanos eliminados: {result.DeletedSplitRows}\n" +
            (result.TruncatedByTop ? "\nAviso: hubo tope de filas procesadas en esta corrida.\n" : "\n") +
            $"\nDetalle espejo: {result.MirrorMessage}",
            "Limpieza nocturna Pervasive",
            MessageBoxButtons.OK,
            icon);
    }

    private void ConfigureEmployeeGridColumns()
    {
        if (_dgvEmployees.Columns.Count == 0)
        {
            return;
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.UserId)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.UserId)].HeaderText = "Numero de Empleado";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.DeviceName)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.DeviceName)].HeaderText = "Nombre de empleado";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.SqlName)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.SqlName)].HeaderText = "Nombre en base de datos";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.SqlUpdatedAt)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.SqlUpdatedAt)].HeaderText = "Actualizado en SQL";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.Department)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.Department)].HeaderText = "Departamento";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.Position)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.Position)].HeaderText = "Puesto (Excel)";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.PervasiveDept)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.PervasiveDept)].HeaderText = "DEPT (Pervasive)";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.PervasiveShift)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.PervasiveShift)].HeaderText = "SHIFT (Pervasive)";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.WorkScheduleCode)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.WorkScheduleCode)].HeaderText = "Horario";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.OvertimeAuthorized)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.OvertimeAuthorized)].HeaderText = "Autoriza horas extra";
        }

        if (_dgvEmployees.Columns.Contains(nameof(SqlEmployeeCatalogItem.StandardShiftHours)))
        {
            _dgvEmployees.Columns[nameof(SqlEmployeeCatalogItem.StandardShiftHours)].HeaderText = "Horas jornada";
        }
    }

    private void ConfigurePervasivePreviewGridColumns()
    {
        if (_dgvPervasivePreview.Columns.Count == 0)
        {
            return;
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.UserId)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.UserId)].HeaderText = "USERID";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.Action)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.Action)].HeaderText = "Acción";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.Employee)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.Employee)].HeaderText = "EMPLOYEE";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.EmployeeName)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.EmployeeName)].HeaderText = "NAME";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.ChargeDate)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.ChargeDate)].HeaderText = "CHARGE_DATE";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.InTsMin)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.InTsMin)].HeaderText = "IN_TS_MIN";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.OutTsMin)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.OutTsMin)].HeaderText = "OUT_TS_MIN";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.Dept)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.Dept)].HeaderText = "DEPT";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.Shift)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.Shift)].HeaderText = "SHIFT";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.LastChangeProgram)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.LastChangeProgram)].HeaderText = "LAST_CHG_PROG";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.ActualDate)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.ActualDate)].HeaderText = "ACTUAL_DATE";
        }

        if (_dgvPervasivePreview.Columns.Contains(nameof(PervasivePayloadPreviewItem.Notes)))
        {
            _dgvPervasivePreview.Columns[nameof(PervasivePayloadPreviewItem.Notes)].HeaderText = "Notas";
        }
    }

    private async Task SafeUiRunAsync(Func<Task> action, string contextErrorTitle, bool showMessageBox = true)
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log($"ERROR: {contextErrorTitle}. Detalle: {ex.Message}");
            if (showMessageBox)
            {
                MessageBox.Show(
                    $"{contextErrorTitle}\n\nDetalle: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UseWaitCursor = busy;

        _btnDownloadLogs.Enabled = !busy;
        _btnRefreshEmployees.Enabled = !busy;
        _btnRenameEmployee.Enabled = !busy;
        _btnOvertimeFormat.Enabled = !busy;
        _btnAutoSyncGui.Enabled = !busy;
        _btnWorkSchedules.Enabled = !busy;
        _btnDeleteEmployee.Enabled = !busy;
        _btnSyncDeviceTime.Enabled = !busy;
        _btnPreviewPervasive.Enabled = !busy;
        _btnNightShiftCleanup.Enabled = !busy;
        _btnChangeEmployeeRange.Enabled = !busy;
        _btnRefreshStats.Enabled = !busy;
        _btnSecretManualPunch.Enabled = !busy && _secretManualPunchUnlocked;
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (string.IsNullOrWhiteSpace(_txtLog.Text))
        {
            _txtLog.Text = line;
        }
        else
        {
            _txtLog.AppendText(Environment.NewLine + line);
        }

        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.ScrollToCaret();
    }

    private async Task OpenWorkSchedulesDialogAsync()
    {
        var schedules = (await _syncService.GetWorkSchedulesAsync())
            .OrderBy(x => x.Code)
            .Select(x => new WorkScheduleEditRow
            {
                Code = x.Code,
                Name = x.Name,
                StartHour12 = To12HourText(x.StartTime),
                StartAmPm = ToAmPm(x.StartTime),
                EndHour12 = To12HourText(x.EndTime),
                EndAmPm = ToAmPm(x.EndTime),
                StandardHours = x.StandardHours,
                IsActive = x.IsActive
            })
            .ToList();

        if (schedules.Count == 0)
        {
            schedules.Add(new WorkScheduleEditRow { Code = 1, Name = "Horario 1", StartHour12 = "08:00", StartAmPm = "AM", EndHour12 = "05:36", EndAmPm = "PM", StandardHours = 9.60m, IsActive = true });
            schedules.Add(new WorkScheduleEditRow { Code = 2, Name = "Horario 2", StartHour12 = "09:00", StartAmPm = "AM", EndHour12 = "06:36", EndAmPm = "PM", StandardHours = 9.60m, IsActive = true });
            schedules.Add(new WorkScheduleEditRow { Code = 3, Name = "Horario 3", StartHour12 = "06:00", StartAmPm = "PM", EndHour12 = "07:00", EndAmPm = "AM", StandardHours = 9.60m, IsActive = true });
        }

        using var prompt = new Form
        {
            Width = 860,
            Height = 560,
            FormBorderStyle = FormBorderStyle.Sizable,
            Text = "Configuración de horarios RH",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = true,
            BackColor = Color.FromArgb(245, 248, 252),
            MinimumSize = new Size(760, 500)
        };

        var lblTitle = new Label
        {
            Left = 16,
            Top = 14,
            Width = 760,
            Height = 42,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 39, 58),
            Text = "Define qué significan Horario 1, 2 y 3 (nombre, entrada, salida y horas estándar)."
        };

        var grid = new DataGridView
        {
            Left = 16,
            Top = 62,
            Width = 810,
            Height = 390,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WorkScheduleEditRow.Code), HeaderText = "Código", ReadOnly = true, Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WorkScheduleEditRow.Name), HeaderText = "Nombre", Width = 220 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WorkScheduleEditRow.StartHour12), HeaderText = "Entrada (hh:mm)", Width = 125 });
        grid.Columns.Add(new DataGridViewComboBoxColumn { DataPropertyName = nameof(WorkScheduleEditRow.StartAmPm), HeaderText = "AM/PM", Width = 80, DataSource = new[] { "AM", "PM" } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WorkScheduleEditRow.EndHour12), HeaderText = "Salida (hh:mm)", Width = 125 });
        grid.Columns.Add(new DataGridViewComboBoxColumn { DataPropertyName = nameof(WorkScheduleEditRow.EndAmPm), HeaderText = "AM/PM", Width = 80, DataSource = new[] { "AM", "PM" } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WorkScheduleEditRow.StandardHours), HeaderText = "Horas estándar", Width = 120 });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(WorkScheduleEditRow.IsActive), HeaderText = "Activo", Width = 80 });

        grid.DataSource = new BindingSource { DataSource = schedules };

        var lblHint = new Label
        {
            Left = 16,
            Top = 460,
            Width = 540,
            Height = 40,
            ForeColor = Color.DimGray,
            Text = "Tip: usa hora + AM/PM (ej. 08:00 AM, 05:36 PM). Internamente se guarda en formato 24h."
        };

        var btnSave = new Button { Text = "Guardar horarios", Left = 586, Top = 468, Width = 120, Height = 34, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancelar", Left = 714, Top = 468, Width = 112, Height = 34, DialogResult = DialogResult.Cancel };

        btnSave.FlatStyle = FlatStyle.Flat;
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.BackColor = Color.FromArgb(0, 120, 215);
        btnSave.ForeColor = Color.White;

        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 230);
        btnCancel.BackColor = Color.White;

        prompt.Controls.Add(lblTitle);
        prompt.Controls.Add(grid);
        prompt.Controls.Add(lblHint);
        prompt.Controls.Add(btnSave);
        prompt.Controls.Add(btnCancel);
        prompt.AcceptButton = btnSave;
        prompt.CancelButton = btnCancel;

        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var toSave = ((IEnumerable<WorkScheduleEditRow>)((BindingSource)grid.DataSource).List.Cast<WorkScheduleEditRow>()).ToList();
        foreach (var row in toSave)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                throw new InvalidOperationException($"El nombre del horario {row.Code} es obligatorio.");
            }

            if (!TryParseHourAmPm(row.StartHour12, row.StartAmPm, out var startTime))
            {
                throw new InvalidOperationException($"Entrada inválida en horario {row.Code}. Usa hh:mm + AM/PM.");
            }

            if (!TryParseHourAmPm(row.EndHour12, row.EndAmPm, out var endTime))
            {
                throw new InvalidOperationException($"Salida inválida en horario {row.Code}. Usa hh:mm + AM/PM.");
            }

            if (row.StandardHours <= 0)
            {
                throw new InvalidOperationException($"Horas estándar inválidas en horario {row.Code}.");
            }

            await _syncService.UpsertWorkScheduleAsync(row.Code, row.Name, startTime, endTime, row.StandardHours, row.IsActive);
        }

        Log("Horarios RH actualizados correctamente.");
        MessageBox.Show("Horarios guardados. Ya puedes asignarlos en Renombrar.", "Horarios RH", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string To12HourText(TimeSpan time)
    {
        var hour = time.Hours;
        var minute = time.Minutes;
        var hour12 = hour % 12;
        if (hour12 == 0)
        {
            hour12 = 12;
        }

        return $"{hour12:00}:{minute:00}";
    }

    private static string ToAmPm(TimeSpan time)
        => time.Hours >= 12 ? "PM" : "AM";

    private static bool TryParseHourAmPm(string? hourText, string? amPm, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(hourText) || string.IsNullOrWhiteSpace(amPm))
        {
            return false;
        }

        var parts = hourText.Trim().Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var hour12) || !int.TryParse(parts[1], out var minute))
        {
            return false;
        }

        if (hour12 < 1 || hour12 > 12 || minute < 0 || minute > 59)
        {
            return false;
        }

        var normalized = amPm.Trim().ToUpperInvariant();
        if (normalized is not ("AM" or "PM"))
        {
            return false;
        }

        var hour24 = hour12 % 12;
        if (normalized == "PM")
        {
            hour24 += 12;
        }

        time = new TimeSpan(hour24, minute, 0);
        return true;
    }

    private static ComboBox CreatePervasiveLabelComboBox(int left, int top, string initialValue, IReadOnlyList<PervasiveCodeOption> options, int width)
    {
        var normalizedInitial = initialValue?.Trim() ?? string.Empty;
        var data = options.ToList();
        var hasCurrent = data.Any(x =>
            string.Equals(x.Code, normalizedInitial, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Label, normalizedInitial, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.DisplayText, normalizedInitial, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(normalizedInitial) && !hasCurrent)
        {
            data.Insert(1, new PervasiveCodeOption(normalizedInitial, $"Actual: {normalizedInitial}"));
        }

        var combo = new ComboBox
        {
            Left = left,
            Top = top,
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(PervasiveCodeOption.Label),
            ValueMember = nameof(PervasiveCodeOption.Code),
            DataSource = data,
            DropDownWidth = Math.Max(width, 340)
        };

        var selected = data.FirstOrDefault(x =>
            string.Equals(x.Code, initialValue?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Label, initialValue?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.DisplayText, initialValue?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (selected is not null)
        {
            combo.SelectedValue = selected.Code;
        }
        else if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }

        return combo;
    }

    private static TextBox CreatePervasiveCodeTextBox(int left, int top)
    {
        return new TextBox
        {
            Left = left,
            Top = top,
            Width = 120,
            ReadOnly = true,
            BackColor = Color.FromArgb(245, 247, 250),
            TextAlign = HorizontalAlignment.Center
        };
    }

    private static void SyncPervasiveCodeText(ComboBox combo, TextBox textBox)
    {
        if (combo.SelectedItem is PervasiveCodeOption selected)
        {
            textBox.Text = selected.Code;
            return;
        }

        textBox.Text = combo.SelectedValue as string ?? string.Empty;
    }

    private static string? GetSelectedPervasiveCode(ComboBox combo)
    {
        if (combo.SelectedItem is PervasiveCodeOption selected)
        {
            return selected.Code;
        }

        return combo.SelectedValue as string;
    }

    private static string PreferNonEmpty(string? primary, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim();
    }

    private static int? ParseNullableInt(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        return int.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
    }

    private static bool ParseBoolean(object? value)
    {
        if (value is bool b)
        {
            return b;
        }

        return bool.TryParse(Convert.ToString(value), out var parsed) && parsed;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatScheduleCode(int? code)
    {
        return code.HasValue ? code.Value.ToString() : "(sin seleccionar)";
    }

    private static bool PromptPassword(string title, out string password)
    {
        password = string.Empty;

        using var prompt = new Form
        {
            Width = 430,
            Height = 190,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            BackColor = Color.White
        };

        var lbl = new Label
        {
            Left = 20,
            Top = 18,
            Width = 380,
            Text = "Ingresa contraseña de autorización para continuar:"
        };

        var txt = new TextBox
        {
            Left = 20,
            Top = 46,
            Width = 380,
            UseSystemPasswordChar = true
        };

        var btnOk = new Button { Text = "Validar", Left = 220, Top = 90, Width = 85, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancelar", Left = 315, Top = 90, Width = 85, DialogResult = DialogResult.Cancel };

        prompt.Controls.Add(lbl);
        prompt.Controls.Add(txt);
        prompt.Controls.Add(btnOk);
        prompt.Controls.Add(btnCancel);
        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;

        if (prompt.ShowDialog() != DialogResult.OK)
        {
            return false;
        }

        password = txt.Text;
        return true;
    }

    private async Task<ManualPunchRequest?> PromptManualPunchDataAsync(int suggestedUserId, string suggestedName)
    {
        var schedulesByCode = (await _syncService.GetWorkSchedulesAsync())
            .GroupBy(x => x.Code)
            .ToDictionary(g => g.Key, g => g.First());

        var employeeCatalog = (await _syncService.GetEmployeeCatalogAsync())
            .OrderBy(x => x.UserId)
            .ToList();

        var catalogRows = employeeCatalog
            .Select(x => new ManualCatalogRow(
                x.UserId,
                string.IsNullOrWhiteSpace(x.SqlName) ? x.DeviceName : x.SqlName,
                x.Department ?? string.Empty,
                x.WorkScheduleCode.HasValue ? $"{x.WorkScheduleCode.Value}" : "-"))
            .ToList();

        if (catalogRows.All(x => x.UserId != suggestedUserId))
        {
            catalogRows.Insert(0, new ManualCatalogRow(
                suggestedUserId,
                string.IsNullOrWhiteSpace(suggestedName) ? $"Empleado_{suggestedUserId}" : suggestedName,
                string.Empty,
                "-"));
        }

        var workingArea = Screen.FromControl(this).WorkingArea;
        var dialogWidth = Math.Min(1240, workingArea.Width - 24);
        var dialogHeight = Math.Min(780, workingArea.Height - 24);

        using var prompt = new Form
        {
            Width = dialogWidth,
            Height = dialogHeight,
            FormBorderStyle = FormBorderStyle.Sizable,
            Text = "Alta manual de asistencia (visual)",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = true,
            BackColor = Color.FromArgb(245, 248, 252)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        prompt.Controls.Add(root);

        var lblHint = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(42, 52, 65),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Text = "1) Selecciona empleado del catálogo. 2) Elige fecha/hora. 3) Agrega marca en la columna que corresponda (Entrada, Salida comida, Entrada comida, Salida)."
        };
        root.Controls.Add(lblHint, 0, 0);

        var splitMain = new SplitContainer
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None
        };
        root.Controls.Add(splitMain, 0, 1);

        var grpCatalog = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Catálogo de empleados",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(42, 52, 65),
            Padding = new Padding(8)
        };
        splitMain.Panel1.Controls.Add(grpCatalog);

        var dgvCatalog = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        grpCatalog.Controls.Add(dgvCatalog);

        dgvCatalog.DataSource = catalogRows;
        if (dgvCatalog.Columns.Contains(nameof(ManualCatalogRow.UserId)))
        {
            dgvCatalog.Columns[nameof(ManualCatalogRow.UserId)].HeaderText = "USERID";
            dgvCatalog.Columns[nameof(ManualCatalogRow.UserId)].FillWeight = 20;
        }

        if (dgvCatalog.Columns.Contains(nameof(ManualCatalogRow.EmployeeName)))
        {
            dgvCatalog.Columns[nameof(ManualCatalogRow.EmployeeName)].HeaderText = "Empleado";
            dgvCatalog.Columns[nameof(ManualCatalogRow.EmployeeName)].FillWeight = 50;
        }

        if (dgvCatalog.Columns.Contains(nameof(ManualCatalogRow.Department)))
        {
            dgvCatalog.Columns[nameof(ManualCatalogRow.Department)].HeaderText = "Depto";
            dgvCatalog.Columns[nameof(ManualCatalogRow.Department)].FillWeight = 20;
        }

        if (dgvCatalog.Columns.Contains(nameof(ManualCatalogRow.ScheduleCode)))
        {
            dgvCatalog.Columns[nameof(ManualCatalogRow.ScheduleCode)].HeaderText = "Horario";
            dgvCatalog.Columns[nameof(ManualCatalogRow.ScheduleCode)].FillWeight = 10;
        }

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0)
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        splitMain.Panel2.Controls.Add(rightLayout);

        var cardInput = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10)
        };
        rightLayout.Controls.Add(cardInput, 0, 0);

        var inputGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 3
        };
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        inputGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        inputGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        inputGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        cardInput.Controls.Add(inputGrid);

        var numUser = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 99999,
            Value = Math.Clamp(suggestedUserId, 1, 99999)
        };
        var txtName = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = string.IsNullOrWhiteSpace(suggestedName) ? string.Empty : suggestedName.Trim()
        };
        var dtDay = new DateTimePicker
        {
            Dock = DockStyle.Fill,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today
        };
        var dtCheckTime = new DateTimePicker
        {
            Dock = DockStyle.Fill,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy HH:mm:ss",
            ShowUpDown = true,
            Value = DateTime.Now
        };
        var cmbQuickMode = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbQuickMode.Items.AddRange(new object[]
        {
            "Entrada (0)",
            "Salida comida (2)",
            "Entrada comida (3)",
            "Salida (1)",
            "Sin modo (null)"
        });
        cmbQuickMode.SelectedIndex = 0;

        var btnAddSelected = new Button { Text = "Agregar tipo", Width = 110, Height = 30 };
        var btnAddEntry = new Button { Text = "Entrada", Width = 95, Height = 30 };
        var btnAddBreakOut = new Button { Text = "Salida comida", Width = 120, Height = 30 };
        var btnAddBreakIn = new Button { Text = "Entrada comida", Width = 124, Height = 30 };
        var btnAddExit = new Button { Text = "Salida", Width = 90, Height = 30 };
        var btnRemovePending = new Button { Text = "Quitar seleccionada", Width = 135, Height = 30 };
        var btnClearPending = new Button { Text = "Limpiar lista", Width = 110, Height = 30 };
        ButtonStyles.ApplySecondary(btnAddSelected);
        ButtonStyles.ApplySecondary(btnAddEntry);
        ButtonStyles.ApplySecondary(btnAddBreakOut);
        ButtonStyles.ApplySecondary(btnAddBreakIn);
        ButtonStyles.ApplySecondary(btnAddExit);
        ButtonStyles.ApplySecondary(btnRemovePending);
        ButtonStyles.ApplySecondary(btnClearPending);

        var quickPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        quickPanel.Controls.Add(cmbQuickMode);
        quickPanel.Controls.Add(btnAddSelected);

        var modeButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        modeButtonsPanel.Controls.Add(btnAddEntry);
        modeButtonsPanel.Controls.Add(btnAddBreakOut);
        modeButtonsPanel.Controls.Add(btnAddBreakIn);
        modeButtonsPanel.Controls.Add(btnAddExit);
        modeButtonsPanel.Controls.Add(btnRemovePending);
        modeButtonsPanel.Controls.Add(btnClearPending);

        inputGrid.Controls.Add(new Label { Text = "USERID:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        inputGrid.Controls.Add(numUser, 1, 0);
        inputGrid.Controls.Add(new Label { Text = "Empleado:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        inputGrid.Controls.Add(txtName, 3, 0);
        inputGrid.Controls.Add(new Label { Text = "Día:", AutoSize = true, Anchor = AnchorStyles.Left }, 4, 0);
        inputGrid.Controls.Add(dtDay, 5, 0);

        inputGrid.Controls.Add(new Label { Text = "Fecha/hora:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        inputGrid.Controls.Add(dtCheckTime, 1, 1);
        inputGrid.Controls.Add(new Label { Text = "Tipo:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
        inputGrid.Controls.Add(quickPanel, 3, 1);
        inputGrid.SetColumnSpan(quickPanel, 3);

        inputGrid.Controls.Add(modeButtonsPanel, 0, 2);
        inputGrid.SetColumnSpan(modeButtonsPanel, 6);

        var cardProjection = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Vista del día (como reporte)",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(42, 52, 65),
            Padding = new Padding(8)
        };
        rightLayout.Controls.Add(cardProjection, 0, 1);

        var dgvProjection = new DataGridView
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
            BorderStyle = BorderStyle.None
        };
        cardProjection.Controls.Add(dgvProjection);

        var splitBottom = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
        };
        rightLayout.Controls.Add(splitBottom, 0, 2);

        void ApplySafeSplitterDistance(SplitContainer container, int desiredDistance, int minPanel1, int minPanel2)
        {
            var totalLength = container.Orientation == Orientation.Vertical
                ? container.ClientSize.Width
                : container.ClientSize.Height;

            if (totalLength <= 0)
            {
                return;
            }

            var splitter = Math.Max(1, container.SplitterWidth);
            var available = totalLength - splitter;
            if (available <= 40)
            {
                return;
            }

            var safeMin1 = Math.Clamp(minPanel1, 40, Math.Max(40, available - 40));
            var safeMin2 = Math.Clamp(minPanel2, 40, Math.Max(40, available - 40));

            if (safeMin1 + safeMin2 > available)
            {
                safeMin1 = Math.Max(40, available / 2);
                safeMin2 = Math.Max(40, available - safeMin1);
            }

            try
            {
                if (container.Panel1MinSize != safeMin1)
                {
                    container.Panel1MinSize = safeMin1;
                }

                if (container.Panel2MinSize != safeMin2)
                {
                    container.Panel2MinSize = safeMin2;
                }
            }
            catch
            {
                return;
            }

            var maxDistance = available - container.Panel2MinSize;
            if (maxDistance < container.Panel1MinSize)
            {
                return;
            }

            var safeDistance = Math.Clamp(desiredDistance, container.Panel1MinSize, maxDistance);

            try
            {
                if (safeDistance > 0 && safeDistance != container.SplitterDistance)
                {
                    container.SplitterDistance = safeDistance;
                }
            }
            catch
            {
                // Ignorar ajustes transitorios de layout.
            }
        }

        splitMain.Resize += (_, _) =>
            ApplySafeSplitterDistance(
                splitMain,
                (int)Math.Round(splitMain.ClientSize.Width * 0.33),
                minPanel1: 160,
                minPanel2: 260);

        splitBottom.Resize += (_, _) =>
            ApplySafeSplitterDistance(
                splitBottom,
                (int)Math.Round(splitBottom.ClientSize.Height * 0.52),
                minPanel1: 90,
                minPanel2: 90);

        var grpExisting = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Marcas registradas del día",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(42, 52, 65),
            Padding = new Padding(8)
        };
        splitBottom.Panel1.Controls.Add(grpExisting);

        var dgvExisting = new DataGridView
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
            BorderStyle = BorderStyle.None
        };
        grpExisting.Controls.Add(dgvExisting);

        var grpPending = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Marcas a insertar",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(42, 52, 65),
            Padding = new Padding(8)
        };
        splitBottom.Panel2.Controls.Add(grpPending);

        var dgvPending = new DataGridView
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
            BorderStyle = BorderStyle.None
        };
        grpPending.Controls.Add(dgvPending);

        var lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(84, 92, 105),
            Padding = new Padding(4, 6, 4, 0),
            Text = "Selecciona un empleado para comenzar."
        };
        rightLayout.Controls.Add(lblStatus, 0, 3);

        var footerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };
        var btnOk = new Button { Text = "Continuar", Width = 110, Height = 34, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancelar", Width = 95, Height = 34, DialogResult = DialogResult.Cancel };
        ButtonStyles.ApplyPrimary(btnOk);
        ButtonStyles.ApplySecondary(btnCancel);
        footerPanel.Controls.Add(btnCancel);
        footerPanel.Controls.Add(btnOk);
        root.Controls.Add(footerPanel, 0, 2);

        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;

        var existingPunches = new List<SqlPunchHistoryItem>();
        var pendingEntries = new List<ManualPunchEntry>();
        string scheduleDescription = "Sin horario asignado.";
        var selectingFromCatalog = false;

        int? GetQuickMode()
        {
            return cmbQuickMode.SelectedIndex switch
            {
                0 => 0,
                1 => 2,
                2 => 3,
                3 => 1,
                _ => (int?)null
            };
        }

        string FormatTime(DateTime? value)
            => value.HasValue ? value.Value.ToString("HH:mm") : "-";

        ManualDayProjectionRow BuildProjectionRow()
        {
            var allPunches = existingPunches
                .Select(x => (x.CheckTime, x.InOutMode))
                .Concat(pendingEntries.Select(x => (x.CheckTime, x.InOutMode)))
                .OrderBy(x => x.CheckTime)
                .ToList();

            var entrada = allPunches.FirstOrDefault(x => x.InOutMode == 0).CheckTime;
            var salidaComida = allPunches.FirstOrDefault(x => x.InOutMode == 2).CheckTime;
            var entradaComida = allPunches.FirstOrDefault(x => x.InOutMode == 3).CheckTime;
            var salida = allPunches.FirstOrDefault(x => x.InOutMode == 1).CheckTime;

            var sinModo = allPunches
                .Where(x => !x.InOutMode.HasValue)
                .Select(x => x.CheckTime)
                .ToList();

            if (entrada == default && sinModo.Count > 0)
            {
                entrada = sinModo.First();
            }

            if (salida == default && sinModo.Count > 1)
            {
                salida = sinModo.Last();
            }

            var hasEntrada = entrada != default;
            var hasSalida = salida != default;
            var hasBreakOut = salidaComida != default;
            var hasBreakIn = entradaComida != default;

            var status = allPunches.Count == 0
                ? "SIN_REGISTRO"
                : hasEntrada && hasSalida && (hasBreakOut == hasBreakIn)
                    ? "OK"
                    : hasEntrada || hasSalida || hasBreakOut || hasBreakIn
                        ? "INCOMPLETO"
                        : "REVISAR";

            return new ManualDayProjectionRow(
                dtDay.Value.Date,
                FormatTime(hasEntrada ? entrada : null),
                FormatTime(hasBreakOut ? salidaComida : null),
                FormatTime(hasBreakIn ? entradaComida : null),
                FormatTime(hasSalida ? salida : null),
                status);
        }

        void RefreshProjection()
        {
            dgvProjection.DataSource = new[] { BuildProjectionRow() };

            if (dgvProjection.Columns.Contains(nameof(ManualDayProjectionRow.Day)))
            {
                dgvProjection.Columns[nameof(ManualDayProjectionRow.Day)].HeaderText = "Fecha";
                dgvProjection.Columns[nameof(ManualDayProjectionRow.Day)].DefaultCellStyle.Format = "dd/MM/yyyy";
                dgvProjection.Columns[nameof(ManualDayProjectionRow.Day)].FillWeight = 20;
            }

            if (dgvProjection.Columns.Contains(nameof(ManualDayProjectionRow.Entry)))
            {
                dgvProjection.Columns[nameof(ManualDayProjectionRow.Entry)].HeaderText = "Entrada";
            }

            if (dgvProjection.Columns.Contains(nameof(ManualDayProjectionRow.BreakOut)))
            {
                dgvProjection.Columns[nameof(ManualDayProjectionRow.BreakOut)].HeaderText = "Salida comida";
            }

            if (dgvProjection.Columns.Contains(nameof(ManualDayProjectionRow.BreakIn)))
            {
                dgvProjection.Columns[nameof(ManualDayProjectionRow.BreakIn)].HeaderText = "Entrada comida";
            }

            if (dgvProjection.Columns.Contains(nameof(ManualDayProjectionRow.Exit)))
            {
                dgvProjection.Columns[nameof(ManualDayProjectionRow.Exit)].HeaderText = "Salida";
            }

            if (dgvProjection.Columns.Contains(nameof(ManualDayProjectionRow.Status)))
            {
                dgvProjection.Columns[nameof(ManualDayProjectionRow.Status)].HeaderText = "Estado";
                dgvProjection.Columns[nameof(ManualDayProjectionRow.Status)].FillWeight = 16;
            }

            lblStatus.Text =
                $"{scheduleDescription}  ·  Registros actuales: {existingPunches.Count}  ·  Pendientes por insertar: {pendingEntries.Count}";
        }

        void RefreshPendingGrid()
        {
            pendingEntries.Sort((a, b) => a.CheckTime.CompareTo(b.CheckTime));
            var rows = pendingEntries
                .Select((x, idx) => new ManualPendingPunchRow(
                    idx,
                    idx + 1,
                    x.CheckTime,
                    FormatInOutMode(x.InOutMode)))
                .ToList();

            dgvPending.DataSource = rows;
            if (dgvPending.Columns.Contains(nameof(ManualPendingPunchRow.Index)))
            {
                dgvPending.Columns[nameof(ManualPendingPunchRow.Index)].Visible = false;
            }

            if (dgvPending.Columns.Contains(nameof(ManualPendingPunchRow.Order)))
            {
                dgvPending.Columns[nameof(ManualPendingPunchRow.Order)].HeaderText = "#";
                dgvPending.Columns[nameof(ManualPendingPunchRow.Order)].FillWeight = 10;
            }

            if (dgvPending.Columns.Contains(nameof(ManualPendingPunchRow.CheckTime)))
            {
                dgvPending.Columns[nameof(ManualPendingPunchRow.CheckTime)].HeaderText = "Fecha/hora";
                dgvPending.Columns[nameof(ManualPendingPunchRow.CheckTime)].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm:ss";
                dgvPending.Columns[nameof(ManualPendingPunchRow.CheckTime)].FillWeight = 45;
            }

            if (dgvPending.Columns.Contains(nameof(ManualPendingPunchRow.ModeLabel)))
            {
                dgvPending.Columns[nameof(ManualPendingPunchRow.ModeLabel)].HeaderText = "Tipo";
                dgvPending.Columns[nameof(ManualPendingPunchRow.ModeLabel)].FillWeight = 45;
            }

            RefreshProjection();
        }

        async Task LoadDayDataAsync()
        {
            var userId = (int)numUser.Value;
            var day = dtDay.Value.Date;
            var from = day;
            var to = day.AddDays(1).AddTicks(-1);

            var history = await _syncService.GetHistoryAsync(userId, from, to, maxRows: 500);
            existingPunches = history
                .OrderBy(x => x.CheckTime)
                .ToList();

            dgvExisting.DataSource = existingPunches
                .Select(x => new ManualRawPunchRow(x.CheckTime, FormatInOutMode(x.InOutMode), x.Source))
                .ToList();

            if (dgvExisting.Columns.Contains(nameof(ManualRawPunchRow.CheckTime)))
            {
                dgvExisting.Columns[nameof(ManualRawPunchRow.CheckTime)].HeaderText = "Fecha/hora";
                dgvExisting.Columns[nameof(ManualRawPunchRow.CheckTime)].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm:ss";
                dgvExisting.Columns[nameof(ManualRawPunchRow.CheckTime)].FillWeight = 48;
            }

            if (dgvExisting.Columns.Contains(nameof(ManualRawPunchRow.ModeLabel)))
            {
                dgvExisting.Columns[nameof(ManualRawPunchRow.ModeLabel)].HeaderText = "Tipo";
                dgvExisting.Columns[nameof(ManualRawPunchRow.ModeLabel)].FillWeight = 28;
            }

            if (dgvExisting.Columns.Contains(nameof(ManualRawPunchRow.Source)))
            {
                dgvExisting.Columns[nameof(ManualRawPunchRow.Source)].HeaderText = "Origen";
                dgvExisting.Columns[nameof(ManualRawPunchRow.Source)].FillWeight = 24;
            }

            var snapshot = await _syncService.GetEmployeeAdminSnapshotAsync(userId);
            if (snapshot?.WorkScheduleCode is int code && schedulesByCode.TryGetValue(code, out var schedule))
            {
                scheduleDescription = $"Horario {BuildScheduleDescription(schedule)}";
            }
            else
            {
                scheduleDescription = "Horario: sin horario asignado.";
            }

            RefreshProjection();
        }

        void AddPending(int? mode)
        {
            pendingEntries.Add(new ManualPunchEntry(dtCheckTime.Value, mode));
            RefreshPendingGrid();
        }

        btnAddSelected.Click += (_, _) => AddPending(GetQuickMode());
        btnAddEntry.Click += (_, _) => AddPending(0);
        btnAddBreakOut.Click += (_, _) => AddPending(2);
        btnAddBreakIn.Click += (_, _) => AddPending(3);
        btnAddExit.Click += (_, _) => AddPending(1);

        btnRemovePending.Click += (_, _) =>
        {
            if (dgvPending.SelectedRows.Count == 0)
            {
                return;
            }

            var row = dgvPending.SelectedRows[0].DataBoundItem as ManualPendingPunchRow;
            if (row is null)
            {
                return;
            }

            if (row.Index >= 0 && row.Index < pendingEntries.Count)
            {
                pendingEntries.RemoveAt(row.Index);
                RefreshPendingGrid();
            }
        };

        btnClearPending.Click += (_, _) =>
        {
            pendingEntries.Clear();
            RefreshPendingGrid();
        };

        async Task TryReloadAsync()
        {
            try
            {
                await LoadDayDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar el día seleccionado.\n\n{ex.Message}", "Alta manual", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        dgvCatalog.SelectionChanged += async (_, _) =>
        {
            if (dgvCatalog.SelectedRows.Count == 0)
            {
                return;
            }

            if (dgvCatalog.SelectedRows[0].DataBoundItem is not ManualCatalogRow selected)
            {
                return;
            }

            selectingFromCatalog = true;
            try
            {
                numUser.Value = Math.Clamp(selected.UserId, 1, 99999);
                txtName.Text = selected.EmployeeName;
            }
            finally
            {
                selectingFromCatalog = false;
            }

            pendingEntries.Clear();
            RefreshPendingGrid();
            await TryReloadAsync();
        };

        numUser.ValueChanged += async (_, _) =>
        {
            if (selectingFromCatalog)
            {
                return;
            }

            pendingEntries.Clear();
            RefreshPendingGrid();
            await TryReloadAsync();
        };

        dtDay.ValueChanged += async (_, _) =>
        {
            pendingEntries.Clear();
            RefreshPendingGrid();
            await TryReloadAsync();
        };

        prompt.Shown += async (_, _) =>
        {
            try
            {
                ApplySafeSplitterDistance(
                    splitMain,
                    (int)Math.Round(splitMain.ClientSize.Width * 0.33),
                    minPanel1: 160,
                    minPanel2: 260);

                ApplySafeSplitterDistance(
                    splitBottom,
                    (int)Math.Round(splitBottom.ClientSize.Height * 0.52),
                    minPanel1: 90,
                    minPanel2: 90);

                var targetIndex = catalogRows.FindIndex(x => x.UserId == suggestedUserId);
                if (targetIndex >= 0 && targetIndex < dgvCatalog.Rows.Count)
                {
                    dgvCatalog.ClearSelection();
                    dgvCatalog.Rows[targetIndex].Selected = true;
                    dgvCatalog.CurrentCell = dgvCatalog.Rows[targetIndex].Cells[0];
                }

                await TryReloadAsync();
            }
            catch
            {
                // Sin bloqueo de apertura por error de precarga.
            }
        };

        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            return null;
        }

        var finalUserId = (int)numUser.Value;
        var finalName = string.IsNullOrWhiteSpace(txtName.Text)
            ? $"Empleado_{finalUserId}"
            : txtName.Text.Trim();

        if (pendingEntries.Count > 0)
        {
            return new ManualPunchRequest(
                finalUserId,
                finalName,
                pendingEntries[0].CheckTime,
                pendingEntries[0].InOutMode,
                pendingEntries.ToList(),
                $"Alta visual manual ({pendingEntries.Count} marca(s)). {scheduleDescription}");
        }

        var quickMode = GetQuickMode();
        return new ManualPunchRequest(
            finalUserId,
            finalName,
            dtCheckTime.Value,
            quickMode,
            null,
            $"Alta visual manual puntual. {scheduleDescription}");
    }

    private static string FormatInOutMode(int? mode)
    {
        return mode switch
        {
            0 => "Entrada (0)",
            2 => "Salida comida (2)",
            3 => "Entrada comida (3)",
            1 => "Salida (1)",
            _ => "Sin modo (null)"
        };
    }

    private async Task<ManualMissingAnalysis> BuildManualMissingAnalysisAsync(
        int userId,
        string employeeName,
        DateTime day,
        IReadOnlyDictionary<int, WorkScheduleItem> schedulesByCode)
    {
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1).AddTicks(-1);

        var history = await _syncService.GetHistoryAsync(userId, dayStart, dayEnd, maxRows: 300);
        var existingPunches = history
            .OrderBy(x => x.CheckTime)
            .Select(x => new ManualExistingPunch(x.CheckTime, x.Source))
            .ToList();

        var snapshot = await _syncService.GetEmployeeAdminSnapshotAsync(userId);
        WorkScheduleItem? schedule = null;
        if (snapshot?.WorkScheduleCode is int code && schedulesByCode.TryGetValue(code, out var loadedSchedule))
        {
            schedule = loadedSchedule;
        }

        var scheduleDescription = schedule is null
            ? "Sin horario asignado. Asigna horario en Renombrar empleado para simular faltantes."
            : BuildScheduleDescription(schedule);
        var displayName = string.IsNullOrWhiteSpace(employeeName)
            ? $"USERID {userId}"
            : employeeName;

        var suggestedPunches = new List<ManualSuggestedPunch>();
        if (schedule is not null)
        {
            var expectedEntry = day.Date.Add(schedule.StartTime);
            var expectedExit = day.Date.Add(schedule.EndTime);
            if (schedule.EndTime <= schedule.StartTime)
            {
                expectedExit = expectedExit.AddDays(1);
            }

            var hasEntryNear = HasPunchNear(existingPunches, expectedEntry, toleranceMinutes: 180);
            var hasExitNear = HasPunchNear(existingPunches, expectedExit, toleranceMinutes: 180);

            if (existingPunches.Count == 0)
            {
                suggestedPunches.Add(new ManualSuggestedPunch(expectedEntry, 0, "Entrada faltante según horario"));
                suggestedPunches.Add(new ManualSuggestedPunch(expectedExit, 1, "Salida faltante según horario"));
            }
            else
            {
                if (!hasEntryNear)
                {
                    suggestedPunches.Add(new ManualSuggestedPunch(expectedEntry, 0, "No hay entrada cercana al horario"));
                }

                if (!hasExitNear)
                {
                    suggestedPunches.Add(new ManualSuggestedPunch(expectedExit, 1, "No hay salida cercana al horario"));
                }
            }
        }

        suggestedPunches = suggestedPunches
            .Where(x => !HasPunchNear(existingPunches, x.CheckTime, toleranceMinutes: 1))
            .GroupBy(x => new { x.CheckTime, x.InOutMode })
            .Select(g => g.First())
            .OrderBy(x => x.CheckTime)
            .ToList();

        string? proportionWarning = null;
        if (schedule is not null)
        {
            var projectedTimes = existingPunches
                .Select(x => x.CheckTime)
                .Concat(suggestedPunches.Select(x => x.CheckTime))
                .OrderBy(x => x)
                .ToList();

            if (projectedTimes.Count >= 2)
            {
                var projectedHours = (projectedTimes[^1] - projectedTimes[0]).TotalHours;
                var expectedHours = (double)(schedule.StandardHours <= 0 ? 9.60m : schedule.StandardHours);
                if (projectedHours < expectedHours * 0.55 || projectedHours > expectedHours * 1.45)
                {
                    proportionWarning =
                        $"Advertencia: jornada proyectada {projectedHours:0.##} h vs estándar {expectedHours:0.##} h. Si se ve desproporcionado, no apliques la simulación.";
                }
            }
        }

        var summary = schedule is null
            ? $"{displayName}: no se puede simular por horario porque no tiene horario asignado."
            : suggestedPunches.Count == 0
                ? $"{displayName}: no se detectaron faltantes claros para ese día según el horario."
                : $"{displayName}: se sugieren {suggestedPunches.Count} registro(s) faltante(s) para completar el día conforme al horario.";

        return new ManualMissingAnalysis(scheduleDescription, existingPunches, suggestedPunches, summary, proportionWarning);
    }

    private static bool HasPunchNear(IReadOnlyList<ManualExistingPunch> punches, DateTime expected, int toleranceMinutes)
    {
        return punches.Any(x => Math.Abs((x.CheckTime - expected).TotalMinutes) <= toleranceMinutes);
    }

    private static string BuildScheduleDescription(WorkScheduleItem schedule)
    {
        var start = DateTime.Today.Add(schedule.StartTime).ToString("HH:mm");
        var end = DateTime.Today.Add(schedule.EndTime).ToString("HH:mm");
        var overnight = schedule.EndTime <= schedule.StartTime ? " (turno nocturno)" : string.Empty;
        return $"{schedule.Code} - {schedule.Name}: {start} a {end}{overnight}. Jornada estándar: {schedule.StandardHours:0.##} h.";
    }

    private static NightShiftCleanupRequest? PromptNightShiftCleanupData(int? suggestedUserId)
    {
        using var prompt = new Form
        {
            Width = 620,
            Height = 330,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Limpieza correctiva nocturna (Pervasive)",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            BackColor = Color.FromArgb(245, 248, 252)
        };

        var lblHint = new Label
        {
            Left = 20,
            Top = 16,
            Width = 560,
            Height = 54,
            ForeColor = Color.FromArgb(42, 52, 65),
            Text = "Recalcula renglones de horario 3 por rango y elimina renglones huérfanos\n" +
                   "que quedaron partidos entre entrada/salida en días distintos."
        };

        var lblFrom = new Label { Left = 20, Top = 88, Width = 120, Text = "Fecha inicio:" };
        var dtFrom = new DateTimePicker
        {
            Left = 150,
            Top = 84,
            Width = 150,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today.AddDays(-60)
        };

        var lblTo = new Label { Left = 320, Top = 88, Width = 100, Text = "Fecha fin:" };
        var dtTo = new DateTimePicker
        {
            Left = 400,
            Top = 84,
            Width = 150,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today
        };

        var chkOnlyUser = new CheckBox
        {
            Left = 20,
            Top = 132,
            Width = 260,
            Text = "Limpiar solo un USERID específico"
        };

        var numUser = new NumericUpDown
        {
            Left = 290,
            Top = 128,
            Width = 120,
            Minimum = 1,
            Maximum = 99999,
            Enabled = false,
            Value = Math.Clamp(suggestedUserId ?? 1, 1, 99999)
        };

        var lblWarn = new Label
        {
            Left = 20,
            Top = 172,
            Width = 560,
            Height = 54,
            ForeColor = Color.FromArgb(134, 74, 20),
            Text = "Recomendado: ejecutar primero un rango corto (15-30 días), revisar resultado,\n" +
                   "y después ampliar si es necesario."
        };

        chkOnlyUser.CheckedChanged += (_, _) => numUser.Enabled = chkOnlyUser.Checked;

        var btnOk = new Button { Text = "Ejecutar", Left = 382, Top = 238, Width = 100, Height = 34, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancelar", Left = 490, Top = 238, Width = 90, Height = 34, DialogResult = DialogResult.Cancel };

        btnOk.FlatStyle = FlatStyle.Flat;
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.BackColor = Color.FromArgb(0, 120, 215);
        btnOk.ForeColor = Color.White;

        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 230);
        btnCancel.BackColor = Color.White;

        prompt.Controls.Add(lblHint);
        prompt.Controls.Add(lblFrom);
        prompt.Controls.Add(dtFrom);
        prompt.Controls.Add(lblTo);
        prompt.Controls.Add(dtTo);
        prompt.Controls.Add(chkOnlyUser);
        prompt.Controls.Add(numUser);
        prompt.Controls.Add(lblWarn);
        prompt.Controls.Add(btnOk);
        prompt.Controls.Add(btnCancel);
        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;

        if (prompt.ShowDialog() != DialogResult.OK)
        {
            return null;
        }

        return new NightShiftCleanupRequest(
            dtFrom.Value.Date,
            dtTo.Value.Date,
            chkOnlyUser.Checked ? Convert.ToInt32(numUser.Value) : null);
    }

    private static EmployeeReassignRequest? PromptEmployeeReassignData(int suggestedOldUserId, string initialName, string? initialDept, string? initialShift)
    {
        using var prompt = new Form
        {
            Width = 760,
            Height = 430,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Cambio de número/DEPT/SHIFT por rango",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            BackColor = Color.FromArgb(245, 248, 252)
        };

        var lblHint = new Label
        {
            Left = 22,
            Top = 16,
            Width = 700,
            Height = 36,
            ForeColor = Color.FromArgb(42, 52, 65),
            Text = "Este cambio afecta SOLO el rango de fechas indicado y NO modifica horas de entrada/salida."
        };

        var lblOld = new Label { Left = 22, Top = 64, Width = 200, Text = "USERID origen (editable):" };
        var numOld = new NumericUpDown { Left = 220, Top = 60, Width = 140, Minimum = 1, Maximum = 99999, Value = Math.Clamp(suggestedOldUserId, 1, 99999) };

        var lblNew = new Label { Left = 22, Top = 102, Width = 200, Text = "USERID destino:" };
        var numNew = new NumericUpDown { Left = 220, Top = 98, Width = 140, Minimum = 1, Maximum = 99999, Value = Math.Clamp(suggestedOldUserId, 1, 99999) };

        var lblFrom = new Label { Left = 22, Top = 140, Width = 200, Text = "Fecha inicio:" };
        var dtFrom = new DateTimePicker { Left = 220, Top = 136, Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-2) };

        var lblTo = new Label { Left = 400, Top = 140, Width = 120, Text = "Fecha fin:" };
        var dtTo = new DateTimePicker { Left = 520, Top = 136, Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today };

        var lblName = new Label { Left = 22, Top = 182, Width = 200, Text = "Nombre (opcional):" };
        var txtName = new TextBox { Left = 220, Top = 178, Width = 460, Text = initialName ?? string.Empty };

        var lblDept = new Label { Left = 22, Top = 222, Width = 200, Text = "DEPT (Pervasive):" };
        var cmbDept = CreatePervasiveLabelComboBox(220, 218, initialDept ?? string.Empty, PervasiveCodeOptions, 280);

        var lblShift = new Label { Left = 22, Top = 262, Width = 200, Text = "SHIFT (Pervasive):" };
        var cmbShift = CreatePervasiveLabelComboBox(220, 258, initialShift ?? string.Empty, PervasiveShiftOptions, 280);

        var btnOk = new Button { Text = "Validar cambios", Left = 484, Top = 324, Width = 120, Height = 36, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancelar", Left = 614, Top = 324, Width = 92, Height = 36, DialogResult = DialogResult.Cancel };

        btnOk.FlatStyle = FlatStyle.Flat;
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.BackColor = Color.FromArgb(0, 120, 215);
        btnOk.ForeColor = Color.White;

        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 230);
        btnCancel.BackColor = Color.White;

        prompt.Controls.Add(lblHint);
        prompt.Controls.Add(lblOld);
        prompt.Controls.Add(numOld);
        prompt.Controls.Add(lblNew);
        prompt.Controls.Add(numNew);
        prompt.Controls.Add(lblFrom);
        prompt.Controls.Add(dtFrom);
        prompt.Controls.Add(lblTo);
        prompt.Controls.Add(dtTo);
        prompt.Controls.Add(lblName);
        prompt.Controls.Add(txtName);
        prompt.Controls.Add(lblDept);
        prompt.Controls.Add(cmbDept);
        prompt.Controls.Add(lblShift);
        prompt.Controls.Add(cmbShift);
        prompt.Controls.Add(btnOk);
        prompt.Controls.Add(btnCancel);
        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;

        if (prompt.ShowDialog() != DialogResult.OK)
        {
            return null;
        }

        if (dtFrom.Value.Date > dtTo.Value.Date)
        {
            MessageBox.Show("La fecha inicio no puede ser mayor a la fecha fin.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        return new EmployeeReassignRequest(
            (int)numOld.Value,
            (int)numNew.Value,
            dtFrom.Value.Date,
            dtTo.Value.Date,
            txtName.Text.Trim(),
            GetSelectedPervasiveCode(cmbDept),
            GetSelectedPervasiveCode(cmbShift));
    }

    private static string BuildEmployeeReassignValidationMessage(EmployeeReassignPreviewResult preview)
    {
        var sqlDays = FormatDatesForPreview(preview.SqlAffectedDates);
        var pervasiveDays = preview.PervasiveEnabled
            ? FormatDatesForPreview(preview.PervasiveAffectedDates)
            : "(Pervasive deshabilitado)";

        return
            "Validación previa del cambio por rango\n\n" +
            $"Rango: {preview.FromDate:dd/MM/yyyy} a {preview.ToDate:dd/MM/yyyy}\n" +
            $"USERID: {preview.OldUserId} -> {preview.NewUserId}\n\n" +
            "SQL Server:\n" +
            $"- Filas origen: {preview.SqlOldRows}\n" +
            $"- Filas destino existentes: {preview.SqlTargetRows}\n" +
            $"- Conflictos (UserId,CheckTime): {preview.SqlConflictRows}\n" +
            $"- Días a cambiar ({preview.SqlDistinctDays}): {sqlDays}\n\n" +
            "Pervasive:\n" +
            $"- Habilitado: {(preview.PervasiveEnabled ? "Sí" : "No")}\n" +
            $"- Filas origen: {preview.PervasiveOldRows}\n" +
            $"- Filas destino existentes: {preview.PervasiveTargetRows}\n" +
            $"- Conflictos por CHARGE_DATE: {preview.PervasiveConflictRows}\n" +
            $"- Días a cambiar ({preview.PervasiveDistinctDays}): {pervasiveDays}\n\n" +
            "Nota: se omiten conflictos automáticamente para no romper índice único.";
    }

    private static string FormatDatesForPreview(IReadOnlyList<DateTime> dates)
    {
        if (dates.Count == 0)
        {
            return "(sin días detectados)";
        }

        var ordered = dates.Select(x => x.Date).Distinct().OrderBy(x => x).ToList();
        var top = ordered.Take(12).Select(d => d.ToString("dd/MM/yyyy"));
        var text = string.Join(", ", top);
        if (ordered.Count > 12)
        {
            text += $" ... (+{ordered.Count - 12} días)";
        }

        return text;
    }

    private sealed record ManualPunchRequest(
        int UserId,
        string EmployeeName,
        DateTime CheckTime,
        int? InOutMode,
        IReadOnlyList<ManualPunchEntry>? BatchEntries,
        string? Notes);

    private sealed record ManualExistingPunch(DateTime CheckTime, string Source);

    private sealed record ManualSuggestedPunch(DateTime CheckTime, int? InOutMode, string Reason);

    private sealed record ManualMissingAnalysis(
        string ScheduleDescription,
        IReadOnlyList<ManualExistingPunch> ExistingPunches,
        IReadOnlyList<ManualSuggestedPunch> SuggestedPunches,
        string Summary,
        string? ProportionWarning);

    private sealed record ManualCatalogRow(
        int UserId,
        string EmployeeName,
        string Department,
        string ScheduleCode);

    private sealed record ManualDayProjectionRow(
        DateTime Day,
        string Entry,
        string BreakOut,
        string BreakIn,
        string Exit,
        string Status);

    private sealed record ManualRawPunchRow(
        DateTime CheckTime,
        string ModeLabel,
        string Source);

    private sealed record ManualPendingPunchRow(
        int Index,
        int Order,
        DateTime CheckTime,
        string ModeLabel);

    private sealed record EmployeeAdminEditData(string Name, string? Department, string? Position, string? PervasiveDept, string? PervasiveShift, int? WorkScheduleCode, bool OvertimeAuthorized);

    private sealed record NightShiftCleanupRequest(DateTime FromDate, DateTime ToDate, int? UserId);

    private sealed record EmployeeReassignRequest(int OldUserId, int NewUserId, DateTime FromDate, DateTime ToDate, string? NewName, string? NewPervasiveDept, string? NewPervasiveShift);

    private sealed class WorkScheduleEditRow
    {
        public int Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string StartHour12 { get; set; } = "08:00";
        public string StartAmPm { get; set; } = "AM";
        public string EndHour12 { get; set; } = "05:36";
        public string EndAmPm { get; set; } = "PM";
        public decimal StandardHours { get; set; } = 9.60m;
        public bool IsActive { get; set; } = true;
    }

    private sealed record PervasiveCodeOption(string Code, string Label)
    {
        public string DisplayText => $"{Code} - {Label}";
    }

}


