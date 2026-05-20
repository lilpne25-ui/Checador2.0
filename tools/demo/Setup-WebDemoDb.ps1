param(
    [string]$SqlServer = ".",
    [string]$Database = "ZKBD_DEMO_WEB",
    [int]$EmployeeCount = 20,
    [int]$LookbackDays = 365,
    [string]$SupervisorUser = "SUPERVSR",
    [string]$SupervisorPassword = "SUPERVSR",
    [string]$AdminUser = "admin1",
    [string]$AdminPassword = "admin1",
    [string]$ConsultaUser = "consulta1",
    [string]$ConsultaPassword = "consulta1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($EmployeeCount -lt 5 -or $EmployeeCount -gt 500) {
    throw "EmployeeCount fuera de rango. Usa un valor entre 5 y 500."
}

if ($LookbackDays -lt 30 -or $LookbackDays -gt 2000) {
    throw "LookbackDays fuera de rango. Usa un valor entre 30 y 2000."
}

function Invoke-SqlNonQuery {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$Sql,
        [hashtable]$Parameters
    )

    $conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandTimeout = 180
        $cmd.CommandText = $Sql
        if ($Parameters) {
            foreach ($key in $Parameters.Keys) {
                [void]$cmd.Parameters.AddWithValue($key, $Parameters[$key])
            }
        }
        [void]$cmd.ExecuteNonQuery()
    }
    finally {
        $conn.Dispose()
    }
}

$masterConn = "Server=$SqlServer;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;"
$demoConn = "Server=$SqlServer;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;"

$dbNameSql = $Database.Replace("'", "''")
$dbNameEscaped = $Database.Replace("]", "]]")
$createDbSql = @"
IF DB_ID(N'$dbNameSql') IS NULL
BEGIN
    EXEC('CREATE DATABASE [$dbNameEscaped]');
END;
"@
Invoke-SqlNonQuery -ConnectionString $masterConn -Sql $createDbSql

$schemaSql = @"
SET NOCOUNT ON;

IF OBJECT_ID('dbo.Employees', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees (
        UserId INT NOT NULL PRIMARY KEY,
        EmployeeName NVARCHAR(200) NOT NULL,
        Department NVARCHAR(150) NULL,
        [Position] NVARCHAR(150) NULL,
        PervasiveDept NVARCHAR(30) NULL,
        PervasiveShift NVARCHAR(30) NULL,
        WorkScheduleCode INT NULL,
        OvertimeAuthorized BIT NOT NULL CONSTRAINT DF_Employees_OvertimeAuthorized DEFAULT (0),
        StandardShiftHours DECIMAL(5,2) NOT NULL CONSTRAINT DF_Employees_StandardShiftHours DEFAULT (9.60),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_Employees_UpdatedAt DEFAULT SYSUTCDATETIME(),
        Photo VARBINARY(MAX) NULL,
        PhotoContentType NVARCHAR(100) NULL,
        PhotoUpdatedAt DATETIME2 NULL
    );
END;

IF OBJECT_ID('dbo.PunchLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PunchLogs (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NOT NULL,
        EmployeeName NVARCHAR(200) NOT NULL,
        CheckTime DATETIME2 NOT NULL,
        InOutMode INT NULL,
        Source NVARCHAR(40) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PunchLogs_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_PunchLogs_User_Time ON dbo.PunchLogs(UserId, CheckTime);
    CREATE INDEX IX_PunchLogs_Time ON dbo.PunchLogs(CheckTime DESC);
END;

IF OBJECT_ID('dbo.SyncState', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SyncState (
        Id INT NOT NULL PRIMARY KEY,
        LastSync DATETIME2 NULL,
        LastRun DATETIME2 NULL,
        LastStatus NVARCHAR(30) NOT NULL,
        LastMessage NVARCHAR(2000) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SyncState WHERE Id = 1)
BEGIN
    INSERT INTO dbo.SyncState(Id, LastSync, LastRun, LastStatus, LastMessage)
    VALUES (1, NULL, NULL, 'N/A', 'Sin sincronizacion ejecutada.');
END;

IF OBJECT_ID('dbo.WorkSchedules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkSchedules (
        Code INT NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL,
        StartTime TIME(0) NOT NULL,
        EndTime TIME(0) NOT NULL,
        StandardHours DECIMAL(5,2) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_WorkSchedules_IsActive DEFAULT (1),
        LateToleranceMinutes INT NOT NULL CONSTRAINT DF_WorkSchedules_LateToleranceMinutes DEFAULT (10),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WorkSchedules_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF COL_LENGTH('dbo.WorkSchedules', 'LateToleranceMinutes') IS NULL
BEGIN
    ALTER TABLE dbo.WorkSchedules ADD LateToleranceMinutes INT NOT NULL CONSTRAINT DF_WorkSchedules_LateToleranceMinutes_Default DEFAULT (10);
END;

IF OBJECT_ID('dbo.EmployeeReassignMap', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeReassignMap (
        OldUserId INT NOT NULL PRIMARY KEY,
        NewUserId INT NOT NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_EmployeeReassignMap_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID('dbo.EmployeeSuppression', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeSuppression (
        UserId INT NOT NULL PRIMARY KEY,
        Reason NVARCHAR(100) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_EmployeeSuppression_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID('dbo.ApiUsers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiUsers
    (
        UserId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApiUsers PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL,
        PasswordSalt VARBINARY(16) NOT NULL,
        PasswordHash VARBINARY(32) NOT NULL,
        MustChangePassword BIT NOT NULL CONSTRAINT DF_ApiUsers_MustChangePassword DEFAULT (1),
        IsActive BIT NOT NULL CONSTRAINT DF_ApiUsers_IsActive DEFAULT (1),
        LastLoginAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApiUsers_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApiUsers_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_ApiUsers_Username ON dbo.ApiUsers(Username);
END;

IF OBJECT_ID('dbo.ApiRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiRoles
    (
        RoleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApiRoles PRIMARY KEY,
        RoleName NVARCHAR(50) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ApiRoles_IsActive DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApiRoles_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_ApiRoles_RoleName ON dbo.ApiRoles(RoleName);
END;

IF OBJECT_ID('dbo.ApiUserRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiUserRoles
    (
        UserId INT NOT NULL,
        RoleId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApiUserRoles_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ApiUserRoles PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_ApiUserRoles_ApiUsers FOREIGN KEY (UserId) REFERENCES dbo.ApiUsers(UserId),
        CONSTRAINT FK_ApiUserRoles_ApiRoles FOREIGN KEY (RoleId) REFERENCES dbo.ApiRoles(RoleId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ApiRoles WHERE RoleName = N'consulta')
BEGIN
    INSERT INTO dbo.ApiRoles (RoleName, IsActive) VALUES (N'consulta', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ApiRoles WHERE RoleName = N'admin')
BEGIN
    INSERT INTO dbo.ApiRoles (RoleName, IsActive) VALUES (N'admin', 1);
END;
"@
Invoke-SqlNonQuery -ConnectionString $demoConn -Sql $schemaSql

$seedScheduleSql = @"
MERGE dbo.WorkSchedules AS target
USING (
    VALUES
        (1, N'Horario 1', CAST('07:00:00' AS time), CAST('16:36:00' AS time), CAST(9.60 AS decimal(5,2)), CAST(1 AS bit), 10),
        (2, N'Horario 2', CAST('08:00:00' AS time), CAST('17:36:00' AS time), CAST(9.60 AS decimal(5,2)), CAST(1 AS bit), 10),
        (3, N'Horario 3', CAST('18:00:00' AS time), CAST('07:00:00' AS time), CAST(9.60 AS decimal(5,2)), CAST(1 AS bit), 10)
) AS src (Code, [Name], StartTime, EndTime, StandardHours, IsActive, LateToleranceMinutes)
ON target.Code = src.Code
WHEN MATCHED THEN
    UPDATE SET
        [Name] = src.[Name],
        StartTime = src.StartTime,
        EndTime = src.EndTime,
        StandardHours = src.StandardHours,
        IsActive = src.IsActive,
        LateToleranceMinutes = src.LateToleranceMinutes,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Code, [Name], StartTime, EndTime, StandardHours, IsActive, LateToleranceMinutes, UpdatedAt)
    VALUES (src.Code, src.[Name], src.StartTime, src.EndTime, src.StandardHours, src.IsActive, src.LateToleranceMinutes, SYSUTCDATETIME());
"@
Invoke-SqlNonQuery -ConnectionString $demoConn -Sql $seedScheduleSql

$today = (Get-Date).Date
$fromDate = $today.AddDays(-1 * ($LookbackDays - 1))

$seedEmployeesAndPunchesSql = @"
SET NOCOUNT ON;
SET DATEFIRST 1;

;WITH Emp AS
(
    SELECT TOP (@EmployeeCount)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS UserId
    FROM sys.all_objects
),
NameMap AS
(
    SELECT *
    FROM (VALUES
        (1,  N'Alejandro Martinez Demo'),
        (2,  N'Maria Fernanda Lopez Demo'),
        (3,  N'Jorge Alberto Ramirez Demo'),
        (4,  N'Paola Hernandez Demo'),
        (5,  N'Ricardo Gonzalez Demo'),
        (6,  N'Karla Patricia Soto Demo'),
        (7,  N'Luis Enrique Torres Demo'),
        (8,  N'Daniela Cruz Demo'),
        (9,  N'Carlos Eduardo Navarro Demo'),
        (10, N'Gabriela Vargas Demo'),
        (11, N'Miguel Angel Diaz Demo'),
        (12, N'Andrea Jimenez Demo'),
        (13, N'Fernando Aguilar Demo'),
        (14, N'Brenda Morales Demo'),
        (15, N'Rafael Castillo Demo'),
        (16, N'Sofia Mendoza Demo'),
        (17, N'Oscar Romero Demo'),
        (18, N'Valeria Guerrero Demo'),
        (19, N'Arturo Flores Demo'),
        (20, N'Natalia Pineda Demo'),
        (21, N'Pedro Salinas Demo'),
        (22, N'Erika Campos Demo'),
        (23, N'Ruben Nunez Demo'),
        (24, N'Claudia Ibarra Demo'),
        (25, N'Emilio Rojas Demo'),
        (26, N'Luz Elena Vega Demo'),
        (27, N'Gerardo Contreras Demo'),
        (28, N'Patricia Silva Demo'),
        (29, N'Israel Ortega Demo'),
        (30, N'Monica Cardenas Demo')
    ) AS n(UserId, EmployeeName)
)
MERGE dbo.Employees AS target
USING
(
    SELECT
        e.UserId,
        COALESCE(n.EmployeeName, CONCAT(N'Empleado ', RIGHT('00' + CAST(e.UserId AS varchar(4)), 2), N' Demo')) AS EmployeeName,
        CASE (e.UserId - 1) % 4
            WHEN 0 THEN N'Produccion'
            WHEN 1 THEN N'Mantenimiento'
            WHEN 2 THEN N'Logistica'
            ELSE N'Calidad'
        END AS Department,
        CASE (e.UserId - 1) % 4
            WHEN 0 THEN N'Operador'
            WHEN 1 THEN N'Tecnico'
            WHEN 2 THEN N'Analista'
            ELSE N'Supervisor'
        END AS [Position],
        CASE WHEN e.UserId % 2 = 0 THEN N'DP2' ELSE N'DP1' END AS PervasiveDept,
        CASE WHEN e.UserId % 2 = 0 THEN N'T2' ELSE N'T1' END AS PervasiveShift,
        CASE WHEN e.UserId % 2 = 0 THEN 2 ELSE 1 END AS WorkScheduleCode,
        CASE WHEN e.UserId % 4 = 0 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS OvertimeAuthorized,
        CAST(9.60 AS decimal(5,2)) AS StandardShiftHours
    FROM Emp e
    LEFT JOIN NameMap n ON n.UserId = e.UserId
) AS src
ON target.UserId = src.UserId
WHEN MATCHED THEN
    UPDATE SET
        EmployeeName = src.EmployeeName,
        Department = src.Department,
        [Position] = src.[Position],
        PervasiveDept = src.PervasiveDept,
        PervasiveShift = src.PervasiveShift,
        WorkScheduleCode = src.WorkScheduleCode,
        OvertimeAuthorized = src.OvertimeAuthorized,
        StandardShiftHours = src.StandardShiftHours,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT
    (
        UserId, EmployeeName, Department, [Position], PervasiveDept, PervasiveShift,
        WorkScheduleCode, OvertimeAuthorized, StandardShiftHours, UpdatedAt
    )
    VALUES
    (
        src.UserId, src.EmployeeName, src.Department, src.[Position], src.PervasiveDept, src.PervasiveShift,
        src.WorkScheduleCode, src.OvertimeAuthorized, src.StandardShiftHours, SYSUTCDATETIME()
    );

DELETE p
FROM dbo.PunchLogs p
WHERE p.UserId BETWEEN 1 AND @EmployeeCount
  AND p.CheckTime >= @FromDate
  AND p.CheckTime < DATEADD(DAY, 1, @ToDate);

;WITH Emp AS
(
    SELECT TOP (@EmployeeCount)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS UserId
    FROM sys.all_objects
),
Dates AS
(
    SELECT CAST(@FromDate AS date) AS WorkDate
    UNION ALL
    SELECT DATEADD(DAY, 1, WorkDate)
    FROM Dates
    WHERE WorkDate < CAST(@ToDate AS date)
),
Base AS
(
    SELECT
        e.UserId,
        emp.EmployeeName,
        d.WorkDate,
        CASE WHEN e.UserId % 2 = 0 THEN CAST('08:00:00' AS time) ELSE CAST('07:00:00' AS time) END AS BaseStart,
        CASE WHEN e.UserId % 2 = 0 THEN CAST('17:36:00' AS time) ELSE CAST('16:36:00' AS time) END AS BaseEnd,
        ABS(CHECKSUM(CONCAT(e.UserId, '-', CONVERT(varchar(10), d.WorkDate, 23), '-seed'))) % 100 AS Rnd
    FROM Emp e
    CROSS JOIN Dates d
    INNER JOIN dbo.Employees emp ON emp.UserId = e.UserId
    WHERE DATEPART(WEEKDAY, d.WorkDate) <= 5
),
Rows AS
(
    SELECT
        b.UserId,
        b.WorkDate,
        b.EmployeeName,
        b.BaseStart,
        b.BaseEnd,
        CASE WHEN b.Rnd < 8 THEN 1 ELSE 0 END AS IsAbsent,
        CASE
            WHEN b.Rnd BETWEEN 8 AND 25
                THEN 12 + (ABS(CHECKSUM(CONCAT(b.UserId, '-', CONVERT(varchar(10), b.WorkDate, 23), '-late'))) % 18)
            ELSE (ABS(CHECKSUM(CONCAT(b.UserId, '-', CONVERT(varchar(10), b.WorkDate, 23), '-jit'))) % 9) - 4
        END AS LateMinutes,
        CASE
            WHEN b.Rnd % 9 = 0 THEN 90
            WHEN b.Rnd % 5 = 0 THEN 60
            WHEN b.Rnd % 3 = 0 THEN 30
            ELSE 0
        END AS ExtraMinutes,
        CASE WHEN b.Rnd % 11 = 0 THEN 1 ELSE 0 END AS MissingLunchOut,
        CASE WHEN b.Rnd % 13 = 0 THEN 1 ELSE 0 END AS MissingLunchIn
    FROM Base b
)
INSERT INTO dbo.PunchLogs (UserId, EmployeeName, CheckTime, InOutMode, Source)
SELECT
    ev.UserId,
    ev.EmployeeName,
    ev.CheckTime,
    ev.InOutMode,
    N'DemoSeed'
FROM
(
    SELECT
        r.UserId,
        r.EmployeeName,
        DATEADD(MINUTE, DATEDIFF(MINUTE, 0, r.BaseStart) + r.LateMinutes, CAST(r.WorkDate AS datetime2)) AS CheckTime,
        CAST(1 AS int) AS InOutMode
    FROM Rows r
    WHERE r.IsAbsent = 0

    UNION ALL

    SELECT
        r.UserId,
        r.EmployeeName,
        DATEADD(MINUTE, DATEDIFF(MINUTE, 0, r.BaseStart) + 240, CAST(r.WorkDate AS datetime2)) AS CheckTime,
        CAST(0 AS int) AS InOutMode
    FROM Rows r
    WHERE r.IsAbsent = 0 AND r.MissingLunchOut = 0

    UNION ALL

    SELECT
        r.UserId,
        r.EmployeeName,
        DATEADD(MINUTE, DATEDIFF(MINUTE, 0, r.BaseStart) + 300, CAST(r.WorkDate AS datetime2)) AS CheckTime,
        CAST(1 AS int) AS InOutMode
    FROM Rows r
    WHERE r.IsAbsent = 0 AND r.MissingLunchIn = 0

    UNION ALL

    SELECT
        r.UserId,
        r.EmployeeName,
        DATEADD(MINUTE, DATEDIFF(MINUTE, 0, r.BaseEnd) + r.ExtraMinutes, CAST(r.WorkDate AS datetime2)) AS CheckTime,
        CAST(0 AS int) AS InOutMode
    FROM Rows r
    WHERE r.IsAbsent = 0
) ev
OPTION (MAXRECURSION 0);

UPDATE dbo.SyncState
SET LastSync = SYSUTCDATETIME(),
    LastRun = SYSUTCDATETIME(),
    LastStatus = N'OK',
    LastMessage = N'Demo seed aplicado correctamente.'
WHERE Id = 1;
"@
Invoke-SqlNonQuery -ConnectionString $demoConn -Sql $seedEmployeesAndPunchesSql -Parameters @{
    "@EmployeeCount" = $EmployeeCount
    "@FromDate"      = $fromDate
    "@ToDate"        = $today
}

$upsertUserSql = @"
DECLARE @RoleId INT = (SELECT TOP (1) RoleId FROM dbo.ApiRoles WHERE RoleName = @RoleName);
IF @RoleId IS NULL
BEGIN
    THROW 50020, 'Rol no encontrado para usuario demo.', 1;
END;

DECLARE @UserId INT = (SELECT TOP (1) UserId FROM dbo.ApiUsers WHERE Username = @Username);
DECLARE @Salt VARBINARY(16) = CRYPT_GEN_RANDOM(16);
DECLARE @Hash VARBINARY(32) = HASHBYTES('SHA2_256', @Salt + CONVERT(VARBINARY(4000), @Password));

IF @UserId IS NULL
BEGIN
    INSERT INTO dbo.ApiUsers
    (
        Username, PasswordSalt, PasswordHash, MustChangePassword, IsActive, UpdatedAt
    )
    VALUES
    (
        @Username, @Salt, @Hash, 0, 1, SYSUTCDATETIME()
    );

    SET @UserId = SCOPE_IDENTITY();
END;
ELSE
BEGIN
    UPDATE dbo.ApiUsers
    SET PasswordSalt = @Salt,
        PasswordHash = @Hash,
        MustChangePassword = 0,
        IsActive = 1,
        UpdatedAt = SYSUTCDATETIME()
    WHERE UserId = @UserId;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ApiUserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
BEGIN
    INSERT INTO dbo.ApiUserRoles(UserId, RoleId) VALUES (@UserId, @RoleId);
END;
"@

Invoke-SqlNonQuery -ConnectionString $demoConn -Sql $upsertUserSql -Parameters @{
    "@Username" = $SupervisorUser
    "@Password" = $SupervisorPassword
    "@RoleName" = "admin"
}

Invoke-SqlNonQuery -ConnectionString $demoConn -Sql $upsertUserSql -Parameters @{
    "@Username" = $AdminUser
    "@Password" = $AdminPassword
    "@RoleName" = "admin"
}

Invoke-SqlNonQuery -ConnectionString $demoConn -Sql $upsertUserSql -Parameters @{
    "@Username" = $ConsultaUser
    "@Password" = $ConsultaPassword
    "@RoleName" = "consulta"
}

Write-Host ""
Write-Host "[DEMO-DB] Base demo lista: $Database" -ForegroundColor Green
Write-Host "[DEMO-DB] Empleados: $EmployeeCount | Dias sembrados: $LookbackDays"
Write-Host "[DEMO-DB] Usuarios API demo:" -ForegroundColor Cyan
Write-Host "  - $SupervisorUser / $SupervisorPassword (admin con modulos restringidos visibles)"
Write-Host "  - $AdminUser / $AdminPassword (admin normal)"
Write-Host "  - $ConsultaUser / $ConsultaPassword (consulta)"
Write-Host ""
