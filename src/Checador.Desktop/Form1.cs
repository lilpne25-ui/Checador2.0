using System.Diagnostics;
using System.Runtime.InteropServices;
using Checador.Application;
using Checador.Domain.Interfaces;
using Checador.Domain.Models;
using Checador.Infrastructure.Access;

namespace Checador.Desktop;

public partial class Form1 : Form
{
    private readonly AttendanceReportOrchestrator _orchestrator;
    private readonly IAttendanceDataSource _attendanceDataSource;
    private readonly Checador.Domain.Interfaces.IAttendanceDiagnostics _diagnostics;
    private readonly AppConfiguration _configuration;
    private readonly SqlDeviceSyncService _sqlSyncService;
    private string? _lastReportPath;
    private bool _connectionProbeRunning;
    private string? _lastConnectionUiSnapshot;
    private readonly Dictionary<Button, Size> _buttonBaseSizes = new();

    public Form1(
        AttendanceReportOrchestrator orchestrator,
        IAttendanceDataSource attendanceDataSource,
        Checador.Domain.Interfaces.IAttendanceDiagnostics diagnostics,
        AppConfiguration configuration,
        SqlDeviceSyncService sqlSyncService)
    {
        _orchestrator = orchestrator;
        _attendanceDataSource = attendanceDataSource;
        _diagnostics = diagnostics;
        _configuration = configuration;
        _sqlSyncService = sqlSyncService;
        InitializeComponent();
        CacheBaseSizes();
        InitializeDefaults();
        Shown += Form1_Shown;
        Resize += (_, _) => ApplyResponsiveScale();
    }

    private void InitializeDefaults()
    {
        txtOutputDirectory.Text = _configuration.Report.OutputDirectory;
        txtTemplatePath.Text = _configuration.Report.TemplatePath ?? string.Empty;
        dtpStart.Value = DateTime.Today.AddDays(-7);
        dtpEnd.Value = DateTime.Today;
        cmbTheme.SelectedIndex = 0;
        cmbEmployee.Items.Clear();
        cmbEmployee.Text = string.Empty;
        txtStatus.Clear();
        SetConnectionIndicator(false, "Verificando...", "Aún sin prueba de conexión.", false);
        ApplyTheme(false);
        ButtonStyles.ApplyPrimary(btnGenerate);
        ButtonStyles.ApplySecondary(btnPreview);
        ButtonStyles.ApplySecondary(btnOpenReport);
        ButtonStyles.ApplySecondary(btnTestConnection);
        ButtonStyles.ApplySecondary(btnAdminSql);
        ApplyResponsiveScale();
        connectionTimer.Start();
        UpdateStatus("Listo para generar reporte.");
    }

    private void CacheBaseSizes()
    {
        var buttons = new[]
        {
            btnGenerate, btnPreview, btnOpenReport, btnTestConnection, btnAdminSql, btnBrowseOutput, btnBrowseTemplate
        };

        foreach (var b in buttons)
        {
            _buttonBaseSizes[b] = b.Size;
        }
    }

    private void btnAdminSql_Click(object sender, EventArgs e)
    {
        using var admin = new SyncAdminForm(_sqlSyncService, _configuration.Sql.AutoSyncMinutes);
        admin.ShowDialog(this);
    }

    private async void Form1_Shown(object? sender, EventArgs e)
    {
        await RefreshConnectionStatusAsync(showMessageBox: false);
        await LoadEmployeesAsync();
    }

    private async void btnGenerate_Click(object sender, EventArgs e)
    {
        var selectedFormat = PromptReportOutputFormat(this);
        if (!selectedFormat.HasValue)
        {
            UpdateStatus("Generacion cancelada por el usuario.");
            return;
        }

        SetBusy(true);

        try
        {
            var request = BuildRequestFromUi();

            if (request.FechaFin < request.FechaInicio)
            {
                MessageBox.Show("La fecha fin no puede ser menor a la fecha inicio.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.OutputDirectory))
            {
                MessageBox.Show("Debes indicar una carpeta de salida.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!chkAllEmployees.Checked && request.UserId <= 0)
            {
                MessageBox.Show("Selecciona un empleado de la lista para generar el reporte.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (chkAllEmployees.Checked)
            {
                UpdateStatus($"Procesando marcas y generando reportes ({GetReportFormatLabel(selectedFormat.Value)}) de todos los empleados...");
                var summary = await GenerateForAllEmployeesWithRhSummaryAsync(request, selectedFormat.Value);
                _lastReportPath = summary.ReportPaths.LastOrDefault();

                UpdateStatus($"Reportes generados ({GetReportFormatLabel(selectedFormat.Value)}): {summary.ReportPaths.Count}");
                btnOpenReport.Enabled = !string.IsNullOrWhiteSpace(_lastReportPath);
                ShowKpiSummary(summary);
            }
            else
            {
                request = await EnrichRequestWithEmployeeRhDataAsync(request);
                UpdateStatus($"Procesando marcas y generando reporte {GetReportFormatLabel(selectedFormat.Value)}...");
                var summary = await _orchestrator.GenerateWithSummaryAsync(request, outputFormat: selectedFormat.Value);
                var reportPath = summary.ReportPaths.FirstOrDefault();
                _lastReportPath = reportPath;

                UpdateStatus($"Reporte {GetReportFormatLabel(selectedFormat.Value)} generado: {reportPath}");
                btnOpenReport.Enabled = true;
                ShowKpiSummary(summary);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No hay registros procesados"))
        {
            UpdateStatus("Sin marcas para el empleado y rango seleccionado.");
            MessageBox.Show("No se encontraron marcas en ese periodo.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (IOException ex)
        {
            UpdateStatus($"No se pudo guardar el reporte {GetReportFormatLabel(selectedFormat.Value)}.");
            MessageBox.Show(
                $"No se pudo generar el reporte {GetReportFormatLabel(selectedFormat.Value)}.\n\n{ex.Message}",
                "Archivo en uso",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            UpdateStatus("Ocurrió un error durante la generación.");
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void btnPreview_Click(object sender, EventArgs e)
    {
        if (chkAllEmployees.Checked)
        {
            SetBusy(true);
            try
            {
                var request = BuildRequestFromUi();

                if (request.FechaFin < request.FechaInicio)
                {
                    MessageBox.Show("La fecha fin no puede ser menor a la fecha inicio.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                UpdateStatus("Procesando vista previa de todos los empleados...");
                var allPreviews = await _orchestrator.BuildPreviewForAllAsync(request);
                if (allPreviews.Count == 0)
                {
                    MessageBox.Show("No hay empleados para mostrar en vista previa.", "Vista previa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                UpdateStatus($"Vista previa masiva lista: {allPreviews.Count} empleados analizados.");

                using var allPreviewForm = new AllEmployeesPreviewForm(allPreviews, _sqlSyncService);
                allPreviewForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                UpdateStatus("Ocurrió un error durante la vista previa masiva.");
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }

            return;
        }

        SetBusy(true);
        try
        {
            var request = BuildRequestFromUi();

            if (request.UserId <= 0)
            {
                MessageBox.Show("Selecciona un empleado para ver la vista previa.", "Vista previa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (request.FechaFin < request.FechaInicio)
            {
                MessageBox.Show("La fecha fin no puede ser menor a la fecha inicio.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            request = await EnrichRequestWithEmployeeRhDataAsync(request);
            var preview = await _orchestrator.BuildPreviewAsync(request);

            if (preview.Days.Count == 0)
            {
                MessageBox.Show("No hay datos para mostrar en vista previa.", "Vista previa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var previewForm = new ReportPreviewForm(preview);
            previewForm.DatosHorasPorDia = preview.Days
                .Select(d => (d.Date.ToString("dd/MM"), Convert.ToDouble(d.HorasTrabajadas ?? 0m)))
                .ToList();
            previewForm.ShowDialog(this);
            UpdateStatus($"Vista previa consultada para empleado {preview.UserId} sin generar Excel.");
        }
        catch (Exception ex)
        {
            UpdateStatus("Ocurrió un error durante la vista previa.");
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void chkAllEmployees_CheckedChanged(object sender, EventArgs e)
    {
        cmbEmployee.Enabled = !chkAllEmployees.Checked;
    }

    private async Task LoadEmployeesAsync()
    {
        try
        {
            var employees = await _attendanceDataSource.GetEmployeesAsync();
            var items = employees
                .OrderBy(e => e.UserId)
                .Select(e => new EmployeeComboItem(e.UserId, e.EmployeeName))
                .ToList();

            cmbEmployee.BeginUpdate();
            cmbEmployee.DataSource = null;
            cmbEmployee.DisplayMember = nameof(EmployeeComboItem.DisplayText);
            cmbEmployee.ValueMember = nameof(EmployeeComboItem.UserId);
            cmbEmployee.DataSource = items;
            cmbEmployee.EndUpdate();

            if (items.Count > 0)
            {
                cmbEmployee.SelectedIndex = 0;
                UpdateStatus($"Empleados cargados: {items.Count}");
            }
            else
            {
                UpdateStatus("No se encontraron empleados en la fuente de datos actual.");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"No se pudo cargar la lista de empleados. Detalle: {ex.Message}");
        }
    }

    private int GetSelectedUserId()
    {
        if (cmbEmployee.SelectedItem is EmployeeComboItem item)
        {
            return item.UserId;
        }

        if (cmbEmployee.SelectedValue is int id)
        {
            return id;
        }

        return 0;
    }

    private ReportRequest BuildRequestFromUi()
    {
        return new ReportRequest
        {
            UserId = GetSelectedUserId(),
            FechaInicio = dtpStart.Value.Date,
            FechaFin = dtpEnd.Value.Date.AddDays(1).AddTicks(-1),
            OutputDirectory = txtOutputDirectory.Text.Trim(),
            TemplatePath = ResolveTemplatePath(txtTemplatePath.Text),
            Company = _configuration.Report.CompanyName
        };
    }

    private static ReportOutputFormat? PromptReportOutputFormat(IWin32Window owner)
        => ReportFormatPickerDialog.ShowPicker(owner);

    private static string GetReportFormatLabel(ReportOutputFormat format)
        => format == ReportOutputFormat.Pdf ? "PDF" : "Excel";

    private static string? ResolveTemplatePath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var trimmed = rawPath.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return trimmed;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, trimmed));
    }

    private async Task<ReportRunSummary> GenerateForAllEmployeesWithRhSummaryAsync(
        ReportRequest baseRequest,
        ReportOutputFormat outputFormat)
    {
        var employees = await _attendanceDataSource.GetEmployeesAsync();
        if (employees.Count == 0)
        {
            throw new InvalidOperationException("No se encontraron empleados en USERINFO.");
        }

        var catalogByUser = new Dictionary<int, SqlEmployeeCatalogItem>();
        var schedulesByCode = new Dictionary<int, WorkScheduleItem>();

        try
        {
            var catalog = await _sqlSyncService.GetEmployeeCatalogAsync();
            var schedules = await _sqlSyncService.GetWorkSchedulesAsync();

            catalogByUser = catalog
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            schedulesByCode = schedules
                .GroupBy(x => x.Code)
                .ToDictionary(g => g.Key, g => g.First());
        }
        catch (Exception ex)
        {
            UpdateStatus($"No se pudieron cargar datos RH para generación masiva. Se continuará con datos básicos. Detalle: {ex.Message}");
        }

        var orderedEmployees = employees.OrderBy(e => e.UserId).ToList();
        var reports = new List<string>(orderedEmployees.Count);
        var employeesWithoutRecords = 0;
        decimal totalWorkedHours = 0m;
        decimal totalExtraHours = 0m;

        for (var index = 0; index < orderedEmployees.Count; index++)
        {
            var employee = orderedEmployees[index];

            var employeeRequest = new ReportRequest
            {
                UserId = employee.UserId,
                FechaInicio = baseRequest.FechaInicio,
                FechaFin = baseRequest.FechaFin,
                OutputDirectory = baseRequest.OutputDirectory,
                TemplatePath = baseRequest.TemplatePath,
                EmployeeName = employee.EmployeeName,
                Company = baseRequest.Company
            };

            if (catalogByUser.TryGetValue(employee.UserId, out var catalogEntry))
            {
                employeeRequest = BuildRequestWithRhData(employeeRequest, catalogEntry, schedulesByCode);
            }
            else
            {
                employeeRequest = await EnrichRequestWithEmployeeRhDataAsync(employeeRequest);
            }

            var employeeSummary = await _orchestrator.GenerateWithSummaryAsync(employeeRequest, outputFormat: outputFormat);
            employeesWithoutRecords += employeeSummary.EmployeesWithoutRecords;
            totalWorkedHours += employeeSummary.TotalWorkedHours;
            totalExtraHours += employeeSummary.TotalExtraHours;
            reports.AddRange(employeeSummary.ReportPaths);

            var processed = index + 1;
            if (processed == 1 || processed == orderedEmployees.Count || processed % 10 == 0)
            {
                UpdateStatus($"Generados {processed}/{orderedEmployees.Count} reportes...");
            }
        }

        return new ReportRunSummary
        {
            EmployeesProcessed = orderedEmployees.Count,
            EmployeesWithoutRecords = employeesWithoutRecords,
            TotalWorkedHours = totalWorkedHours,
            TotalExtraHours = totalExtraHours,
            ReportPaths = reports
        };
    }

    private async Task<ReportRequest> EnrichRequestWithEmployeeRhDataAsync(ReportRequest request)
    {
        try
        {
            var catalog = await _sqlSyncService.GetEmployeeCatalogAsync();
            var schedulesByCode = (await _sqlSyncService.GetWorkSchedulesAsync())
                .GroupBy(x => x.Code)
                .ToDictionary(g => g.Key, g => g.First());

            var entry = catalog.FirstOrDefault(e => e.UserId == request.UserId);
            if (entry is not null)
            {
                return BuildRequestWithRhData(request, entry, schedulesByCode);
            }

            var snapshot = await _sqlSyncService.GetEmployeeAdminSnapshotAsync(request.UserId);
            if (snapshot is null)
            {
                return request;
            }

            return BuildRequestWithRhData(request, snapshot, schedulesByCode);
        }
        catch (Exception ex)
        {
            UpdateStatus($"No se pudieron leer datos RH desde SQL para preview/reporte. Detalle: {ex.Message}");
            return request;
        }
    }

    private static ReportRequest BuildRequestWithRhData(
        ReportRequest request,
        SqlEmployeeCatalogItem entry,
        IReadOnlyDictionary<int, WorkScheduleItem> schedulesByCode)
    {
        WorkScheduleItem? schedule = null;
        if (entry.WorkScheduleCode.HasValue)
        {
            schedulesByCode.TryGetValue(entry.WorkScheduleCode.Value, out schedule);
        }

        var workScheduleDisplay = schedule is not null
            ? $"{schedule.StartTime:hh\\:mm} a {schedule.EndTime:hh\\:mm} Hrs"
            : (entry.WorkScheduleCode.HasValue ? $"Horario {entry.WorkScheduleCode.Value}" : null);

        var workShiftType = ResolveShiftType(schedule, entry.WorkScheduleCode);

        var employeeName = !string.IsNullOrWhiteSpace(entry.SqlName)
            ? entry.SqlName
            : (!string.IsNullOrWhiteSpace(entry.DeviceName) ? entry.DeviceName : request.EmployeeName);

        return new ReportRequest
        {
            UserId = request.UserId,
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            OutputDirectory = request.OutputDirectory,
            TemplatePath = request.TemplatePath,
            EmployeeName = employeeName,
            Company = request.Company,
            Area = string.IsNullOrWhiteSpace(entry.Department) ? request.Area : entry.Department,
            Position = string.IsNullOrWhiteSpace(entry.Position) ? request.Position : entry.Position,
            WorkScheduleCode = entry.WorkScheduleCode ?? request.WorkScheduleCode,
            WorkScheduleDisplay = !string.IsNullOrWhiteSpace(workScheduleDisplay) ? workScheduleDisplay : request.WorkScheduleDisplay,
            WorkShiftType = !string.IsNullOrWhiteSpace(workShiftType) ? workShiftType : request.WorkShiftType
        };
    }

    private static ReportRequest BuildRequestWithRhData(
        ReportRequest request,
        EmployeeAdminSnapshot snapshot,
        IReadOnlyDictionary<int, WorkScheduleItem> schedulesByCode)
    {
        var synthetic = new SqlEmployeeCatalogItem(
            snapshot.UserId,
            request.EmployeeName ?? $"Empleado_{snapshot.UserId}",
            snapshot.SqlName,
            null,
            false,
            snapshot.Department,
            snapshot.Position,
            snapshot.PervasiveDept,
            snapshot.PervasiveShift,
            snapshot.WorkScheduleCode,
            snapshot.OvertimeAuthorized,
            0m);

        return BuildRequestWithRhData(request, synthetic, schedulesByCode);
    }

    private static string? ResolveShiftType(WorkScheduleItem? schedule, int? workScheduleCode)
    {
        if (schedule is not null)
        {
            return schedule.StartTime > schedule.EndTime
                ? "Nocturna"
                : "Diurna";
        }

        return workScheduleCode switch
        {
            1 or 2 => "Diurna",
            3 => "Nocturna",
            _ => null
        };
    }


    private void btnOpenReport_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastReportPath) || !File.Exists(_lastReportPath))
        {
            MessageBox.Show("No hay un reporte reciente para abrir.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = _lastReportPath,
            UseShellExecute = true
        };

        Process.Start(psi);
    }

    private void btnBrowseOutput_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.SelectedPath = txtOutputDirectory.Text;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtOutputDirectory.Text = dialog.SelectedPath;
        }
    }

    private void btnBrowseTemplate_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|Excel sin macros (*.xlsx)|*.xlsx|Excel con macros (*.xlsm)|*.xlsm",
            FileName = txtTemplatePath.Text
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtTemplatePath.Text = dialog.FileName;
        }
    }

    private void UpdateStatus(string text)
    {
        var line = $"{DateTime.Now:HH:mm} · {text}";
        if (string.IsNullOrWhiteSpace(txtStatus.Text))
        {
            txtStatus.Text = line;
        }
        else
        {
            txtStatus.AppendText(Environment.NewLine + line);
        }

        txtStatus.SelectionStart = txtStatus.TextLength;
        txtStatus.ScrollToCaret();

        const int maxLength = 9000;
        if (txtStatus.TextLength > maxLength)
        {
            txtStatus.Text = txtStatus.Text[^maxLength..];
            txtStatus.SelectionStart = txtStatus.TextLength;
            txtStatus.ScrollToCaret();
        }
    }

    private async void btnTestConnection_Click(object sender, EventArgs e)
    {
        await RefreshConnectionStatusAsync(showMessageBox: true);
    }

    private async void connectionTimer_Tick(object sender, EventArgs e)
    {
        await RefreshConnectionStatusAsync(showMessageBox: false);
    }

    private async Task RefreshConnectionStatusAsync(bool showMessageBox)
    {
        if (_connectionProbeRunning)
        {
            return;
        }

        _connectionProbeRunning = true;
        SetBusy(true);

        try
        {
            var result = await ProbeConnectionAsync();
            SetConnectionIndicator(result.IsConnected, result.SourceLabel, result.Detail, true);

            if (showMessageBox)
            {
                var icon = result.IsConnected ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
                var prefix = result.IsConnected ? "✅" : "❌";
                MessageBox.Show(
                    $"{prefix} {result.Summary}\n\nDetalle: {result.Detail}",
                    "Prueba de conexión",
                    MessageBoxButtons.OK,
                    icon);
            }
        }
        catch (Exception ex)
        {
            SetConnectionIndicator(false, "Error de prueba", ex.Message, true);

            if (showMessageBox)
            {
                MessageBox.Show(
                    $"❌ Error al probar conexión\n\nDetalle: {ex.Message}",
                    "Prueba de conexión",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            _connectionProbeRunning = false;
            SetBusy(false);
        }
    }

    private async Task<ConnectionProbeResult> ProbeConnectionAsync()
    {
        var useDirect = _configuration.Device.Enabled && _configuration.Device.PreferDirect;
        if (useDirect)
        {
            var directResult = await Task.Run(ProbeDirectConnection);
            if (directResult.IsConnected || !_configuration.Device.FallbackToAccess)
            {
                return directResult;
            }

            var accessFallback = await _diagnostics.ProbeAsync();
            if (accessFallback.IsConnected)
            {
                var accessDetail = BuildAccessDetail(accessFallback);
                return new ConnectionProbeResult(
                    true,
                    "Access (fallback)",
                    "Conectado por fallback a Access MDB",
                    accessDetail);
            }

            return new ConnectionProbeResult(
                false,
                "Sin conexión",
                "Sin conexión en modo directo ni fallback",
                $"Directo: {directResult.Detail} | Access: {accessFallback.Message}");
        }

        var accessResult = await _diagnostics.ProbeAsync();
        if (!accessResult.IsConnected)
        {
            return new ConnectionProbeResult(
                false,
                "Access MDB",
                "Sin conexión a Access MDB",
                accessResult.Message);
        }

        return new ConnectionProbeResult(
            true,
            "Access MDB",
            "Conectado a Access MDB",
            BuildAccessDetail(accessResult));
    }

    private ConnectionProbeResult ProbeDirectConnection()
    {
        var zkemType = Type.GetTypeFromProgID("zkemkeeper.CZKEM")
                       ?? Type.GetTypeFromProgID("zkemkeeper.ZKEM")
                       ?? Type.GetTypeFromProgID("zkemkeeper.ZKEM.1");

        if (zkemType is null)
        {
            return new ConnectionProbeResult(
                false,
                "ZKTeco Direct",
                "Sin SDK COM ZKTeco",
                "No se encontró zkemkeeper (CZKEM/ZKEM). Ejecuta instalar-zkteco-sdk-admin.ps1.");
        }

        object? comObject = null;
        try
        {
            comObject = Activator.CreateInstance(zkemType);
            if (comObject is null)
            {
                return new ConnectionProbeResult(false, "ZKTeco Direct", "Instancia COM nula", "No se pudo crear instancia del SDK.");
            }

            dynamic zk = comObject;
            try
            {
                if (_configuration.Device.CommKey > 0)
                {
                    zk.SetCommPassword(_configuration.Device.CommKey);
                }
            }
            catch
            {
            }

            var connected = zk.Connect_Net(_configuration.Device.Ip, _configuration.Device.Port);
            if (!(connected is bool ok) || !ok)
            {
                return new ConnectionProbeResult(
                    false,
                    "ZKTeco Direct",
                    "Sin conexión directa al reloj",
                    $"No se pudo conectar a {_configuration.Device.Ip}:{_configuration.Device.Port}.");
            }

            try
            {
                zk.EnableDevice(_configuration.Device.MachineNumber, true);
            }
            catch
            {
            }

            return new ConnectionProbeResult(
                true,
                "ZKTeco Direct",
                "Conectado al reloj por modo directo",
                $"Dispositivo {_configuration.Device.Ip}:{_configuration.Device.Port}, máquina {_configuration.Device.MachineNumber}.");
        }
        catch (Exception ex)
        {
            return new ConnectionProbeResult(
                false,
                "ZKTeco Direct",
                "Error en conexión directa",
                ex.Message);
        }
        finally
        {
            try
            {
                if (comObject is not null)
                {
                    dynamic zk = comObject;
                    zk.Disconnect();
                }
            }
            catch
            {
            }

            if (comObject is not null && Marshal.IsComObject(comObject))
            {
                try
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
                catch
                {
                }
            }
        }
    }

    private static string BuildAccessDetail(Checador.Domain.Models.AccessDiagnosticsResult result)
    {
        var ultimaMarca = result.LastCheckTime.HasValue
            ? result.LastCheckTime.Value.ToString("dd/MM/yyyy HH:mm:ss")
            : "Sin marcas";
        return $"Empleados: {result.EmployeeCount}. Última marca: {ultimaMarca}.";
    }

    private void SetConnectionIndicator(bool connected, string sourceLabel, string detail, bool writeLog)
    {
        var colorAnterior = pnlConnectionIndicator.BackColor;
        var colorNuevo = connected
            ? Color.FromArgb(16, 124, 16)
            : Color.FromArgb(196, 43, 28);
        _ = AnimationHelper.AnimatePanelColorAsync(
            pnlConnectionIndicator,
            colorAnterior,
            colorNuevo,
            500);
        lblConnectionValue.Text = connected
            ? $"Conectado · {sourceLabel}"
            : $"Sin conexión · {sourceLabel}";
        lblConnectionValue.PillBackColor = connected
            ? Color.FromArgb(222, 247, 236)
            : Color.FromArgb(255, 234, 234);
        lblConnectionValue.PillForeColor = connected
            ? Color.FromArgb(12, 84, 30)
            : Color.FromArgb(114, 28, 36);
        lblConnectionValue.Invalidate();

        var snapshot = $"{lblConnectionValue.Text} | {detail}";
        if (writeLog && !string.Equals(snapshot, _lastConnectionUiSnapshot, StringComparison.Ordinal))
        {
            UpdateStatus(snapshot);
            _lastConnectionUiSnapshot = snapshot;
        }
    }

    private void SetBusy(bool busy)
    {
        btnGenerate.Enabled = !busy;
        btnPreview.Enabled = !busy;
        btnTestConnection.Enabled = !busy;
        btnBrowseOutput.Enabled = !busy;
        btnBrowseTemplate.Enabled = !busy;
        btnAdminSql.Enabled = !busy;
        chkAllEmployees.Enabled = !busy;
        cmbEmployee.Enabled = !busy && !chkAllEmployees.Checked;
        dtpStart.Enabled = !busy;
        dtpEnd.Enabled = !busy;

        var targetValue = busy ? progressAction.Maximum : progressAction.Minimum;
        _ = AnimationHelper.AnimateProgressAsync(progressAction, targetValue, 400);
        progressAction.Visible = busy;
        UseWaitCursor = busy;

        if (!busy)
        {
            btnOpenReport.Enabled = !string.IsNullOrWhiteSpace(_lastReportPath);
        }
    }

    private void ShowKpiSummary(ReportRunSummary summary, string? customSubtitle = null)
    {
        using var kpi = new KpiSummaryForm(summary);
        if (!string.IsNullOrWhiteSpace(customSubtitle))
        {
            kpi.ResumenAdicional = customSubtitle;
        }

        kpi.ShowDialog(this);
    }

    private void cmbTheme_SelectedIndexChanged(object sender, EventArgs e)
    {
        var selectedTheme = cmbTheme.SelectedItem?.ToString() ?? ThemeManager.CurrentTheme;
        ThemeManager.Apply(selectedTheme);
        ApplyTheme(cmbTheme.SelectedIndex == 1);
    }

    private void ApplyTheme(bool dark)
    {
        var formBack = dark ? Color.FromArgb(30, 33, 40) : Color.FromArgb(245, 247, 250);
        var cardBack = dark ? Color.FromArgb(45, 49, 57) : Color.White;
        var primaryText = dark ? Color.FromArgb(235, 239, 245) : Color.FromArgb(17, 37, 62);
        var secondaryText = dark ? Color.FromArgb(190, 198, 210) : Color.FromArgb(90, 100, 115);
        var inputBack = dark ? Color.FromArgb(37, 41, 48) : Color.White;

        BackColor = formBack;
        pnlTopBar.BackColor = dark ? Color.FromArgb(39, 43, 51) : Color.White;
        lblAppGlyph.ForeColor = dark ? Color.FromArgb(86, 163, 255) : Color.FromArgb(0, 120, 215);
        lblTitle.ForeColor = primaryText;
        lblSubtitle.ForeColor = secondaryText;

        var groups = new[] { grpFiltros, grpUbicaciones, grpAcciones, grpEstado };
        foreach (var group in groups)
        {
            group.BackColor = cardBack;
            group.ForeColor = primaryText;
        }

        foreach (var control in Controls)
        {
            if (control is Label label && label != lblTitle && label != lblSubtitle)
            {
                label.ForeColor = primaryText;
                label.BackColor = Color.Transparent;
            }
        }

        var inputs = new Control[] { txtOutputDirectory, txtTemplatePath, cmbTheme };
        foreach (var input in inputs)
        {
            input.BackColor = inputBack;
            input.ForeColor = primaryText;
        }

        cmbEmployee.BackColor = inputBack;
        cmbEmployee.ForeColor = primaryText;
        dtpStart.CalendarMonthBackground = inputBack;
        dtpEnd.CalendarMonthBackground = inputBack;

        StyleButton(btnGenerate, primary: true, dark);
        StyleButton(btnPreview, primary: false, dark);
        StyleButton(btnOpenReport, primary: false, dark);
        StyleButton(btnTestConnection, primary: false, dark);
        StyleButton(btnAdminSql, primary: false, dark);
        StyleButton(btnBrowseOutput, primary: false, dark, compact: true);
        StyleButton(btnBrowseTemplate, primary: false, dark, compact: true);

        txtStatus.BackColor = dark ? Color.FromArgb(34, 38, 46) : Color.FromArgb(247, 250, 255);
        txtStatus.ForeColor = dark ? Color.FromArgb(222, 232, 245) : Color.FromArgb(45, 58, 79);
        txtStatus.BorderStyle = BorderStyle.None;
        pnlConnectionIndicator.BorderStyle = BorderStyle.FixedSingle;

        lblConnectionTitle.ForeColor = primaryText;
    }

    private void ApplyResponsiveScale()
    {
        var factor = Math.Clamp(ClientSize.Width / 980f, 1f, 1.35f);
        var verticalBoost = Math.Clamp(ClientSize.Height / 550f, 1f, 1.28f);
        var scale = Math.Min(factor, verticalBoost);

        foreach (var pair in _buttonBaseSizes)
        {
            var b = pair.Key;
            var baseSize = pair.Value;
            var widthScale = b == btnGenerate || b == btnPreview || b == btnOpenReport || b == btnTestConnection || b == btnAdminSql
                ? Math.Min(1.0f, scale)
                : scale;
            b.Size = new Size(
                (int)Math.Round(baseSize.Width * widthScale),
                (int)Math.Round(baseSize.Height * Math.Min(1.2f, scale)));
        }

        grpFiltros.Padding = new Padding((int)Math.Round(10 * scale));
        grpUbicaciones.Padding = new Padding((int)Math.Round(10 * scale));
        grpAcciones.Padding = new Padding((int)Math.Round(10 * scale));
        grpEstado.Padding = new Padding((int)Math.Round(10 * scale));

        txtStatus.Font = new Font("Segoe UI", 9.2F * Math.Min(1.12f, scale), FontStyle.Regular);
        lblConnectionValue.Font = new Font("Segoe UI", 8.7F * Math.Min(1.15f, scale), FontStyle.Bold);
    }

    private static void StyleButton(Button button, bool primary, bool dark, bool compact = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.Cursor = Cursors.Hand;

        if (primary)
        {
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(0, 120, 215);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", compact ? 8.5F : 9F, FontStyle.Bold);
            return;
        }

        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = dark ? Color.FromArgb(82, 90, 102) : Color.FromArgb(210, 218, 230);
        button.BackColor = dark ? Color.FromArgb(52, 58, 68) : Color.White;
        button.ForeColor = dark ? Color.White : Color.FromArgb(35, 50, 70);
        button.Font = new Font("Segoe UI", compact ? 8.5F : 9F, FontStyle.Regular);
    }

    private sealed record ConnectionProbeResult(bool IsConnected, string SourceLabel, string Summary, string Detail);

    private sealed record EmployeeComboItem(int UserId, string EmployeeName)
    {
        public string DisplayText => $"{UserId} - {EmployeeName}";
    }
}


