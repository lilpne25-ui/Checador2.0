# Patrones Avanzados: RH en Checador 2.0

## 🔒 1. TRANSACCIONES Y CONSISTENCIA

Cuando necesitas múltiples operaciones que deben ejecutarse juntas:

### Ejemplo: Aprobar Vacación + Registrar Auditoría

```csharp
// En Infrastructure.Access - Crear nuevo repositorio
public sealed class SqlVacationWithAuditRepository : IVacationRepository
{
    private readonly SqlSyncOptions _options;

    public async Task ApproveVacationWithAuditAsync(
        int vacationId,
        int approvedByUserId,
        string? auditReason,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Iniciar transacción
        await using var transaction = connection.BeginTransaction();
        try
        {
            // 1. Actualizar vacación
            const string updateVacation = @"
UPDATE dbo.VacationRequests
SET [Status] = 1, ApprovedAt = @ApprovedAt, ApprovedBy = @ApprovedBy
WHERE VacationId = @VacationId";

            await using var cmd1 = new SqlCommand(updateVacation, connection, transaction);
            cmd1.Parameters.AddWithValue("@VacationId", vacationId);
            cmd1.Parameters.AddWithValue("@ApprovedAt", DateTime.UtcNow);
            cmd1.Parameters.AddWithValue("@ApprovedBy", approvedByUserId);
            await cmd1.ExecuteNonQueryAsync(cancellationToken);

            // 2. Registrar auditoría
            const string insertAudit = @"
INSERT INTO dbo.AuditLog (TableName, RecordId, Action, OldValue, NewValue, ChangedBy, ChangedAt)
VALUES ('VacationRequests', @RecordId, 'APPROVED', NULL, '1', @ChangedBy, @ChangedAt)";

            await using var cmd2 = new SqlCommand(insertAudit, connection, transaction);
            cmd2.Parameters.AddWithValue("@RecordId", vacationId);
            cmd2.Parameters.AddWithValue("@ChangedBy", approvedByUserId);
            cmd2.Parameters.AddWithValue("@ChangedAt", DateTime.UtcNow);
            await cmd2.ExecuteNonQueryAsync(cancellationToken);

            // Confirmar todo
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

### Tabla de Auditoría (Bootstrap)

```sql
IF OBJECT_ID('dbo.AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog (
        AuditId INT IDENTITY(1,1) PRIMARY KEY,
        TableName NVARCHAR(100) NOT NULL,
        RecordId INT NOT NULL,
        [Action] NVARCHAR(50) NOT NULL,
        OldValue NVARCHAR(MAX) NULL,
        NewValue NVARCHAR(MAX) NULL,
        ChangedBy INT,
        ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_AuditLog_ChangedAt DEFAULT SYSUTCDATETIME(),
        FOREIGN KEY (ChangedBy) REFERENCES dbo.Employees(UserId)
    );
    
    CREATE INDEX IX_AuditLog_TableRecord ON dbo.AuditLog(TableName, RecordId);
END;
```

---

## 🔐 2. VALIDACIONES COMPLEJAS (Domain Layer)

### Crear servicio de validaciones reutilizable

```csharp
// Checador.Domain/Services/VacationValidationService.cs
namespace Checador.Domain.Services;

public sealed class VacationValidationService
{
    private const int MaxVacationDaysPerYear = 15;
    private const int MinDaysInAdvance = 5;
    private const int MaxConsecutiveDays = 10;

    private readonly IVacationRepository _repo;
    private readonly IEmployeeRepository _empRepo;

    public VacationValidationService(
        IVacationRepository vacationRepo,
        IEmployeeRepository employeeRepo)
    {
        _repo = vacationRepo;
        _empRepo = employeeRepo;
    }

    /// <summary>
    /// Validación completa de solicitud de vacaciones.
    /// Retorna lista de errores (vacío = válido).
    /// </summary>
    public async Task<List<string>> ValidateVacationRequestAsync(
        int userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // 1. Validar fechas
        if (startDate > endDate)
            errors.Add("Fecha inicio no puede ser posterior a fecha fin.");

        if (startDate.DayOfWeek == DayOfWeek.Saturday || startDate.DayOfWeek == DayOfWeek.Sunday)
            errors.Add("No se pueden iniciar vacaciones en fin de semana.");

        // 2. Validar anticipación
        if (startDate.Date <= DateTime.Now.Date.AddDays(MinDaysInAdvance))
            errors.Add($"Vacaciones deben solicitarse con {MinDaysInAdvance}+ días de anticipación.");

        // 3. Validar duración
        var days = (endDate.Date - startDate.Date).Days + 1;
        if (days > MaxConsecutiveDays)
            errors.Add($"Máximo {MaxConsecutiveDays} días consecutivos. Solicitaste {days}.");

        // 4. Validar disponibilidad anual
        var usedThisYear = await _repo.CountUsedDaysAsync(
            userId,
            new DateTime(DateTime.Now.Year, 1, 1),
            new DateTime(DateTime.Now.Year, 12, 31),
            cancellationToken);

        if (usedThisYear + days > MaxVacationDaysPerYear)
            errors.Add($"Excede límite anual. Disponibles: {MaxVacationDaysPerYear - usedThisYear} días.");

        // 5. Validar si empleado existe y está activo
        var employee = await _empRepo.GetByIdAsync(userId, cancellationToken);
        if (employee is null)
            errors.Add("Empleado no encontrado.");
        else if (!employee.IsActive)
            errors.Add("Empleado inactivo.");

        // 6. Validar solapamiento con otra solicitud aprobada
        var overlapping = await _repo.GetByStatusAsync(
            VacationStatus.Approved,
            startDate.AddDays(-1),
            endDate.AddDays(1),
            cancellationToken);

        if (overlapping.Any(v => v.UserId == userId))
            errors.Add("Ya existe vacaciones aprobadas en este rango.");

        return errors;
    }

    /// <summary>
    /// Validar si se puede rechazar una solicitud.
    /// </summary>
    public async Task<List<string>> ValidateRejectVacationAsync(
        int vacationId,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var vacation = await _repo.GetByIdAsync(vacationId, cancellationToken);
        if (vacation is null)
        {
            errors.Add("Solicitud no encontrada.");
            return errors;
        }

        if (vacation.Status != VacationStatus.Pending)
            errors.Add("Solo se pueden rechazar solicitudes pendientes.");

        // No se puede rechazar si ya pasó la fecha de inicio
        if (vacation.StartDate.Date <= DateTime.Now.Date)
            errors.Add("No se pueden rechazar vacaciones que ya iniciaron.");

        return errors;
    }
}
```

---

## 📧 3. NOTIFICACIONES Y EVENTOS

### Sistema de eventos para avisar cambios

```csharp
// Checador.Domain/Events/VacationApprovedEvent.cs
namespace Checador.Domain.Events;

public sealed record VacationApprovedEvent(
    int VacationId,
    int UserId,
    DateTime StartDate,
    DateTime EndDate,
    int ApprovedBy,
    DateTime ApprovedAt
);

// Checador.Domain/Interfaces/IEventPublisher.cs
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
```

### Servicio que emite eventos

```csharp
// En RhVacationService
public sealed class RhVacationService
{
    private readonly IVacationRepository _repo;
    private readonly IEventPublisher _eventPublisher;

    public RhVacationService(
        IVacationRepository repo,
        IEventPublisher eventPublisher)
    {
        _repo = repo;
        _eventPublisher = eventPublisher;
    }

    public async Task ApproveVacationAsync(
        int vacationId,
        int approvedByUserId,
        CancellationToken cancellationToken = default)
    {
        var vacation = await _repo.GetByIdAsync(vacationId, cancellationToken)
            ?? throw new InvalidOperationException("Solicitud no encontrada.");

        var approved = vacation with
        {
            Status = VacationStatus.Approved,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = approvedByUserId
        };

        await _repo.UpdateAsync(approved, cancellationToken);

        // Emitir evento para que otros módulos se enteren
        await _eventPublisher.PublishAsync(
            new VacationApprovedEvent(
                vacationId,
                vacation.UserId,
                vacation.StartDate,
                vacation.EndDate,
                approvedByUserId,
                approved.ApprovedAt!.Value),
            cancellationToken);
    }
}
```

### Manejador de eventos

```csharp
// Checador.Application/EventHandlers/VacationApprovedEventHandler.cs
public sealed class VacationApprovedEventHandler
{
    private readonly IEmailService _emailService;
    private readonly IEmployeeRepository _empRepo;

    public async Task HandleAsync(
        VacationApprovedEvent @event,
        CancellationToken cancellationToken = default)
    {
        // Obtener datos del empleado
        var employee = await _empRepo.GetByIdAsync(@event.UserId, cancellationToken);
        if (employee?.Email is null)
            return;

        // Enviar email
        var message = $@"
Hola {employee.Name},

Tu solicitud de vacaciones ha sido aprobada.

Fechas: {@event.StartDate:yyyy-MM-dd} a {@event.EndDate:yyyy-MM-dd}
Aprobado por: {(await _empRepo.GetByIdAsync(@event.ApprovedBy, cancellationToken))?.Name}
Fecha aprobación: {@event.ApprovedAt:yyyy-MM-dd HH:mm}

Gracias.
";

        await _emailService.SendAsync(
            employee.Email,
            "Vacaciones Aprobadas",
            message,
            cancellationToken);
    }
}
```

---

## 📊 4. REPORTES AVANZADOS

### Generador de reportes de RH

```csharp
// Checador.Application/Reports/VacationReportGenerator.cs
public sealed class VacationReportGenerator
{
    private readonly IVacationRepository _vacationRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public async Task<VacationReportData> GenerateAnnualReportAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        var fromDate = new DateTime(year, 1, 1);
        var toDate = new DateTime(year, 12, 31);

        var approvedVacations = await _vacationRepo.GetByStatusAsync(
            VacationStatus.Approved,
            fromDate,
            toDate,
            cancellationToken);

        var employees = await _employeeRepo.GetAllAsync(cancellationToken);

        var reportRows = employees
            .GroupJoin(
                approvedVacations,
                e => e.UserId,
                v => v.UserId,
                (emp, vacs) => new VacationReportRow(
                    EmployeeId: emp.UserId,
                    EmployeeName: emp.Name,
                    Department: emp.Department,
                    TotalDaysUsed: vacs.Sum(v => v.DaysRequested),
                    VacationCount: vacs.Count(),
                    FirstVacation: vacs.OrderBy(v => v.StartDate).FirstOrDefault()?.StartDate,
                    LastVacation: vacs.OrderByDescending(v => v.EndDate).FirstOrDefault()?.EndDate,
                    AverageDaysPerVacation: vacs.Count > 0 ? (decimal)vacs.Sum(v => v.DaysRequested) / vacs.Count() : 0
                ))
            .OrderBy(r => r.EmployeeName)
            .ToList();

        return new VacationReportData(year, reportRows);
    }
}

public sealed record VacationReportRow(
    int EmployeeId,
    string EmployeeName,
    string? Department,
    int TotalDaysUsed,
    int VacationCount,
    DateTime? FirstVacation,
    DateTime? LastVacation,
    decimal AverageDaysPerVacation
);

public sealed record VacationReportData(
    int Year,
    List<VacationReportRow> Rows
);
```

### Exportar a Excel usando ClosedXML

```csharp
// En Infrastructure.Excel
public sealed class VacationReportExcelGenerator
{
    public async Task<byte[]> GenerateExcelAsync(
        VacationReportData report,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Vacaciones");

        // Headers
        worksheet.Cell(1, 1).Value = "ID";
        worksheet.Cell(1, 2).Value = "Empleado";
        worksheet.Cell(1, 3).Value = "Departamento";
        worksheet.Cell(1, 4).Value = "Días Usados";
        worksheet.Cell(1, 5).Value = "# Solicitudes";
        worksheet.Cell(1, 6).Value = "Primera Vacación";
        worksheet.Cell(1, 7).Value = "Última Vacación";
        worksheet.Cell(1, 8).Value = "Promedio/Vacación";

        // Hacer headers negrita
        worksheet.Range(1, 1, 1, 8).Style.Font.Bold = true;

        // Data
        int row = 2;
        foreach (var item in report.Rows)
        {
            worksheet.Cell(row, 1).Value = item.EmployeeId;
            worksheet.Cell(row, 2).Value = item.EmployeeName;
            worksheet.Cell(row, 3).Value = item.Department;
            worksheet.Cell(row, 4).Value = item.TotalDaysUsed;
            worksheet.Cell(row, 5).Value = item.VacationCount;
            worksheet.Cell(row, 6).Value = item.FirstVacation?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 7).Value = item.LastVacation?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 8).Value = Math.Round(item.AverageDaysPerVacation, 2);

            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Guardar a memoria
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
```

---

## 🔍 5. CACHÉ Y OPTIMIZACIÓN

### Caché de datos que cambian poco

```csharp
// Checador.Application/Caching/EmployeeCacheService.cs
public sealed class EmployeeCacheService
{
    private readonly IEmployeeRepository _repository;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "Employees_All";
    private const int CacheDurationMinutes = 30;

    public EmployeeCacheService(
        IEmployeeRepository repository,
        IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<Employee>? cachedEmployees))
        {
            return cachedEmployees!;
        }

        var employees = await _repository.GetAllAsync(cancellationToken);
        _cache.Set(CacheKey, employees, TimeSpan.FromMinutes(CacheDurationMinutes));
        return employees;
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
    }
}
```

---

## 🛡️ 6. SEGURIDAD Y PERMISOS

### Servicio de autorización para RH

```csharp
// Checador.Domain/Services/RhAuthorizationService.cs
public sealed class RhAuthorizationService
{
    private readonly IEmployeeRepository _empRepo;

    public enum RhRole
    {
        Employee = 0,
        Manager = 1,
        HrStaff = 2,
        HrDirector = 3
    }

    public async Task<bool> CanApproveVacationAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _empRepo.GetByIdAsync(userId, cancellationToken);
        if (employee is null)
            return false;

        // RhStaff y HrDirector pueden aprobar
        return employee.RhRole is >= (int)RhRole.HrStaff;
    }

    public async Task<bool> CanViewVacationAsync(
        int requestingUserId,
        int targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (requestingUserId == targetUserId)
            return true; // Cada uno ve sus propias vacaciones

        var requesting = await _empRepo.GetByIdAsync(requestingUserId, cancellationToken);
        if (requesting?.RhRole >= (int)RhRole.Manager)
            return true; // Managers y RH ven todas

        return false;
    }
}
```

### Usar en servicio

```csharp
public async Task ApproveVacationAsync(
    int vacationId,
    int approvedByUserId,
    CancellationToken cancellationToken = default)
{
    // Validar permiso
    if (!await _authService.CanApproveVacationAsync(approvedByUserId, cancellationToken))
        throw new UnauthorizedAccessException("No tienes permisos para aprobar vacaciones.");

    // ... resto de lógica
}
```

---

## 🔄 7. TESTING CON MOCKS

### Test unitario de servicio de vacaciones

```csharp
// Checador.Tests/Application/RhVacationServiceTests.cs
using Xunit;
using Moq;

public class RhVacationServiceTests
{
    private readonly Mock<IVacationRepository> _mockRepo;
    private readonly RhVacationService _service;

    public RhVacationServiceTests()
    {
        _mockRepo = new Mock<IVacationRepository>();
        _service = new RhVacationService(_mockRepo.Object);
    }

    [Fact]
    public async Task RequestVacation_ValidDates_ShouldInsert()
    {
        // Arrange
        int userId = 1;
        var startDate = DateTime.Now.AddDays(10);
        var endDate = DateTime.Now.AddDays(15);

        _mockRepo
            .Setup(x => x.CountUsedDaysAsync(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.RequestVacationAsync(userId, startDate, endDate, "Test", CancellationToken.None);

        // Assert
        Assert.NotEqual(0, result);
        _mockRepo.Verify(x => x.InsertAsync(It.IsAny<VacationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestVacation_InsufficientAdvance_ShouldThrow()
    {
        // Arrange
        int userId = 1;
        var startDate = DateTime.Now.AddDays(2); // Solo 2 días de anticipación (insuficiente)
        var endDate = DateTime.Now.AddDays(5);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RequestVacationAsync(userId, startDate, endDate, "Test", CancellationToken.None));
    }
}
```

---

## 📝 Resumen de Patrones

| Patrón | Cuándo Usar | Beneficio |
|--------|------------|-----------|
| **Transacciones** | Múltiples cambios relacionados | Consistencia garantizada |
| **Validaciones en Domain** | Lógica compleja | Reutilizable, testeable |
| **Eventos** | Notificar otros módulos | Desacoplamiento |
| **Reportes** | Análisis de datos | Insights de negocio |
| **Caché** | Datos que no cambian | Rendimiento |
| **Permisos** | Control de acceso | Seguridad |
| **Tests** | Asegurar calidad | Confianza en código |

---

Estos patrones mantienen tu código profesional, escalable y fácil de mantener.
