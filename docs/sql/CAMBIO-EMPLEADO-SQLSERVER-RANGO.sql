/*
CAMBIO CONTROLADO DE EMPLEADO EN SQL SERVER (ZKBD)
--------------------------------------------------
Objetivo:
- Cambiar USERID y/o actualizar nombre/depto/puesto/dept/shift SOLO en un rango de fechas.
- NO tocar entradas/salidas ni tiempos (CheckTime, InOutMode).

Modo seguro:
- Ejecutar primero con @ApplyChanges = 0 (simulación).
- Revisar conflictos por índice único (UserId, CheckTime).
- Solo después ejecutar con @ApplyChanges = 1.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @OldUserId INT = 10;                 -- USERID actual
DECLARE @NewUserId INT = 110;                -- USERID nuevo (si no cambia, usar mismo valor que @OldUserId)
DECLARE @FromDate DATE = '2026-03-09';       -- fecha inicio (incluyente)
DECLARE @ToDate DATE = '2026-03-11';         -- fecha fin (incluyente)

DECLARE @NewName NVARCHAR(200) = N'Nombre Actualizado';
DECLARE @NewDepartment NVARCHAR(150) = N'PROD';
DECLARE @NewPosition NVARCHAR(150) = N'OPERADOR';
DECLARE @NewPervasiveDept NVARCHAR(30) = N'1';
DECLARE @NewPervasiveShift NVARCHAR(30) = N'1';

DECLARE @ApplyChanges BIT = 0;               -- 0 = simulación (rollback), 1 = aplicar (commit)
DECLARE @AllowSkipConflicts BIT = 1;         -- 1 = omite filas en conflicto, 0 = aborta si hay conflicto

DECLARE @FromDateTime DATETIME2 = CAST(@FromDate AS DATETIME2);
DECLARE @ToDateTime DATETIME2 = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2)));

IF @FromDate > @ToDate
BEGIN
    THROW 51000, 'Rango inválido: @FromDate no puede ser mayor que @ToDate.', 1;
END;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '=== PREVIEW SQL SERVER ===';

    SELECT
        @OldUserId AS OldUserId,
        @NewUserId AS NewUserId,
        @FromDate AS FromDate,
        @ToDate AS ToDate,
        @ApplyChanges AS ApplyChanges,
        @AllowSkipConflicts AS AllowSkipConflicts;

    SELECT COUNT(1) AS OldRowsInRange
    FROM dbo.PunchLogs
    WHERE UserId = @OldUserId
      AND CheckTime >= @FromDateTime
      AND CheckTime <= @ToDateTime;

    SELECT COUNT(1) AS NewRowsInRange
    FROM dbo.PunchLogs
    WHERE UserId = @NewUserId
      AND CheckTime >= @FromDateTime
      AND CheckTime <= @ToDateTime;

    ;WITH OldRows AS
    (
        SELECT p.CheckTime
        FROM dbo.PunchLogs p
        WHERE p.UserId = @OldUserId
          AND p.CheckTime >= @FromDateTime
          AND p.CheckTime <= @ToDateTime
    )
    SELECT o.CheckTime
    INTO #Conflicts
    FROM OldRows o
    INNER JOIN dbo.PunchLogs n
        ON n.UserId = @NewUserId
       AND n.CheckTime = o.CheckTime;

    SELECT COUNT(1) AS ConflictRows FROM #Conflicts;

    SELECT TOP (50)
        c.CheckTime AS ConflictCheckTime,
        @OldUserId AS SourceUserId,
        @NewUserId AS TargetUserId
    FROM #Conflicts c
    ORDER BY c.CheckTime;

    IF (@OldUserId <> @NewUserId)
    BEGIN
        IF EXISTS (SELECT 1 FROM #Conflicts) AND @AllowSkipConflicts = 0
        BEGIN
            THROW 51001, 'Se detectaron conflictos (UserId, CheckTime). Ajusta datos o usa @AllowSkipConflicts = 1 para omitirlos.', 1;
        END;

        ;WITH Candidates AS
        (
            SELECT p.Id
            FROM dbo.PunchLogs p
            WHERE p.UserId = @OldUserId
              AND p.CheckTime >= @FromDateTime
              AND p.CheckTime <= @ToDateTime
              AND (
                    @AllowSkipConflicts = 0
                    OR NOT EXISTS
                    (
                        SELECT 1
                        FROM dbo.PunchLogs x
                        WHERE x.UserId = @NewUserId
                          AND x.CheckTime = p.CheckTime
                    )
                  )
        )
        UPDATE p
           SET p.UserId = @NewUserId,
               p.EmployeeName = COALESCE(NULLIF(LTRIM(RTRIM(@NewName)), ''), p.EmployeeName)
        FROM dbo.PunchLogs p
        INNER JOIN Candidates c
            ON c.Id = p.Id;

        PRINT CONCAT('PunchLogs UserId cambiados: ', @@ROWCOUNT);
    END
    ELSE
    BEGIN
        UPDATE p
           SET p.EmployeeName = COALESCE(NULLIF(LTRIM(RTRIM(@NewName)), ''), p.EmployeeName)
        FROM dbo.PunchLogs p
        WHERE p.UserId = @NewUserId
          AND p.CheckTime >= @FromDateTime
          AND p.CheckTime <= @ToDateTime;

        PRINT CONCAT('PunchLogs nombre actualizado (sin cambio de UserId): ', @@ROWCOUNT);
    END;

    -- Asegura fila de empleado destino con datos administrativos (sin tocar horarios de marcas)
    MERGE dbo.Employees AS target
    USING
    (
        SELECT
            @NewUserId AS UserId,
            COALESCE(NULLIF(LTRIM(RTRIM(@NewName)), ''), CONCAT(N'Empleado_', @NewUserId)) AS EmployeeName,
            NULLIF(LTRIM(RTRIM(@NewDepartment)), '') AS Department,
            NULLIF(LTRIM(RTRIM(@NewPosition)), '') AS [Position],
            NULLIF(LTRIM(RTRIM(@NewPervasiveDept)), '') AS PervasiveDept,
            NULLIF(LTRIM(RTRIM(@NewPervasiveShift)), '') AS PervasiveShift
    ) AS source
    ON target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET
            target.EmployeeName = COALESCE(source.EmployeeName, target.EmployeeName),
            target.Department = COALESCE(source.Department, target.Department),
            target.[Position] = COALESCE(source.[Position], target.[Position]),
            target.PervasiveDept = COALESCE(source.PervasiveDept, target.PervasiveDept),
            target.PervasiveShift = COALESCE(source.PervasiveShift, target.PervasiveShift),
            target.UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT(UserId, EmployeeName, Department, [Position], PervasiveDept, PervasiveShift, WorkScheduleCode, OvertimeAuthorized, StandardShiftHours, UpdatedAt)
        VALUES(source.UserId, source.EmployeeName, source.Department, source.[Position], source.PervasiveDept, source.PervasiveShift, NULL, 0, 9.60, SYSUTCDATETIME());

    PRINT 'Employees upsert completado para USERID destino.';

    -- Verificación post-update (en transacción)
    SELECT TOP (200)
        p.UserId,
        p.EmployeeName,
        p.CheckTime,
        p.InOutMode,
        p.Source
    FROM dbo.PunchLogs p
    WHERE p.UserId = @NewUserId
      AND p.CheckTime >= @FromDateTime
      AND p.CheckTime <= @ToDateTime
    ORDER BY p.CheckTime;

    IF @ApplyChanges = 1
    BEGIN
        COMMIT TRANSACTION;
        PRINT 'COMMIT aplicado en SQL Server.';
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'SIMULACIÓN finalizada (ROLLBACK ejecutado en SQL Server).';
    END;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrNum INT = ERROR_NUMBER();
    DECLARE @ErrState INT = ERROR_STATE();

    RAISERROR('Error en cambio controlado SQL Server: %s', 16, 1, @ErrMsg) WITH NOWAIT;
END CATCH;
