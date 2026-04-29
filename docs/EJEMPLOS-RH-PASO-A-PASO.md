# Ejemplos Prácticos: Implementar Módulo RH Paso a Paso

## 📌 Caso de Uso: Sistema de Vacaciones Completo

Vamos a implementar **solicitud y aprobación de vacaciones** desde cero.

---

## 1️⃣ CREAR MODELOS (Domain/Models)

### Archivo: `Checador.Domain/Models/VacationRequest.cs`

```csharp
namespace Checador.Domain.Models;

/// <summary>
/// Representa una solicitud de vacaciones.
/// Estado: 0=Pending, 1=Approved, 2=Rejected, 3=Cancelled
/// </summary>
public sealed record VacationRequest(
    int VacationId,
    int UserId,
    DateTime StartDate,
    DateTime EndDate,
    int DaysRequested,
    int Status, // 0-3 (mejor que enum para BD)
    string? Notes,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    int? ApprovedBy
);

/// <summary>
/// Constantes de estado para evitar hardcode.
/// </summary>
public static class VacationStatus
{
    public const int Pending = 0;
    public const int Approved = 1;
    public const int Rejected = 2;
    public const int Cancelled = 3;
}
```

### Archivo: `Checador.Domain/Models/VacationReport.cs`

Para reportes consolidados:
```csharp
namespace Checador.Domain.Models;

public sealed record VacationSummary(
    int UserId,
    string EmployeeName,
    int TotalVacationDaysYear,
    int UsedVacationDays,
    int PendingVacationDays,
    int AvailableVacationDays,
    List<VacationRequest> DetailVacations
);
```

---

## 2️⃣ CREAR INTERFACES (Domain/Interfaces)

### Archivo: `Checador.Domain/Interfaces/IVacationRepository.cs`

```csharp
using Checador.Domain.Models;

namespace Checador.Domain.Interfaces;

public interface IVacationRepository
{
    /// <summary>
    /// Obtener una solicitud por ID.
    /// </summary>
    Task<VacationRequest?> GetByIdAsync(int vacationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtener todas las solicitudes de un empleado.
    /// </summary>
    Task<IReadOnlyList<VacationRequest>> GetByUserAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtener solicitudes por estado.
    /// </summary>
    Task<IReadOnlyList<VacationRequest>> GetByStatusAsync(
        int status,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtener solicitudes pendientes de aprobación.
    /// </summary>
    Task<IReadOnlyList<VacationRequest>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Insertar nueva solicitud.
    /// </summary>
    Task<int> InsertAsync(VacationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualizar solicitud existente.
    /// </summary>
    Task UpdateAsync(VacationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Contar días usados en un rango de fechas.
    /// </summary>
    Task<int> CountUsedDaysAsync(
        int userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
```

---

## 3️⃣ CREAR TABLA SQL

### En `Checador.Infrastructure.Access/SqlDeviceSyncService.cs`

Busca el método `EnsureSchemaAsync()` (~línea 500) y agrega esto:

```csharp
// Dentro del método EnsureSchemaAsync(), después de crear Employees:

private async Task CreateVacationTablesAsync(SqlConnection connection, CancellationToken cancellationToken = default)
{
    const string createVacationRequests = @"
IF OBJECT_ID('dbo.VacationRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.VacationRequests (
        VacationId INT NOT NULL IDENTITY(1,1) PRIMARY KEY CLUSTERED,
        UserId INT NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        DaysRequested INT NOT NULL,
        [Status] INT NOT NULL CONSTRAINT DF_VacationRequests_Status DEFAULT (0),
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VacationRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
        ApprovedAt DATETIME2 NULL,
        ApprovedBy INT NULL,
        
        CONSTRAINT FK_VacationRequests_UserId FOREIGN KEY (UserId) 
            REFERENCES dbo.Employees(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_VacationRequests_ApprovedBy FOREIGN KEY (ApprovedBy) 
            REFERENCES dbo.Employees(UserId) ON DELETE SET NULL
    );
    
    CREATE NONCLUSTERED INDEX IX_VacationRequests_UserId ON dbo.VacationRequests(UserId);
    CREATE NONCLUSTERED INDEX IX_VacationRequests_Status ON dbo.VacationRequests([Status]);
    CREATE NONCLUSTERED INDEX IX_VacationRequests_StartDate ON dbo.VacationRequests(StartDate);
END;

-- Agregar columnas faltantes si BD existente
IF COL_LENGTH('dbo.VacationRequests', 'ApprovedBy') IS NULL
BEGIN
    ALTER TABLE dbo.VacationRequests ADD ApprovedBy INT NULL;
END;
";

    await using var cmd = new SqlCommand(createVacationRequests, connection);
    await cmd.ExecuteNonQueryAsync(cancellationToken);
}

// Luego, en EnsureSchemaAsync(), llama:
// await CreateVacationTablesAsync(connection, cancellationToken);
```

---

## 4️⃣ CREAR REPOSITORIO (Infrastructure.Access)

### Archivo: `Checador.Infrastructure.Access/SqlVacationRepository.cs`

```csharp
using Checador.Domain.Interfaces;
using Checador.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Checador.Infrastructure.Access;

public sealed class SqlVacationRepository : IVacationRepository
{
    private readonly SqlSyncOptions _options;

    public SqlVacationRepository(SqlSyncOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<VacationRequest?> GetByIdAsync(
        int vacationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT VacationId, UserId, StartDate, EndDate, DaysRequested, [Status],
       Notes, CreatedAt, ApprovedAt, ApprovedBy
FROM dbo.VacationRequests
WHERE VacationId = @VacationId";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@VacationId", vacationId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadVacationRecord(reader, cancellationToken);
        }

        return null;
    }

    public async Task<IReadOnlyList<VacationRequest>> GetByUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT VacationId, UserId, StartDate, EndDate, DaysRequested, [Status],
       Notes, CreatedAt, ApprovedAt, ApprovedBy
FROM dbo.VacationRequests
WHERE UserId = @UserId
ORDER BY CreatedAt DESC";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var result = new List<VacationRequest>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadVacationRecord(reader, cancellationToken));
        }

        return result;
    }

    public async Task<IReadOnlyList<VacationRequest>> GetByStatusAsync(
        int status,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
SELECT VacationId, UserId, StartDate, EndDate, DaysRequested, [Status],
       Notes, CreatedAt, ApprovedAt, ApprovedBy
FROM dbo.VacationRequests
WHERE [Status] = @Status";

        if (fromDate.HasValue)
            sql += " AND StartDate >= @FromDate";
        if (toDate.HasValue)
            sql += " AND EndDate <= @ToDate";

        sql += " ORDER BY StartDate ASC";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Status", status);
        if (fromDate.HasValue)
            cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.Date);
        if (toDate.HasValue)
            cmd.Parameters.AddWithValue("@ToDate", toDate.Value.Date);

        var result = new List<VacationRequest>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadVacationRecord(reader, cancellationToken));
        }

        return result;
    }

    public async Task<IReadOnlyList<VacationRequest>> GetPendingApprovalsAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetByStatusAsync(VacationStatus.Pending, cancellationToken: cancellationToken);
    }

    public async Task<int> InsertAsync(
        VacationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
INSERT INTO dbo.VacationRequests 
(UserId, StartDate, EndDate, DaysRequested, [Status], Notes, CreatedAt)
VALUES (@UserId, @StartDate, @EndDate, @DaysRequested, @Status, @Notes, @CreatedAt);

SELECT SCOPE_IDENTITY();";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", request.UserId);
        cmd.Parameters.AddWithValue("@StartDate", request.StartDate.Date);
        cmd.Parameters.AddWithValue("@EndDate", request.EndDate.Date);
        cmd.Parameters.AddWithValue("@DaysRequested", request.DaysRequested);
        cmd.Parameters.AddWithValue("@Status", request.Status);
        cmd.Parameters.AddWithValue("@Notes", request.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

        var insertedId = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(insertedId);
    }

    public async Task UpdateAsync(
        VacationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
UPDATE dbo.VacationRequests
SET UserId = @UserId,
    StartDate = @StartDate,
    EndDate = @EndDate,
    DaysRequested = @DaysRequested,
    [Status] = @Status,
    Notes = @Notes,
    ApprovedAt = @ApprovedAt,
    ApprovedBy = @ApprovedBy
WHERE VacationId = @VacationId";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@VacationId", request.VacationId);
        cmd.Parameters.AddWithValue("@UserId", request.UserId);
        cmd.Parameters.AddWithValue("@StartDate", request.StartDate.Date);
        cmd.Parameters.AddWithValue("@EndDate", request.EndDate.Date);
        cmd.Parameters.AddWithValue("@DaysRequested", request.DaysRequested);
        cmd.Parameters.AddWithValue("@Status", request.Status);
        cmd.Parameters.AddWithValue("@Notes", request.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ApprovedAt", request.ApprovedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ApprovedBy", request.ApprovedBy ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountUsedDaysAsync(
        int userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT COALESCE(SUM(DaysRequested), 0)
FROM dbo.VacationRequests
WHERE UserId = @UserId
  AND [Status] IN (1, 2)  -- Approved or being used
  AND StartDate >= @From
  AND EndDate <= @To";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@From", from.Date);
        cmd.Parameters.AddWithValue("@To", to.Date);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result ?? 0);
    }

    // Helper privado para no repetir lectura
    private static VacationRequest ReadVacationRecord(SqlDataReader reader, CancellationToken cancellationToken)
    {
        return new VacationRequest(
            reader.GetInt32(0),                                             // VacationId
            reader.GetInt32(1),                                             // UserId
            reader.GetDateTime(2),                                          // StartDate
            reader.GetDateTime(3),                                          // EndDate
            reader.GetInt32(4),                                             // DaysRequested
            reader.GetInt32(5),                                             // Status
            reader.IsDBNull(6) ? null : reader.GetString(6),                // Notes
            reader.GetDateTime(7),                                          // CreatedAt
            reader.IsDBNull(8) ? null : reader.GetDateTime(8),              // ApprovedAt
            reader.IsDBNull(9) ? null : reader.GetInt32(9)                  // ApprovedBy
        );
    }
}
```

---

## 5️⃣ CREAR SERVICIO DE LÓGICA (Application)

### Archivo: `Checador.Application/RhVacationService.cs`

```csharp
using Checador.Domain.Interfaces;
using Checador.Domain.Models;

namespace Checador.Application;

/// <summary>
/// Servicio de lógica de negocio para vacaciones.
/// - Validar solicitudes
/// - Calcular días
/// - Aprobar/rechazar
/// </summary>
public sealed class RhVacationService
{
    private readonly IVacationRepository _repo;
    
    // Configuración de negocio
    private const int MaxVacationDaysPerYear = 15;
    private const int MinDaysInAdvance = 5;

    public RhVacationService(IVacationRepository vacationRepository)
    {
        _repo = vacationRepository ?? throw new ArgumentNullException(nameof(vacationRepository));
    }

    /// <summary>
    /// Solicitar vacaciones. Valida fechas y disponibilidad.
    /// </summary>
    public async Task<int> RequestVacationAsync(
        int userId,
        DateTime startDate,
        DateTime endDate,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        // Validar fechas
        if (startDate > endDate)
            throw new InvalidOperationException("Fecha inicio no puede ser mayor a fecha fin.");

        if (startDate.Date <= DateTime.Now.Date.AddDays(MinDaysInAdvance))
            throw new InvalidOperationException($"Las vacaciones deben solicitarse con mínimo {MinDaysInAdvance} días de anticipación.");

        // Calcular días (inclusive)
        var daysRequested = (endDate.Date - startDate.Date).Days + 1;

        if (daysRequested <= 0)
            throw new InvalidOperationException("Debe solicitar al menos 1 día.");

        // Verificar disponibilidad
        var usedDaysThisYear = await _repo.CountUsedDaysAsync(
            userId,
            new DateTime(DateTime.Now.Year, 1, 1),
            new DateTime(DateTime.Now.Year, 12, 31),
            cancellationToken);

        if (usedDaysThisYear + daysRequested > MaxVacationDaysPerYear)
            throw new InvalidOperationException(
                $"Excede límite anual. Disponibles: {MaxVacationDaysPerYear - usedDaysThisYear} días.");

        // Crear solicitud
        var request = new VacationRequest(
            vacationId: 0, // Se genera en BD
            userId: userId,
            startDate: startDate,
            endDate: endDate,
            daysRequested: daysRequested,
            status: VacationStatus.Pending,
            notes: notes,
            createdAt: DateTime.UtcNow,
            approvedAt: null,
            approvedBy: null
        );

        return await _repo.InsertAsync(request, cancellationToken);
    }

    /// <summary>
    /// Aprobar solicitud (solo RH/Manager).
    /// </summary>
    public async Task ApproveVacationAsync(
        int vacationId,
        int approvedByUserId,
        CancellationToken cancellationToken = default)
    {
        var vacation = await _repo.GetByIdAsync(vacationId, cancellationToken)
            ?? throw new InvalidOperationException("Solicitud no encontrada.");

        if (vacation.Status != VacationStatus.Pending)
            throw new InvalidOperationException("Solo se pueden aprobar solicitudes pendientes.");

        var approved = vacation with
        {
            Status = VacationStatus.Approved,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = approvedByUserId
        };

        await _repo.UpdateAsync(approved, cancellationToken);
    }

    /// <summary>
    /// Rechazar solicitud (solo RH/Manager).
    /// </summary>
    public async Task RejectVacationAsync(
        int vacationId,
        int rejectedByUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var vacation = await _repo.GetByIdAsync(vacationId, cancellationToken)
            ?? throw new InvalidOperationException("Solicitud no encontrada.");

        if (vacation.Status != VacationStatus.Pending)
            throw new InvalidOperationException("Solo se pueden rechazar solicitudes pendientes.");

        var rejected = vacation with
        {
            Status = VacationStatus.Rejected,
            Notes = reason,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = rejectedByUserId
        };

        await _repo.UpdateAsync(rejected, cancellationToken);
    }

    /// <summary>
    /// Obtener historial de vacaciones de un empleado.
    /// </summary>
    public async Task<IReadOnlyList<VacationRequest>> GetEmployeeVacationsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _repo.GetByUserAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Obtener solicitudes pendientes de aprobación.
    /// </summary>
    public async Task<IReadOnlyList<VacationRequest>> GetPendingApprovalsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _repo.GetPendingApprovalsAsync(cancellationToken);
    }

    /// <summary>
    /// Calcular resumen de vacaciones para un empleado.
    /// </summary>
    public async Task<VacationSummary> GetEmployeeVacationSummaryAsync(
        int userId,
        string employeeName,
        CancellationToken cancellationToken = default)
    {
        var allVacations = await _repo.GetByUserAsync(userId, cancellationToken);

        var usedDays = allVacations
            .Where(v => v.Status == VacationStatus.Approved)
            .Sum(v => v.DaysRequested);

        var pendingDays = allVacations
            .Where(v => v.Status == VacationStatus.Pending)
            .Sum(v => v.DaysRequested);

        return new VacationSummary(
            userId: userId,
            employeeName: employeeName,
            totalVacationDaysYear: MaxVacationDaysPerYear,
            usedVacationDays: usedDays,
            pendingVacationDays: pendingDays,
            availableVacationDays: MaxVacationDaysPerYear - usedDays - pendingDays,
            detailVacations: allVacations.ToList()
        );
    }
}
```

---

## 6️⃣ REGISTRAR EN INYECCIÓN DE DEPENDENCIAS

### En `Checador.Desktop/Program.cs` (o donde tengas DI)

```csharp
// En el método ConfigureServices o similar
services.AddSingleton<SqlSyncOptions>(sqlOptions);

// Repositorio
services.AddSingleton<IVacationRepository>(sp => 
    new SqlVacationRepository(sp.GetRequiredService<SqlSyncOptions>()));

// Servicio
services.AddSingleton<RhVacationService>(sp =>
    new RhVacationService(sp.GetRequiredService<IVacationRepository>()));
```

---

## 7️⃣ USAR EN UI (WinForms)

### Archivo: `Checador.Desktop/Forms/RhVacationForm.cs`

```csharp
using Checador.Application;
using System.Windows.Forms;

namespace Checador.Desktop.Forms;

public partial class RhVacationForm : Form
{
    private readonly RhVacationService _vacationService;

    public RhVacationForm(RhVacationService vacationService)
    {
        InitializeComponent();
        _vacationService = vacationService;
    }

    private void RhVacationForm_Load(object sender, EventArgs e)
    {
        LoadVacationSummary();
    }

    private async void LoadVacationSummary()
    {
        try
        {
            // Obtener ID del usuario logueado (ajusta según tu auth)
            int userId = GetCurrentUserId();

            var summary = await _vacationService.GetEmployeeVacationSummaryAsync(
                userId,
                "Empleado", // Obtener nombre real
                CancellationToken.None);

            labelTotal.Text = $"Total: {summary.TotalVacationDaysYear} días";
            labelUsed.Text = $"Usados: {summary.UsedVacationDays} días";
            labelPending.Text = $"Pendientes: {summary.PendingVacationDays} días";
            labelAvailable.Text = $"Disponibles: {summary.AvailableVacationDays} días";

            // Cargar historial en DataGridView
            dataGridVacations.DataSource = summary.DetailVacations
                .Select(v => new
                {
                    v.VacationId,
                    Desde = v.StartDate.ToString("yyyy-MM-dd"),
                    Hasta = v.EndDate.ToString("yyyy-MM-dd"),
                    Días = v.DaysRequested,
                    Estado = GetStatusLabel(v.Status),
                    v.Notes
                })
                .ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar vacaciones: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnRequestVacation_Click(object sender, EventArgs e)
    {
        try
        {
            int userId = GetCurrentUserId();
            DateTime startDate = dateStartDate.Value.Date;
            DateTime endDate = dateEndDate.Value.Date;
            string notes = txtNotes.Text.Trim();

            int vacationId = await _vacationService.RequestVacationAsync(
                userId,
                startDate,
                endDate,
                string.IsNullOrEmpty(notes) ? null : notes,
                CancellationToken.None);

            MessageBox.Show(
                $"Solicitud registrada exitosamente.\nID: {vacationId}\nPendiente de aprobación.",
                "Éxito",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            LoadVacationSummary();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show($"Error en validación: {ex.Message}", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnApprovePending_Click(object sender, EventArgs e)
    {
        try
        {
            var pending = await _vacationService.GetPendingApprovalsAsync(CancellationToken.None);

            if (pending.Count == 0)
            {
                MessageBox.Show("No hay solicitudes pendientes.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Mostrar en otro form para seleccionar cuál aprobar
            using (var approvalForm = new ApproveVacationForm(_vacationService, pending))
            {
                if (approvalForm.ShowDialog(this) == DialogResult.OK)
                {
                    LoadVacationSummary();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private int GetCurrentUserId()
    {
        // Obtener del contexto de usuario actual de tu app
        // Esto es pseudo-código, ajusta a tu implementación real
        return SessionManager.CurrentUserId;
    }

    private string GetStatusLabel(int status)
    {
        return status switch
        {
            VacationStatus.Pending => "⏳ Pendiente",
            VacationStatus.Approved => "✅ Aprobado",
            VacationStatus.Rejected => "❌ Rechazado",
            VacationStatus.Cancelled => "🚫 Cancelado",
            _ => "Desconocido"
        };
    }
}
```

---

## 📊 SQL Útiles para Queries Comunes

```sql
-- Vacaciones por empleado y mes
SELECT 
    e.EmployeeName,
    MONTH(v.StartDate) AS Mes,
    YEAR(v.StartDate) AS Año,
    SUM(v.DaysRequested) AS DíasTotales,
    COUNT(*) AS Solicitudes
FROM dbo.VacationRequests v
INNER JOIN dbo.Employees e ON e.UserId = v.UserId
WHERE v.[Status] = 1 -- Aprobadas
GROUP BY e.EmployeeName, YEAR(v.StartDate), MONTH(v.StartDate)
ORDER BY Año DESC, Mes DESC;

-- Solicitudes pendientes
SELECT 
    v.VacationId,
    e.EmployeeName,
    v.StartDate,
    v.EndDate,
    v.DaysRequested,
    v.Notes,
    v.CreatedAt
FROM dbo.VacationRequests v
INNER JOIN dbo.Employees e ON e.UserId = v.UserId
WHERE v.[Status] = 0 -- Pendientes
ORDER BY v.CreatedAt;

-- Disponibilidad de vacaciones por empleado
SELECT 
    e.UserId,
    e.EmployeeName,
    15 - COALESCE(SUM(CASE WHEN v.[Status] = 1 THEN v.DaysRequested ELSE 0 END), 0) AS DíasDisponibles,
    COALESCE(SUM(CASE WHEN v.[Status] = 1 THEN v.DaysRequested ELSE 0 END), 0) AS DíasUsados
FROM dbo.Employees e
LEFT JOIN dbo.VacationRequests v ON e.UserId = v.UserId
    AND YEAR(v.StartDate) = YEAR(GETDATE())
GROUP BY e.UserId, e.EmployeeName
ORDER BY e.EmployeeName;
```

---

## 🎯 Checklist de Implementación

- [ ] Crear `VacationRequest.cs` en Domain/Models
- [ ] Crear `IVacationRepository.cs` en Domain/Interfaces
- [ ] Agregar tabla `VacationRequests` en `SqlDeviceSyncService.cs`
- [ ] Crear `SqlVacationRepository.cs` en Infrastructure.Access
- [ ] Crear `RhVacationService.cs` en Application
- [ ] Registrar dependencias en Program.cs
- [ ] Crear formulario `RhVacationForm.cs` en Desktop
- [ ] Probar solicitud → Aprobación → Consulta en la UI
- [ ] Verificar datos en BD (SELECT * FROM dbo.VacationRequests)

---

## 🚀 Próxima Extensión

Mismo patrón funciona para:
- **Nómina** (tabla: PayrollRecords)
- **Permisos** (tabla: PermissionRequests)
- **Evaluaciones** (tabla: EmployeeEvaluations)
- **Capacitaciones** (tabla: TrainingRecords)

Copia-adapta este flujo para cada módulo.
