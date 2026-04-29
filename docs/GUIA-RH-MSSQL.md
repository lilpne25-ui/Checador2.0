# Guía: Integrar Módulo RH con MSSQL en Checador 2.0

## 🎯 Resumen Ejecutivo

Tu app actual usa un patrón de **arquitectura limpia** con capas:
- **Domain** (Modelos + Interfaces)
- **Infrastructure.Access** (Acceso a datos MSSQL)
- **Application** (Lógica de negocio)
- **Desktop** (UI WinForms)

Para agregar RH, solo extiende este patrón SIN modificar lo existente. Las tablas RH vivirán en la misma BD MSSQL con el mismo sistema de bootstrap que ya tienes.

---

## 📋 Arquitectura Actual (Cómo Funciona Hoy)

### 1. Tabla `Employees` en MSSQL
Ya existe estructura base:
```sql
UserId (PK)          -- Clave única
EmployeeName
Department           -- ✅ YA existe
Position            -- ✅ YA existe
WorkScheduleCode
OvertimeAuthorized
StandardShiftHours
UpdatedAt
```

**Nota:** Tu tabla `Employees` ya tiene `Department` y `Position`, así que tienes una base RH embrionaria.

### 2. Patrón de Acceso a Datos (Cómo Conectas a BD)

En `SqlDeviceSyncService.cs` y `SqlAttendanceDataSource.cs` ves este flujo:

```
[Desktop UI] 
    ↓
[Application Service] (ej: ReportGeneratorService)
    ↓
[Infrastructure.Access] (ej: SqlAttendanceDataSource)
    ↓
[SqlConnection → SQL Server]
    ↓
[MSSQL Tablas: Employees, PunchLogs, etc]
```

**Patrón de código actual:**
```csharp
// 1. Inyectar opciones de SQL
private readonly SqlSyncOptions _options;

// 2. Crear conexión async
await using var connection = new SqlConnection(_options.ConnectionString);
await connection.OpenAsync(cancellationToken);

// 3. Ejecutar query con parámetros
const string sql = "SELECT * FROM dbo.Employees WHERE UserId = @UserId";
await using var cmd = new SqlCommand(sql, connection);
cmd.Parameters.AddWithValue("@UserId", userId);

// 4. Leer resultados
await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
while (await reader.ReadAsync(cancellationToken))
{
    result.Add(new EmployeeInfo(...));
}
```

---

## 🏗️ Cómo Extender para RH

### Paso 1: Crear Modelos RH en `Domain/Models`

Ejemplo estructura para **Vacaciones**:
```csharp
// Checador.Domain/Models/VacationRequest.cs
public sealed record VacationRequest(
    int VacationId,
    int UserId,
    DateTime StartDate,
    DateTime EndDate,
    int DaysRequested,
    VacationStatus Status,          // enum: Pending, Approved, Rejected
    string? Notes,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    int? ApprovedBy
);

public enum VacationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}
```

**Para Nómina:**
```csharp
public sealed record PayrollRecord(
    int PayrollId,
    int UserId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal BaseSalary,
    decimal Bonuses,
    decimal Deductions,
    decimal NetSalary,
    PayrollStatus Status,
    DateTime CreatedAt
);

public enum PayrollStatus
{
    Draft = 0,
    Approved = 1,
    Paid = 2
}
```

### Paso 2: Crear Interfaces en `Domain/Interfaces`

```csharp
// Checador.Domain/Interfaces/IVacationRepository.cs
public interface IVacationRepository
{
    Task<VacationRequest?> GetByIdAsync(int vacationId, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<VacationRequest>> GetByUserAsync(int userId, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<VacationRequest>> GetByStatusAsync(
        VacationStatus status,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);
    
    Task InsertAsync(VacationRequest request, CancellationToken cancellationToken = default);
    
    Task UpdateAsync(VacationRequest request, CancellationToken cancellationToken = default);
    
    Task DeleteAsync(int vacationId, CancellationToken cancellationToken = default);
}
```

### Paso 3: Crear Tablas en MSSQL (Bootstrap)

En `SqlDeviceSyncService.cs`, en el método `EnsureSchemaAsync()`, agregarías:

```sql
IF OBJECT_ID('dbo.VacationRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.VacationRequests (
        VacationId INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        DaysRequested INT NOT NULL,
        [Status] INT NOT NULL CONSTRAINT DF_VacationRequests_Status DEFAULT (0),
        Notes NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VacationRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
        ApprovedAt DATETIME2 NULL,
        ApprovedBy INT NULL,
        FOREIGN KEY (UserId) REFERENCES dbo.Employees(UserId),
        FOREIGN KEY (ApprovedBy) REFERENCES dbo.Employees(UserId)
    );
    
    CREATE INDEX IX_VacationRequests_UserId ON dbo.VacationRequests(UserId);
    CREATE INDEX IX_VacationRequests_Status ON dbo.VacationRequests([Status]);
END;
```

**Nota:** El patrón que tienes ya cubre esto. Ve a `SqlDeviceSyncService.cs` línea ~500+ donde tiene:
```csharp
const string createEmployeesTable = @"
    IF OBJECT_ID('dbo.Employees', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Employees (...)
    END;
```

Simplemente agregas más bloques `IF OBJECT_ID...` en la misma lógica.

### Paso 4: Crear Repositorio en `Infrastructure.Access`

```csharp
// Checador.Infrastructure.Access/SqlVacationRepository.cs
using Checador.Domain.Interfaces;
using Checador.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Checador.Infrastructure.Access;

public sealed class SqlVacationRepository : IVacationRepository
{
    private readonly SqlSyncOptions _options;
    
    public SqlVacationRepository(SqlSyncOptions options)
    {
        _options = options;
    }
    
    public async Task<VacationRequest?> GetByIdAsync(int vacationId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        
        const string sql = @"
        SELECT VacationId, UserId, StartDate, EndDate, DaysRequested, [Status], Notes, 
               CreatedAt, ApprovedAt, ApprovedBy
        FROM dbo.VacationRequests
        WHERE VacationId = @VacationId";
        
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@VacationId", vacationId);
        
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new VacationRequest(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetDateTime(2),
                reader.GetDateTime(3),
                reader.GetInt32(4),
                (VacationStatus)reader.GetInt32(5),
                await reader.IsDBNullAsync(6, cancellationToken) ? null : reader.GetString(6),
                reader.GetDateTime(7),
                await reader.IsDBNullAsync(8, cancellationToken) ? null : reader.GetDateTime(8),
                await reader.IsDBNullAsync(9, cancellationToken) ? null : reader.GetInt32(9)
            );
        }
        
        return null;
    }
    
    public async Task InsertAsync(VacationRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        
        const string sql = @"
        INSERT INTO dbo.VacationRequests 
        (UserId, StartDate, EndDate, DaysRequested, [Status], Notes, CreatedAt)
        VALUES (@UserId, @StartDate, @EndDate, @DaysRequested, @Status, @Notes, @CreatedAt)";
        
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", request.UserId);
        cmd.Parameters.AddWithValue("@StartDate", request.StartDate);
        cmd.Parameters.AddWithValue("@EndDate", request.EndDate);
        cmd.Parameters.AddWithValue("@DaysRequested", request.DaysRequested);
        cmd.Parameters.AddWithValue("@Status", (int)request.Status);
        cmd.Parameters.AddWithValue("@Notes", request.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
    
    // ... más métodos similares (UpdateAsync, DeleteAsync, etc)
}
```

### Paso 5: Crear Servicio de Lógica RH en `Application`

```csharp
// Checador.Application/RhVacationService.cs
using Checador.Domain.Interfaces;
using Checador.Domain.Models;

namespace Checador.Application;

public sealed class RhVacationService
{
    private readonly IVacationRepository _vacationRepo;
    
    public RhVacationService(IVacationRepository vacationRepo)
    {
        _vacationRepo = vacationRepo;
    }
    
    /// <summary>
    /// Solicitar vacaciones. Calcula automáticamente días entre fechas.
    /// </summary>
    public async Task RequestVacationAsync(
        int userId,
        DateTime startDate,
        DateTime endDate,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            throw new InvalidOperationException("Fecha inicial no puede ser mayor a fecha final.");
        
        var daysRequested = (endDate.Date - startDate.Date).Days + 1;
        
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
        
        await _vacationRepo.InsertAsync(request, cancellationToken);
    }
    
    /// <summary>
    /// Aprobar solicitud de vacaciones (solo RH).
    /// </summary>
    public async Task ApproveVacationAsync(
        int vacationId,
        int approvedByUserId,
        CancellationToken cancellationToken = default)
    {
        var vacation = await _vacationRepo.GetByIdAsync(vacationId, cancellationToken);
        if (vacation is null)
            throw new InvalidOperationException("Solicitud de vacaciones no encontrada.");
        
        var approved = vacation with
        {
            Status = VacationStatus.Approved,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = approvedByUserId
        };
        
        await _vacationRepo.UpdateAsync(approved, cancellationToken);
    }
    
    /// <summary>
    /// Obtener historial de vacaciones de un empleado.
    /// </summary>
    public async Task<IReadOnlyList<VacationRequest>> GetEmployeeVacationHistoryAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _vacationRepo.GetByUserAsync(userId, cancellationToken);
    }
}
```

### Paso 6: Inyectar Dependencias en Startup

En tu `Program.cs` o donde registres servicios:

```csharp
// En Desktop o Console ConfigureServices
services.AddSingleton<SqlSyncOptions>(options);
services.AddSingleton<SqlVacationRepository>();
services.AddSingleton<IVacationRepository>(sp => sp.GetRequiredService<SqlVacationRepository>());
services.AddSingleton<RhVacationService>();
```

### Paso 7: Usar en UI (WinForms)

```csharp
// En tu formulario RH
private readonly RhVacationService _vacationService;

public RhVacationForm(RhVacationService vacationService)
{
    _vacationService = vacationService;
}

private async void btnRequestVacation_Click(object sender, EventArgs e)
{
    try
    {
        await _vacationService.RequestVacationAsync(
            userId: int.Parse(txtUserId.Text),
            startDate: dateStartDate.Value,
            endDate: dateEndDate.Value,
            notes: txtNotes.Text,
            cancellationToken: CancellationToken.None);
        
        MessageBox.Show("Solicitud registrada exitosamente.");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}");
    }
}
```

---

## 📊 Estructura de Tablas RH Recomendadas

### 1. **Vacaciones** (Ya mostrada arriba)
Campos clave: `StartDate`, `EndDate`, `DaysRequested`, `Status`

### 2. **Nómina**
```sql
CREATE TABLE dbo.Payroll (
    PayrollId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    PeriodStart DATE NOT NULL,
    PeriodEnd DATE NOT NULL,
    BaseSalary DECIMAL(10,2) NOT NULL,
    Bonuses DECIMAL(10,2) DEFAULT 0,
    Deductions DECIMAL(10,2) DEFAULT 0,
    NetSalary AS (BaseSalary + Bonuses - Deductions), -- Computed column
    [Status] INT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (UserId) REFERENCES dbo.Employees(UserId)
);
```

### 3. **Licencias/Permisos**
```sql
CREATE TABLE dbo.Permissions (
    PermissionId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    PermissionDate DATE NOT NULL,
    HoursRequested INT NOT NULL,
    Reason NVARCHAR(500),
    ApprovedBy INT,
    [Status] INT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (UserId) REFERENCES dbo.Employees(UserId),
    FOREIGN KEY (ApprovedBy) REFERENCES dbo.Employees(UserId)
);
```

### 4. **Evaluaciones**
```sql
CREATE TABLE dbo.Evaluations (
    EvaluationId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    EvaluationDate DATE NOT NULL,
    Score DECIMAL(5,2),  -- 0-100
    Evaluator INT,
    Comments NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (UserId) REFERENCES dbo.Employees(UserId),
    FOREIGN KEY (Evaluator) REFERENCES dbo.Employees(UserId)
);
```

---

## 🔄 Patrón de Flujo (CRUD Genérico)

**Para CUALQUIER tabla RH, el flujo es siempre:**

```
1. Modelo (Domain/Models) ← Definir estructura
   ↓
2. Interfaz (Domain/Interfaces) ← Contrato de datos
   ↓
3. Tabla SQL (SqlDeviceSyncService.EnsureSchemaAsync) ← Bootstrap
   ↓
4. Repositorio (Infrastructure.Access) ← Acceso a datos
   ↓
5. Servicio (Application) ← Lógica de negocio
   ↓
6. UI (Desktop) ← Formularios WinForms
```

---

## 💡 Ventajas de Este Enfoque

✅ **Mantienes aislado lo RH de lo de asistencia** - Sin toquetear código actual  
✅ **Reutilizas infraestructura** - Misma BD, misma conexión, misma inyección  
✅ **Testeable** - Interfaces en Domain permiten mocks  
✅ **Escalable** - Agregar tabla nueva = copiar/adaptar patrón  
✅ **Seguro** - Parámetros SqlCommand evitan SQL injection  

---

## 🚀 Próximos Pasos

1. **Define qué módulos RH necesitas exactamente** (vacaciones, nómina, evaluaciones, etc)
2. **Crea los records/enums** en `Domain/Models`
3. **Crea las interfaces** en `Domain/Interfaces`
4. **Diseña las tablas SQL** (agrégalas a `EnsureSchemaAsync`)
5. **Implementa repositorios** con patrón identical al de `SqlAttendanceDataSource`
6. **Crea servicios de lógica** en `Application`
7. **Agrega formularios WinForms** en `Desktop`

**No necesitas crear API REST ni cambiar MSSQL de máquina.** Todo corre localmente con tu infraestructura actual.
