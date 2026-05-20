using Checador.Domain.Models;
using Microsoft.Data.SqlClient;
using System.Data.Odbc;

namespace Checador.Infrastructure.Access;

public sealed class SqlDeviceSyncService
{
    private readonly SqlSyncOptions _options;
    private readonly ZkTecoDirectAttendanceDataSource _directSource;
    private readonly PervasiveAttendanceMirrorService? _pervasiveMirror;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SqlDeviceSyncService(
        SqlSyncOptions options,
        ZkTecoDirectAttendanceDataSource directSource,
        PervasiveAttendanceMirrorService? pervasiveMirror = null)
    {
        _options = options;
        _directSource = directSource;
        _pervasiveMirror = pervasiveMirror;
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await EnsureDatabaseExistsAsync(cancellationToken);
            await EnsureSchemaAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public bool IsCurrentProcessAuthorizedForDirectDevice()
        => _directSource.IsCurrentProcessAuthorizedForDirectDevice();

    public string GetDirectAccessBlockedMessage(string operation = "Connect")
        => _directSource.BuildDirectAccessBlockedMessage(operation);

    public async Task<SqlSyncRunResult> DownloadAttendanceLogsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        var lastSync = await GetLastSyncAsync(cancellationToken);
        var lookbackStart = DateTime.Now.AddDays(-Math.Max(_options.InitialLookbackDays, 1));
        var fromBase = _options.ForceLookbackWindow ? lookbackStart : (lastSync ?? lookbackStart);
        var from = fromBase.AddSeconds(-1);
        var to = DateTime.Now;

        var employees = await _directSource.GetEmployeesAsync(cancellationToken);
        var punches = await _directSource.GetPunchesAsync(null, from, to, cancellationToken);

        return await ImportSnapshotAsync(
            employees,
            punches,
            source: "DirectDevice",
            runLabel: "Download Attendance Logs",
            overrideLastSync: punches.Count > 0 ? punches.Max(p => p.CheckTime) : lastSync,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PervasivePayloadPreviewItem>> GetPervasivePayloadPreviewAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        if (_pervasiveMirror is null || !_pervasiveMirror.IsEnabled)
        {
            return Array.Empty<PervasivePayloadPreviewItem>();
        }

        var lastSync = await GetLastSyncAsync(cancellationToken);
        var lookbackStart = DateTime.Now.AddDays(-Math.Max(_options.InitialLookbackDays, 1));
        var fromBase = _options.ForceLookbackWindow ? lookbackStart : (lastSync ?? lookbackStart);
        var from = fromBase.AddSeconds(-1);
        var to = DateTime.Now;

        var employees = await _directSource.GetEmployeesAsync(cancellationToken);
        var punches = await _directSource.GetPunchesAsync(null, from, to, cancellationToken);
        var sqlEmployeesForMirror = await GetSqlEmployeesAsync(cancellationToken);
        var mirrorEmployees = sqlEmployeesForMirror
            .Select(kvp => new EmployeeInfo(kvp.Key, kvp.Value.Name))
            .ToList();
        var mirrorMeta = sqlEmployeesForMirror.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value.PervasiveDept, kvp.Value.PervasiveShift));

        return await _pervasiveMirror.BuildPreviewAsync(mirrorEmployees, punches, mirrorMeta, cancellationToken);
    }

    public async Task<PervasiveNightShiftCleanupPreviewResult> PreviewPervasiveNightShiftHistoricalCleanupAsync(
        DateTime fromDate,
        DateTime toDate,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        var from = fromDate.Date;
        var to = toDate.Date;
        if (from > to)
        {
            throw new InvalidOperationException("Rango inválido para preview de limpieza nocturna. La fecha inicial no puede ser mayor a la final.");
        }

        if (_pervasiveMirror is null || !_pervasiveMirror.IsEnabled)
        {
            return new PervasiveNightShiftCleanupPreviewResult(
                false,
                from,
                to,
                userId,
                0,
                0,
                false,
                0,
                0,
                "Preview no disponible: espejo Pervasive deshabilitado.");
        }

        const int maxRows = 120000;
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var (candidateRows, totalRows) = await GetSqlNightShiftPunchesForCleanupAsync(
            connection,
            from,
            to,
            userId,
            maxRows,
            cancellationToken);

        var targetUsers = new List<int>();
        if (userId.HasValue && userId.Value > 0)
        {
            targetUsers.Add(userId.Value);
        }

        if (candidateRows.Count == 0 && targetUsers.Count == 0)
        {
            return new PervasiveNightShiftCleanupPreviewResult(
                true,
                from,
                to,
                userId,
                totalRows,
                0,
                false,
                0,
                0,
                "Preview limpieza nocturna: sin marcas candidatas de horario 3 en el rango.");
        }

        var affectedUsers = candidateRows
            .Select(x => x.UserId)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        foreach (var explicitUser in targetUsers)
        {
            if (!affectedUsers.Contains(explicitUser))
            {
                affectedUsers.Add(explicitUser);
            }
        }

        affectedUsers = affectedUsers.OrderBy(x => x).ToList();
        var estimatedSplitRows = affectedUsers.Count == 0
            ? 0
            : await _pervasiveMirror.CountNightShiftSplitRowsAsync(from, to, affectedUsers, cancellationToken);

        var truncated = totalRows > maxRows;
        var message =
            $"Preview limpieza nocturna Pervasive ({from:dd/MM/yyyy} - {to:dd/MM/yyyy}). " +
            $"Marcas candidatas={totalRows}, procesables={candidateRows.Count}, usuarios afectados={affectedUsers.Count}, " +
            $"renglones partidos estimados={estimatedSplitRows}. " +
            (truncated ? $"Aviso: se aplicará tope de {maxRows} marcas al ejecutar." : "Sin tope por volumen para este rango.");

        return new PervasiveNightShiftCleanupPreviewResult(
            true,
            from,
            to,
            userId,
            totalRows,
            candidateRows.Count,
            truncated,
            affectedUsers.Count,
            estimatedSplitRows,
            message);
    }

    public async Task<PervasiveNightShiftCleanupResult> RunPervasiveNightShiftHistoricalCleanupAsync(
        DateTime fromDate,
        DateTime toDate,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        var from = fromDate.Date;
        var to = toDate.Date;
        if (from > to)
        {
            throw new InvalidOperationException("Rango inválido para limpieza nocturna. La fecha inicial no puede ser mayor a la final.");
        }

        if (_pervasiveMirror is null || !_pervasiveMirror.IsEnabled)
        {
            return new PervasiveNightShiftCleanupResult(
                false,
                from,
                to,
                userId,
                0,
                0,
                false,
                0,
                0,
                "Espejo Pervasive deshabilitado.",
                "Limpieza nocturna no ejecutada: espejo Pervasive deshabilitado.");
        }

        const int maxRows = 120000;
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var sqlEmployeesForMirror = await GetSqlEmployeesAsync(connection, null, cancellationToken);
        var mirrorEmployees = sqlEmployeesForMirror
            .Select(kvp => new EmployeeInfo(kvp.Key, kvp.Value.Name))
            .ToList();
        var mirrorMeta = sqlEmployeesForMirror.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value.PervasiveDept, kvp.Value.PervasiveShift));

        var (candidateRows, totalRows) = await GetSqlNightShiftPunchesForCleanupAsync(
            connection,
            from,
            to,
            userId,
            maxRows,
            cancellationToken);

        var targetUsers = new List<int>();
        if (userId.HasValue && userId.Value > 0)
        {
            targetUsers.Add(userId.Value);
        }

        if (candidateRows.Count == 0 && targetUsers.Count == 0)
        {
            return new PervasiveNightShiftCleanupResult(
                true,
                from,
                to,
                userId,
                totalRows,
                0,
                false,
                0,
                0,
                "Sin marcas candidatas de horario 3.",
                "Limpieza nocturna completada: sin marcas candidatas de horario 3 en el rango.");
        }

        var affectedUsers = candidateRows
            .Select(x => x.UserId)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        foreach (var explicitUser in targetUsers)
        {
            if (!affectedUsers.Contains(explicitUser))
            {
                affectedUsers.Add(explicitUser);
            }
        }
        affectedUsers = affectedUsers.OrderBy(x => x).ToList();

        var totalMirrorInserted = 0;
        var totalMirrorUpdated = 0;
        var totalMirrorSkipped = 0;
        var dayChunkCount = 0;
        if (candidateRows.Count > 0)
        {
            const int chunkDays = 10;
            var orderedRows = candidateRows
                .OrderBy(x => x.CheckTime)
                .ToList();
            var dayChunks = orderedRows
                .GroupBy(x => x.CheckTime.Date)
                .OrderBy(x => x.Key)
                .Select(x => x.ToList())
                .Chunk(chunkDays)
                .ToList();
            dayChunkCount = dayChunks.Count;

            foreach (var dayChunk in dayChunks)
            {
                var chunkRows = dayChunk
                    .SelectMany(x => x)
                    .OrderBy(x => x.CheckTime)
                    .ToList();

                var chunkMirror = await MirrorWithRetriesAsync(
                    mirrorEmployees,
                    chunkRows,
                    mirrorMeta,
                    cancellationToken);

                totalMirrorInserted += chunkMirror.Inserted;
                totalMirrorUpdated += chunkMirror.Updated;
                totalMirrorSkipped += chunkMirror.Skipped;
            }
        }

        var deletedSplitRows = await _pervasiveMirror.CleanupNightShiftSplitRowsAsync(
            from,
            to,
            affectedUsers,
            cancellationToken);

        var canonicalNameByUser = mirrorEmployees
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().EmployeeName);

        var normalizedNameRows = await _pervasiveMirror.NormalizeNightShiftEmployeeNamesAsync(
            from,
            to,
            canonicalNameByUser,
            affectedUsers,
            cancellationToken);

        var remainingSplitRows = await _pervasiveMirror.CountNightShiftSplitRowsAsync(
            from,
            to,
            affectedUsers,
            cancellationToken);

        var mirrorMessage = candidateRows.Count > 0
            ? $"Espejo nocturno por bloques ({dayChunkCount} corrida(s)). " +
              $"Insertados={totalMirrorInserted}, actualizados={totalMirrorUpdated}, omitidos={totalMirrorSkipped}. " +
              $"Nombres normalizados={normalizedNameRows}, renglones partidos pendientes={remainingSplitRows}."
            : $"Sin marcas SQL candidatas; se ejecutó limpieza directa en Pervasive para USERID(s) {string.Join(", ", affectedUsers)}. " +
              $"Nombres normalizados={normalizedNameRows}, renglones partidos pendientes={remainingSplitRows}.";

        var truncated = totalRows > maxRows;
        var message =
            $"Limpieza nocturna Pervasive ({from:dd/MM/yyyy} - {to:dd/MM/yyyy}). " +
            $"Marcas candidatas={totalRows}, procesadas={candidateRows.Count}, usuarios={affectedUsers.Count}, " +
            $"renglones huérfanos eliminados={deletedSplitRows}. " +
            (truncated ? $"Aviso: se aplicó tope de {maxRows} marcas en esta corrida. " : string.Empty) +
            mirrorMessage;

        return new PervasiveNightShiftCleanupResult(
            true,
            from,
            to,
            userId,
            totalRows,
            candidateRows.Count,
            truncated,
            affectedUsers.Count,
            deletedSplitRows,
            mirrorMessage,
            message);
    }

    public async Task<SqlSyncRunResult> ImportSnapshotAsync(
        IReadOnlyList<EmployeeInfo> employees,
        IReadOnlyList<PunchRecord> punches,
        string source,
        string runLabel,
        DateTime? overrideLastSync,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        var inserted = 0;
        var duplicated = 0;
        var suppressed = 0;
        var insertedPunches = new List<PunchRecord>();
        var sqlCorrectiveUpdated = 0;

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var remapLookup = await GetEmployeeReassignMapAsync(connection, tx, cancellationToken);
            var suppressedEmployees = await GetSuppressedEmployeesAsync(connection, tx, cancellationToken);
            var suppressedUserIds = suppressedEmployees.Keys.ToHashSet();
            var latestPunchByEffectiveUserId = punches
                .GroupBy(p =>
                {
                    if (remapLookup.TryGetValue(p.UserId, out var mappedUserId))
                    {
                        return mappedUserId;
                    }

                    return p.UserId;
                })
                .ToDictionary(g => g.Key, g => g.Max(p => p.CheckTime));

            var reactivatedUserIds = suppressedEmployees
                .Where(entry =>
                    latestPunchByEffectiveUserId.TryGetValue(entry.Key, out var lastPunchTime)
                    && lastPunchTime > entry.Value)
                .Select(entry => entry.Key)
                .ToList();

            if (reactivatedUserIds.Count > 0)
            {
                await RemoveSuppressedEmployeesAsync(connection, tx, reactivatedUserIds, cancellationToken);
                foreach (var reactivatedUserId in reactivatedUserIds)
                {
                    suppressedUserIds.Remove(reactivatedUserId);
                }
            }

            foreach (var employee in employees)
            {
                var effectiveUserId = remapLookup.TryGetValue(employee.UserId, out var mappedEmployeeUserId)
                    ? mappedEmployeeUserId
                    : employee.UserId;

                if (remapLookup.ContainsKey(employee.UserId) || suppressedUserIds.Contains(effectiveUserId))
                {
                    continue;
                }

                await UpsertEmployeeAsync(connection, tx, employee.UserId, employee.EmployeeName, cancellationToken);
            }

            foreach (var punch in punches.OrderBy(p => p.CheckTime))
            {
                var effectiveUserId = remapLookup.TryGetValue(punch.UserId, out var remappedUserId)
                    ? remappedUserId
                    : punch.UserId;
                var effectivePunch = effectiveUserId == punch.UserId
                    ? punch
                    : new PunchRecord(
                        effectiveUserId,
                        punch.EmployeeName,
                        punch.CheckTime,
                        punch.InOutMode,
                        punch.WorkScheduleCode,
                        punch.OvertimeAuthorized,
                        punch.StandardShiftHours);

                if (suppressedUserIds.Contains(effectivePunch.UserId))
                {
                    suppressed++;
                    continue;
                }

                var wasInserted = await TryInsertPunchAsync(connection, tx, effectivePunch, source, cancellationToken);
                if (wasInserted)
                {
                    inserted++;
                    insertedPunches.Add(effectivePunch);
                }
                else
                {
                    duplicated++;
                }
            }

            var weeklyCorrectiveFrom = DateTime.Now.Date.AddDays(-7);
            var correctiveFrom = insertedPunches.Count > 0
                ? MinDate(weeklyCorrectiveFrom, insertedPunches.Min(p => p.CheckTime).Date.AddDays(-1))
                : weeklyCorrectiveFrom;
            sqlCorrectiveUpdated = await ApplySqlCorrectiveCleanupAsync(
                connection,
                tx,
                correctiveFrom,
                maxGapSeconds: 60,
                cancellationToken);

            string mirrorMessage;
            if (_pervasiveMirror is not null && _pervasiveMirror.IsEnabled)
            {
                IReadOnlyList<PunchRecord> mirrorPunches = Array.Empty<PunchRecord>();
                try
                {
                    var reconciliationFrom = correctiveFrom;
                    var weeklyBackfillPunches = await GetSqlPunchesForMirrorAsync(
                        connection,
                        tx,
                        reconciliationFrom,
                        maxRows: 4000,
                        cancellationToken);

                    mirrorPunches = BuildRecentMirrorSubset(weeklyBackfillPunches, maxGroups: 120);

                    if (mirrorPunches.Count == 0)
                    {
                        mirrorMessage = $"Espejo Pervasive omitido: sin candidatos de reconciliacion desde {reconciliationFrom:dd-MM-yyyy}.";
                        goto MirrorHandled;
                    }

                    var sqlEmployeesForMirror = await GetSqlEmployeesAsync(connection, tx, cancellationToken);
                    var mirrorEmployees = sqlEmployeesForMirror
                        .Select(kvp => new EmployeeInfo(kvp.Key, kvp.Value.Name))
                        .ToList();
                    var mirrorMeta = sqlEmployeesForMirror.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (kvp.Value.PervasiveDept, kvp.Value.PervasiveShift));
                    var mirror = await MirrorWithRetriesAsync(mirrorEmployees, mirrorPunches, mirrorMeta, cancellationToken);
                    mirrorMessage = $"Reconciliacion Pervasive automatica desde {reconciliationFrom:dd-MM-yyyy}. {mirror.Message}";
                }
                catch (Exception mirrorEx)
                {
                    if (IsTimeoutLikeException(mirrorEx))
                    {
                        try
                        {
                            var sqlEmployeesForMirror = await GetSqlEmployeesAsync(connection, tx, cancellationToken);
                            var mirrorEmployees = sqlEmployeesForMirror
                                .Select(kvp => new EmployeeInfo(kvp.Key, kvp.Value.Name))
                                .ToList();
                            var mirrorMeta = sqlEmployeesForMirror.ToDictionary(
                                kvp => kvp.Key,
                                kvp => (kvp.Value.PervasiveDept, kvp.Value.PervasiveShift));

                            var reducedSource = mirrorPunches.Count > 0
                                ? mirrorPunches
                                : (insertedPunches.Count > 0 ? insertedPunches : punches);
                            var reducedPunches = BuildRecentMirrorSubset(reducedSource, maxGroups: 40);
                            var fallbackMirror = await MirrorWithRetriesAsync(mirrorEmployees, reducedPunches, mirrorMeta, cancellationToken);
                            mirrorMessage = $"Espejo Pervasive parcial por contingencia (timeout en lote completo). {fallbackMirror.Message}";
                        }
                        catch (Exception)
                        {
                            try
                            {
                                var sqlEmployeesForMirror = await GetSqlEmployeesAsync(connection, tx, cancellationToken);
                                var mirrorEmployees = sqlEmployeesForMirror
                                    .Select(kvp => new EmployeeInfo(kvp.Key, kvp.Value.Name))
                                    .ToList();
                                var mirrorMeta = sqlEmployeesForMirror.ToDictionary(
                                    kvp => kvp.Key,
                                    kvp => (kvp.Value.PervasiveDept, kvp.Value.PervasiveShift));

                                var ultraReducedPunches = BuildRecentMirrorSubset(
                                    mirrorPunches.Count > 0 ? mirrorPunches : (insertedPunches.Count > 0 ? insertedPunches : punches),
                                    maxGroups: 8);

                                var fastMirror = await _pervasiveMirror!.MirrorSnapshotFastFallbackAsync(
                                    mirrorEmployees,
                                    ultraReducedPunches,
                                    mirrorMeta,
                                    cancellationToken);

                                mirrorMessage = $"Espejo Pervasive por contingencia RAPIDA (upsert directo tras timeout). {fastMirror.Message}";
                            }
                            catch (Exception fastEx)
                            {
                                if (_pervasiveMirror.StopOnFailure)
                                {
                                    throw new InvalidOperationException($"Fallo espejo Pervasive (modo estricto): {fastEx.Message}", fastEx);
                                }

                                mirrorMessage = $"Espejo Pervasive con error (no bloqueante, solo afecta espejo de asistencias): {FlattenExceptionMessages(fastEx)}";
                            }
                        }

                        goto MirrorHandled;
                    }

                    if (_pervasiveMirror.StopOnFailure)
                    {
                        throw new InvalidOperationException($"Fallo espejo Pervasive (modo estricto): {mirrorEx.Message}", mirrorEx);
                    }

                    mirrorMessage = $"Espejo Pervasive con error (no bloqueante, solo afecta espejo de asistencias): {FlattenExceptionMessages(mirrorEx)}";
                }

            MirrorHandled:;
            }
            else
            {
                mirrorMessage = "Espejo Pervasive deshabilitado.";
            }

            var nextSync = overrideLastSync;
            var message = $"{runLabel} OK. Empleados: {employees.Count}, marcas leidas: {punches.Count}, insertadas: {inserted}, duplicadas: {duplicated}, omitidas_baja={suppressed}, sql_actualizados_correctivo={sqlCorrectiveUpdated}. {mirrorMessage}";

            await UpsertSyncStateAsync(connection, tx, nextSync, "OK", message, cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new SqlSyncRunResult(
                employees.Count,
                punches.Count,
                inserted,
                duplicated,
                nextSync,
                message);
        }
        catch (Exception ex)
        {
            try
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch
            {
            }

            await SaveErrorSyncStateAsync(ex.Message, cancellationToken);
            throw new InvalidOperationException($"Fallo {runLabel} en SQL. Detalle: {ex.Message}", ex);
        }
    }

    public async Task<SqlSyncRunResult> InsertManualPunchAsync(
        int userId,
        string employeeName,
        DateTime checkTime,
        int? inOutMode,
        CancellationToken cancellationToken = default)
    {
        return await InsertManualPunchesAsync(
            userId,
            employeeName,
            new[] { new ManualPunchEntry(checkTime, inOutMode) },
            cancellationToken);
    }

    public async Task<SqlSyncRunResult> InsertManualPunchesAsync(
        int userId,
        string employeeName,
        IReadOnlyList<ManualPunchEntry> entries,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        if (userId <= 0)
        {
            throw new InvalidOperationException("USERID invalido para insercion manual.");
        }

        if (entries is null || entries.Count == 0)
        {
            throw new InvalidOperationException("Debes enviar al menos una marca manual.");
        }

        var normalizedName = string.IsNullOrWhiteSpace(employeeName)
            ? $"Empleado_{userId}"
            : employeeName.Trim();

        var normalizedEntries = entries
            .Select(entry =>
            {
                if (entry.CheckTime == default)
                {
                    throw new InvalidOperationException("La fecha/hora de la marca manual es obligatoria.");
                }

                if (entry.InOutMode is not null && entry.InOutMode is not (0 or 1 or 2 or 3))
                {
                    throw new InvalidOperationException("InOutMode invalido. Usa 0=entrada, 1=salida, 2=salida comida, 3=entrada comida o sin modo.");
                }

                return new PunchRecord(userId, normalizedName, entry.CheckTime, entry.InOutMode);
            })
            .OrderBy(p => p.CheckTime)
            .ToList();

        var keepLastSync = await GetLastSyncAsync(cancellationToken);

        return await ImportSnapshotAsync(
            new[] { new EmployeeInfo(userId, normalizedName) },
            normalizedEntries,
            source: "DesktopManual",
            runLabel: normalizedEntries.Count == 1 ? "Insercion manual de marca" : "Insercion manual de marcas",
            overrideLastSync: keepLastSync,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<SqlEmployeeCatalogItem>> GetEmployeeCatalogAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        IReadOnlyList<EmployeeInfo> deviceEmployees;
        try
        {
            deviceEmployees = await _directSource.GetEmployeesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // En modo SyncAgent-only o con reloj no disponible,
            // se permite seguir operando con catálogo SQL sin bloquear la UI.
            deviceEmployees = Array.Empty<EmployeeInfo>();
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var sqlEmployees = await GetSqlEmployeesAsync(connection, null, cancellationToken);
        var remapLookup = await GetEmployeeReassignMapAsync(connection, null, cancellationToken);
        var suppressedUserIds = (await GetSuppressedEmployeesAsync(connection, null, cancellationToken)).Keys.ToHashSet();

        var deviceByEffectiveUserId = new Dictionary<int, string>();
        foreach (var deviceEmployee in deviceEmployees)
        {
            var effectiveUserId = remapLookup.TryGetValue(deviceEmployee.UserId, out var mappedUserId)
                ? mappedUserId
                : deviceEmployee.UserId;

            if (suppressedUserIds.Contains(effectiveUserId))
            {
                continue;
            }

            if (!deviceByEffectiveUserId.ContainsKey(effectiveUserId))
            {
                deviceByEffectiveUserId[effectiveUserId] = deviceEmployee.EmployeeName;
            }
        }

        var suppressedOldIds = remapLookup.Keys.ToHashSet();
        var allUserIds = sqlEmployees.Keys
            .Concat(deviceByEffectiveUserId.Keys)
            .Where(userId => !suppressedOldIds.Contains(userId) && !suppressedUserIds.Contains(userId))
            .Distinct()
            .OrderBy(userId => userId)
            .ToList();

        var result = allUserIds
            .Select(userId =>
            {
                var hasSqlData = sqlEmployees.TryGetValue(userId, out var sqlData);
                var deviceName = deviceByEffectiveUserId.TryGetValue(userId, out var deviceNameFromMap)
                    ? deviceNameFromMap
                    : (hasSqlData ? sqlData.Name : $"Empleado_{userId}");

                return new SqlEmployeeCatalogItem(
                    userId,
                    deviceName,
                    hasSqlData ? sqlData.Name : null,
                    hasSqlData ? sqlData.UpdatedAt : null,
                    hasSqlData && sqlData.HasPhoto,
                    hasSqlData ? sqlData.Department : null,
                    hasSqlData ? sqlData.Position : null,
                    hasSqlData ? sqlData.PervasiveDept : null,
                    hasSqlData ? sqlData.PervasiveShift : null,
                    hasSqlData ? sqlData.WorkScheduleCode : null,
                    hasSqlData && sqlData.OvertimeAuthorized,
                    hasSqlData ? sqlData.StandardShiftHours : 9.60m);
            })
            .ToList();

        return result;
    }

    public async Task<EmployeeAdminSnapshot?> GetEmployeeAdminSnapshotAsync(int userId, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP 1
    UserId,
    EmployeeName,
    Department,
    [Position],
    PervasiveDept,
    PervasiveShift,
    WorkScheduleCode,
    OvertimeAuthorized
FROM dbo.Employees
WHERE UserId = @UserId;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new EmployeeAdminSnapshot(
            reader.GetInt32(0),
            await reader.IsDBNullAsync(1, cancellationToken) ? null : reader.GetString(1),
            await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetString(2),
            await reader.IsDBNullAsync(3, cancellationToken) ? null : reader.GetString(3),
            await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetString(4),
            await reader.IsDBNullAsync(5, cancellationToken) ? null : reader.GetString(5),
            await reader.IsDBNullAsync(6, cancellationToken) ? (int?)null : reader.GetInt32(6),
            !await reader.IsDBNullAsync(7, cancellationToken) && reader.GetBoolean(7));
    }

    public async Task<EmployeeRenameResult> RenameEmployeeAsync(
        int userId,
        string newName,
        string? department,
        string? position,
        string? pervasiveDept,
        string? pervasiveShift,
        int? workScheduleCode,
        bool overtimeAuthorized,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        if (userId <= 0)
        {
            throw new InvalidOperationException("USERID invalido para renombrar empleado.");
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new InvalidOperationException("El nuevo nombre no puede ir vacio.");
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var sqlName = newName.Trim();
        await UpsertEmployeeAdminDataAsync(connection, null, userId, sqlName, department, position, pervasiveDept, pervasiveShift, workScheduleCode, overtimeAuthorized, cancellationToken);

        (bool Success, string Message) deviceResult;
        try
        {
            var deviceName = BuildDeviceSafeName(sqlName);
            deviceResult = await _directSource.TryUpdateEmployeeNameAsync(userId, deviceName, cancellationToken);
            if (!string.Equals(deviceName, sqlName, StringComparison.Ordinal))
            {
                var detail = $" SQL conserva nombre completo. En checador se aplico version corta: '{deviceName}'.";
                deviceResult = (deviceResult.Success, deviceResult.Message + detail);
            }
        }
        catch (Exception ex)
        {
            deviceResult = (false, $"SQL guardado. No se pudo actualizar nombre en checador: {ex.Message}");
        }

        return new EmployeeRenameResult(
            userId,
            sqlName,
            deviceResult.Success,
            deviceResult.Message,
            true);
    }

    public async Task<EmployeeDeleteResult> DeleteEmployeeAsync(int userId, bool deleteHistoryRows = false, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        if (userId <= 0)
        {
            throw new InvalidOperationException("USERID invalido para eliminar empleado.");
        }

        var deviceResult = await _directSource.TryDeleteEmployeeAsync(userId, cancellationToken);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        if (deviceResult.Success)
        {
            await RemoveSuppressedEmployeesAsync(connection, null, new[] { userId }, cancellationToken);
        }
        else
        {
            await UpsertSuppressedEmployeeAsync(connection, null, userId, "DELETE_REQUESTED", cancellationToken);
        }

        int deletedHistory = 0;
        if (deleteHistoryRows)
        {
            await using var deleteHistoryCmd = new SqlCommand("DELETE FROM dbo.PunchLogs WHERE UserId = @UserId;", connection);
            deleteHistoryCmd.Parameters.AddWithValue("@UserId", userId);
            deletedHistory = await deleteHistoryCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var deleteEmployeeCmd = new SqlCommand("DELETE FROM dbo.Employees WHERE UserId = @UserId;", connection);
        deleteEmployeeCmd.Parameters.AddWithValue("@UserId", userId);
        var deletedEmployees = await deleteEmployeeCmd.ExecuteNonQueryAsync(cancellationToken);

        return new EmployeeDeleteResult(
            userId,
            deviceResult.Success,
            deviceResult.Message,
            deletedEmployees > 0,
            deletedHistory);
    }

    public async Task<(bool Supported, string Message)> CheckEmployeePhotoCapabilityAsync(int userId, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return await _directSource.ProbePhotoCapabilityAsync(userId, cancellationToken);
    }

    public async Task<(bool Success, byte[]? Photo, string? ContentType, string Message)> TryGetEmployeePhotoFromDeviceAsync(int userId, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        var result = await _directSource.TryGetEmployeePhotoAsync(userId, cancellationToken);
        if (result.Success && result.PhotoBytes is { Length: > 0 })
        {
            await SaveEmployeePhotoAsync(userId, result.PhotoBytes, result.ContentType ?? "application/octet-stream", cancellationToken);
            return (true, result.PhotoBytes, result.ContentType ?? "application/octet-stream", "Foto leida del reloj y guardada en SQL.");
        }

        return (false, null, null, result.Message);
    }

    public async Task<(bool Success, string Message)> SyncDeviceTimeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return await _directSource.TrySyncDeviceTimeAsync(cancellationToken);
    }

    public async Task SaveEmployeePhotoAsync(int userId, byte[] photoBytes, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        if (userId <= 0)
        {
            throw new InvalidOperationException("USERID invalido para foto.");
        }

        if (photoBytes is null || photoBytes.Length == 0)
        {
            throw new InvalidOperationException("La foto esta vacia.");
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
UPDATE dbo.Employees
SET Photo = @Photo,
    PhotoContentType = @ContentType,
    PhotoUpdatedAt = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME()
WHERE UserId = @UserId;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.Employees(UserId, EmployeeName, UpdatedAt, Photo, PhotoContentType, PhotoUpdatedAt)
    VALUES(@UserId, CONCAT('Empleado_', @UserId), SYSUTCDATETIME(), @Photo, @ContentType, SYSUTCDATETIME());
END;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Photo", photoBytes);
        cmd.Parameters.AddWithValue("@ContentType", string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<(byte[]? Photo, string? ContentType)> GetEmployeePhotoAsync(int userId, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        if (userId <= 0)
        {
            throw new InvalidOperationException("USERID invalido para consultar foto.");
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP 1 Photo, PhotoContentType
FROM dbo.Employees
WHERE UserId = @UserId;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return (null, null);
        }

        if (await reader.IsDBNullAsync(0, cancellationToken))
        {
            return (null, null);
        }

        var bytes = (byte[])reader[0];
        var contentType = await reader.IsDBNullAsync(1, cancellationToken) ? null : reader.GetString(1);
        return (bytes, contentType);
    }

    public async Task<IReadOnlyList<SqlPunchHistoryItem>> GetHistoryAsync(int? userId, DateTime from, DateTime to, int maxRows = 5000, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP (@TopRows)
    p.UserId,
    COALESCE(NULLIF(e.EmployeeName, ''), p.EmployeeName) AS EmployeeName,
    p.CheckTime,
    p.Source,
    p.InOutMode
FROM dbo.PunchLogs p
LEFT JOIN dbo.Employees e ON e.UserId = p.UserId
WHERE p.CheckTime >= @FromDate
  AND p.CheckTime <= @ToDate
  AND (@UserId IS NULL OR p.UserId = @UserId)
ORDER BY CheckTime DESC;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TopRows", Math.Max(1, maxRows));
        cmd.Parameters.AddWithValue("@FromDate", from);
        cmd.Parameters.AddWithValue("@ToDate", to);
        cmd.Parameters.AddWithValue("@UserId", userId.HasValue ? userId.Value : DBNull.Value);

        var rows = new List<SqlPunchHistoryItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SqlPunchHistoryItem(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.GetString(3),
                await reader.IsDBNullAsync(4, cancellationToken) ? (int?)null : reader.GetInt32(4)));
        }

        return rows;
    }

    public async Task<SqlSyncStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var totalEmployees = await ExecuteScalarIntAsync(connection, "SELECT COUNT(1) FROM dbo.Employees;", cancellationToken);
        var totalPunches = await ExecuteScalarIntAsync(connection, "SELECT COUNT(1) FROM dbo.PunchLogs;", cancellationToken);
        var lastPunch = await ExecuteScalarDateTimeAsync(connection, "SELECT MAX(CheckTime) FROM dbo.PunchLogs;", cancellationToken);

        const string stateSql = @"
SELECT TOP 1
    LastSync,
    LastStatus,
    LastMessage
FROM dbo.SyncState
WHERE Id = 1;";

        await using var cmd = new SqlCommand(stateSql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        DateTime? lastSync = null;
        var lastStatus = "N/A";
        var lastMessage = "Sin sincronizacion ejecutada.";

        if (await reader.ReadAsync(cancellationToken))
        {
            if (!await reader.IsDBNullAsync(0, cancellationToken))
            {
                lastSync = reader.GetDateTime(0);
            }

            if (!await reader.IsDBNullAsync(1, cancellationToken))
            {
                lastStatus = reader.GetString(1);
            }

            if (!await reader.IsDBNullAsync(2, cancellationToken))
            {
                lastMessage = reader.GetString(2);
            }
        }

        return new SqlSyncStats(totalEmployees, totalPunches, lastPunch, lastSync, lastStatus, lastMessage);
    }

    public async Task<SqlLiveStatus> GetLiveStatusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        var stats = await GetStatsAsync(cancellationToken);
        var device = await _directSource.TryGetLiveStatusAsync(cancellationToken);

        return new SqlLiveStatus(
            device.Connected,
            device.DeviceTime,
            device.DeviceUsers,
            stats.TotalEmployees,
            stats.TotalPunches,
            stats.LastPunch,
            stats.LastSync,
            stats.LastStatus,
            device.Message);
    }

    public async Task<IReadOnlyList<WorkScheduleItem>> GetWorkSchedulesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT
    Code,
    [Name],
    StartTime,
    EndTime,
    StandardHours,
    IsActive,
    LateToleranceMinutes
FROM dbo.WorkSchedules
ORDER BY Code;";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<WorkScheduleItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new WorkScheduleItem(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetTimeSpan(2),
                reader.GetTimeSpan(3),
                reader.GetDecimal(4),
                reader.GetBoolean(5),
                await reader.IsDBNullAsync(6, cancellationToken) ? 10 : reader.GetInt32(6)));
        }

        return result;
    }

    public async Task UpsertWorkScheduleAsync(
        int code,
        string name,
        TimeSpan startTime,
        TimeSpan endTime,
        decimal standardHours,
        bool isActive,
        int lateToleranceMinutes = 10,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        if (code <= 0)
        {
            throw new InvalidOperationException("El codigo de horario debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("El nombre del horario es obligatorio.");
        }

        if (standardHours <= 0)
        {
            throw new InvalidOperationException("Las horas estandar deben ser mayores a cero.");
        }

        if (lateToleranceMinutes < 0 || lateToleranceMinutes > 180)
        {
            throw new InvalidOperationException("La tolerancia de retardo debe estar entre 0 y 180 minutos.");
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
MERGE dbo.WorkSchedules AS target
USING (
    SELECT
        @Code AS Code,
        @Name AS [Name],
        @StartTime AS StartTime,
        @EndTime AS EndTime,
        @StandardHours AS StandardHours,
        @IsActive AS IsActive,
        @LateToleranceMinutes AS LateToleranceMinutes
) AS source
ON target.Code = source.Code
WHEN MATCHED THEN
    UPDATE SET
        [Name] = source.[Name],
        StartTime = source.StartTime,
        EndTime = source.EndTime,
        StandardHours = source.StandardHours,
        IsActive = source.IsActive,
        LateToleranceMinutes = source.LateToleranceMinutes,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT(Code, [Name], StartTime, EndTime, StandardHours, IsActive, LateToleranceMinutes, UpdatedAt)
    VALUES(source.Code, source.[Name], source.StartTime, source.EndTime, source.StandardHours, source.IsActive, source.LateToleranceMinutes, SYSUTCDATETIME());";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Code", code);
        cmd.Parameters.AddWithValue("@Name", name.Trim());
        cmd.Parameters.AddWithValue("@StartTime", startTime);
        cmd.Parameters.AddWithValue("@EndTime", endTime);
        cmd.Parameters.AddWithValue("@StandardHours", standardHours);
        cmd.Parameters.AddWithValue("@IsActive", isActive);
        cmd.Parameters.AddWithValue("@LateToleranceMinutes", lateToleranceMinutes);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LatenessIncidentItem>> GetLatenessIncidentsAsync(
        DateTime fromDate,
        DateTime toDate,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        var from = fromDate.Date;
        var to = toDate.Date;
        if (from > to)
        {
            throw new InvalidOperationException("Rango invalido para retardos. La fecha inicial no puede ser mayor a la final.");
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
;WITH FirstPunch AS
(
    SELECT
        p.UserId,
        CAST(p.CheckTime AS date) AS WorkDate,
        MIN(p.CheckTime) AS FirstCheckIn
    FROM dbo.PunchLogs p
    WHERE p.CheckTime >= @FromDate
      AND p.CheckTime < DATEADD(DAY, 1, @ToDate)
      AND (@UserId IS NULL OR p.UserId = @UserId)
    GROUP BY p.UserId, CAST(p.CheckTime AS date)
),
Enriched AS
(
    SELECT
        fp.UserId,
        COALESCE(NULLIF(e.EmployeeName, ''), CONCAT('Empleado_', fp.UserId)) AS EmployeeName,
        fp.WorkDate,
        fp.FirstCheckIn,
        e.WorkScheduleCode,
        ws.[Name] AS WorkScheduleName,
        COALESCE(ws.StartTime, CAST('07:00:00' AS time)) AS ScheduledStartTime,
        COALESCE(ws.LateToleranceMinutes, 10) AS LateToleranceMinutes,
        DATEADD(MINUTE, DATEDIFF(MINUTE, 0, COALESCE(ws.StartTime, CAST('07:00:00' AS time))), CAST(fp.WorkDate AS datetime2)) AS ScheduledStartDateTime
    FROM FirstPunch fp
    LEFT JOIN dbo.Employees e ON e.UserId = fp.UserId
    LEFT JOIN dbo.WorkSchedules ws ON ws.Code = e.WorkScheduleCode
)
SELECT
    UserId,
    EmployeeName,
    WorkDate,
    FirstCheckIn,
    WorkScheduleCode,
    WorkScheduleName,
    ScheduledStartTime,
    LateToleranceMinutes,
    DATEDIFF(MINUTE, ScheduledStartDateTime, FirstCheckIn) AS DelayMinutes
FROM Enriched
WHERE WorkScheduleCode IS NOT NULL
  AND DATEDIFF(MINUTE, ScheduledStartDateTime, FirstCheckIn) > LateToleranceMinutes
ORDER BY WorkDate ASC, UserId ASC;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FromDate", from);
        cmd.Parameters.AddWithValue("@ToDate", to);
        var userParam = cmd.Parameters.Add("@UserId", System.Data.SqlDbType.Int);
        userParam.Value = userId.HasValue && userId.Value > 0 ? userId.Value : DBNull.Value;

        var rows = new List<LatenessIncidentItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var workDate = DateOnly.FromDateTime(reader.GetDateTime(2));
            var scheduleCode = await reader.IsDBNullAsync(4, cancellationToken) ? (int?)null : reader.GetInt32(4);
            var scheduleName = await reader.IsDBNullAsync(5, cancellationToken) ? null : reader.GetString(5);
            rows.Add(new LatenessIncidentItem(
                reader.GetInt32(0),
                reader.GetString(1),
                workDate,
                reader.GetDateTime(3),
                scheduleCode,
                scheduleName,
                reader.GetTimeSpan(6),
                reader.GetInt32(7),
                reader.GetInt32(8)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<SqlPunchHistoryItem>> GetRecentHistoryAsync(int maxRows = 5, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP (@TopRows)
    p.UserId,
    COALESCE(NULLIF(e.EmployeeName, ''), p.EmployeeName) AS EmployeeName,
    p.CheckTime,
    p.Source
FROM dbo.PunchLogs p
LEFT JOIN dbo.Employees e ON e.UserId = p.UserId
ORDER BY p.CheckTime DESC;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TopRows", Math.Max(1, maxRows));

        var rows = new List<SqlPunchHistoryItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SqlPunchHistoryItem(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.GetString(3)));
        }

        return rows;
    }

    public async Task<EmployeeReassignPreviewResult> PreviewEmployeeReassignByDateRangeAsync(
        int oldUserId,
        int newUserId,
        DateTime fromDate,
        DateTime toDate,
        bool includePervasive = true,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        ValidateEmployeeReassignInput(oldUserId, newUserId, fromDate, toDate);

        var fromDateTime = fromDate.Date;
        var toDateTime = toDate.Date.AddDays(1).AddTicks(-1);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var sqlOldRows = await ExecuteCountSqlAsync(connection,
            @"SELECT COUNT(1)
              FROM dbo.PunchLogs
              WHERE UserId = @UserId
                AND CheckTime >= @FromDate
                AND CheckTime <= @ToDate;",
            oldUserId,
            fromDateTime,
            toDateTime,
            cancellationToken);

        var sqlTargetRows = await ExecuteCountSqlAsync(connection,
            @"SELECT COUNT(1)
              FROM dbo.PunchLogs
              WHERE UserId = @UserId
                AND CheckTime >= @FromDate
                AND CheckTime <= @ToDate;",
            newUserId,
            fromDateTime,
            toDateTime,
            cancellationToken);

        var sqlConflictRows = oldUserId == newUserId
            ? 0
            : await ExecuteConflictCountSqlAsync(connection, oldUserId, newUserId, fromDateTime, toDateTime, cancellationToken);

        var sqlAffectedDates = await GetSqlAffectedDatesAsync(connection, oldUserId, fromDateTime, toDateTime, cancellationToken);

        var pervasiveEnabled = includePervasive && _pervasiveMirror is not null && _pervasiveMirror.IsEnabled;
        var pervasiveOldRows = 0;
        var pervasiveTargetRows = 0;
        var pervasiveConflictRows = 0;
        IReadOnlyList<DateTime> pervasiveDates = Array.Empty<DateTime>();

        if (pervasiveEnabled)
        {
            var pervasivePreview = await _pervasiveMirror!.PreviewEmployeeRemapByDateRangeAsync(
                FormatEmployeeCode(oldUserId),
                FormatEmployeeCode(newUserId),
                fromDate,
                toDate,
                cancellationToken);

            pervasiveOldRows = pervasivePreview.OldRows;
            pervasiveTargetRows = pervasivePreview.TargetRows;
            pervasiveConflictRows = pervasivePreview.ConflictRows;
            pervasiveDates = pervasivePreview.AffectedDates;
        }

        var message =
            $"Preview cambio empleado. SQL filas origen={sqlOldRows}, destino={sqlTargetRows}, conflictos={sqlConflictRows}, dias={sqlAffectedDates.Count}. " +
            (pervasiveEnabled
                ? $"Pervasive filas origen={pervasiveOldRows}, destino={pervasiveTargetRows}, conflictos={pervasiveConflictRows}, dias={pervasiveDates.Count}."
                : "Pervasive deshabilitado.");

        return new EmployeeReassignPreviewResult(
            oldUserId,
            newUserId,
            fromDate.Date,
            toDate.Date,
            sqlOldRows,
            sqlTargetRows,
            sqlConflictRows,
            sqlAffectedDates.Count,
            sqlAffectedDates,
            pervasiveEnabled,
            pervasiveOldRows,
            pervasiveTargetRows,
            pervasiveConflictRows,
            pervasiveDates.Count,
            pervasiveDates,
            message);
    }

    public async Task<EmployeeReassignApplyResult> ReassignEmployeeByDateRangeAsync(
        int oldUserId,
        int newUserId,
        DateTime fromDate,
        DateTime toDate,
        string? newName,
        string? newDepartment,
        string? newPervasiveDept,
        string? newPervasiveShift,
        bool allowSkipConflicts = true,
        bool includePervasive = true,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        ValidateEmployeeReassignInput(oldUserId, newUserId, fromDate, toDate);

        var preview = await PreviewEmployeeReassignByDateRangeAsync(
            oldUserId,
            newUserId,
            fromDate,
            toDate,
            includePervasive,
            cancellationToken);
        if (!allowSkipConflicts && preview.SqlConflictRows > 0)
        {
            throw new InvalidOperationException($"Existen {preview.SqlConflictRows} conflictos SQL por indice (UserId, CheckTime). Activa omision o corrige antes de aplicar.");
        }

        var fromDateTime = fromDate.Date;
        var toDateTime = toDate.Date.AddDays(1).AddTicks(-1);

        var sqlUpdatedRows = 0;
        await using (var connection = new SqlConnection(_options.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                if (oldUserId != newUserId)
                {
                    if (allowSkipConflicts)
                    {
                        const string updateSql = @"
UPDATE p
   SET p.UserId = @NewUserId,
       p.EmployeeName = COALESCE(NULLIF(@NewName, ''), p.EmployeeName)
FROM dbo.PunchLogs p
WHERE p.UserId = @OldUserId
  AND p.CheckTime >= @FromDate
  AND p.CheckTime <= @ToDate
  AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.PunchLogs x
          WHERE x.UserId = @NewUserId
            AND x.CheckTime = p.CheckTime
      );";

                        await using var cmd = new SqlCommand(updateSql, connection, tx);
                        cmd.Parameters.AddWithValue("@OldUserId", oldUserId);
                        cmd.Parameters.AddWithValue("@NewUserId", newUserId);
                        cmd.Parameters.AddWithValue("@FromDate", fromDateTime);
                        cmd.Parameters.AddWithValue("@ToDate", toDateTime);
                        cmd.Parameters.AddWithValue("@NewName", NormalizeOptional(newName) ?? string.Empty);
                        sqlUpdatedRows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                    else
                    {
                        const string updateSql = @"
UPDATE dbo.PunchLogs
   SET UserId = @NewUserId,
       EmployeeName = COALESCE(NULLIF(@NewName, ''), EmployeeName)
WHERE UserId = @OldUserId
  AND CheckTime >= @FromDate
  AND CheckTime <= @ToDate;";

                        await using var cmd = new SqlCommand(updateSql, connection, tx);
                        cmd.Parameters.AddWithValue("@OldUserId", oldUserId);
                        cmd.Parameters.AddWithValue("@NewUserId", newUserId);
                        cmd.Parameters.AddWithValue("@FromDate", fromDateTime);
                        cmd.Parameters.AddWithValue("@ToDate", toDateTime);
                        cmd.Parameters.AddWithValue("@NewName", NormalizeOptional(newName) ?? string.Empty);
                        sqlUpdatedRows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
                else
                {
                    const string updateSql = @"
UPDATE dbo.PunchLogs
   SET EmployeeName = COALESCE(NULLIF(@NewName, ''), EmployeeName)
WHERE UserId = @UserId
  AND CheckTime >= @FromDate
  AND CheckTime <= @ToDate;";

                    await using var cmd = new SqlCommand(updateSql, connection, tx);
                    cmd.Parameters.AddWithValue("@UserId", newUserId);
                    cmd.Parameters.AddWithValue("@FromDate", fromDateTime);
                    cmd.Parameters.AddWithValue("@ToDate", toDateTime);
                    cmd.Parameters.AddWithValue("@NewName", NormalizeOptional(newName) ?? string.Empty);
                    sqlUpdatedRows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                const string upsertEmployeeSql = @"
MERGE dbo.Employees AS target
USING
(
    SELECT
        @UserId AS UserId,
        @EmployeeName AS EmployeeName,
        @Department AS Department,
        @PervasiveDept AS PervasiveDept,
        @PervasiveShift AS PervasiveShift
) AS source
ON target.UserId = source.UserId
WHEN MATCHED THEN
    UPDATE SET
        EmployeeName = COALESCE(NULLIF(source.EmployeeName, ''), target.EmployeeName),
        Department = COALESCE(source.Department, target.Department),
        PervasiveDept = COALESCE(source.PervasiveDept, target.PervasiveDept),
        PervasiveShift = COALESCE(source.PervasiveShift, target.PervasiveShift),
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT(UserId, EmployeeName, Department, [Position], PervasiveDept, PervasiveShift, WorkScheduleCode, OvertimeAuthorized, StandardShiftHours, UpdatedAt)
    VALUES(source.UserId, COALESCE(NULLIF(source.EmployeeName, ''), CONCAT('Empleado_', source.UserId)), source.Department, NULL, source.PervasiveDept, source.PervasiveShift, NULL, 0, 9.60, SYSUTCDATETIME());";

                await using (var upsertCmd = new SqlCommand(upsertEmployeeSql, connection, tx))
                {
                    upsertCmd.Parameters.AddWithValue("@UserId", newUserId);
                    upsertCmd.Parameters.AddWithValue("@EmployeeName", NormalizeOptional(newName) ?? string.Empty);
                    upsertCmd.Parameters.AddWithValue("@Department", NormalizeOptional(newDepartment) ?? (object)DBNull.Value);
                    upsertCmd.Parameters.AddWithValue("@PervasiveDept", NormalizeOptional(newPervasiveDept) ?? (object)DBNull.Value);
                    upsertCmd.Parameters.AddWithValue("@PervasiveShift", NormalizeOptional(newPervasiveShift) ?? (object)DBNull.Value);
                    await upsertCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                if (oldUserId != newUserId)
                {
                    const string upsertRemapSql = @"
MERGE dbo.EmployeeReassignMap AS target
USING (
    SELECT @OldUserId AS OldUserId, @NewUserId AS NewUserId
) AS source
ON target.OldUserId = source.OldUserId
WHEN MATCHED THEN
    UPDATE SET
        NewUserId = source.NewUserId,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (OldUserId, NewUserId, UpdatedAt)
    VALUES (source.OldUserId, source.NewUserId, SYSUTCDATETIME());";

                    await using (var upsertRemapCmd = new SqlCommand(upsertRemapSql, connection, tx))
                    {
                        upsertRemapCmd.Parameters.AddWithValue("@OldUserId", oldUserId);
                        upsertRemapCmd.Parameters.AddWithValue("@NewUserId", newUserId);
                        await upsertRemapCmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    const string deleteOldCatalogSql = @"
DELETE FROM dbo.Employees
WHERE UserId = @OldUserId
  AND UserId <> @NewUserId;";

                    await using var deleteOldCatalogCmd = new SqlCommand(deleteOldCatalogSql, connection, tx);
                    deleteOldCatalogCmd.Parameters.AddWithValue("@OldUserId", oldUserId);
                    deleteOldCatalogCmd.Parameters.AddWithValue("@NewUserId", newUserId);
                    await deleteOldCatalogCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                try { await tx.RollbackAsync(cancellationToken); } catch { }
                throw;
            }
        }

        var pervasiveEnabled = includePervasive && _pervasiveMirror is not null && _pervasiveMirror.IsEnabled;
        var pervasiveUpdatedRows = 0;
        var pervasiveSkippedConflicts = 0;

        if (pervasiveEnabled)
        {
            var pervasiveApply = await _pervasiveMirror!.ApplyEmployeeRemapByDateRangeAsync(
                FormatEmployeeCode(oldUserId),
                FormatEmployeeCode(newUserId),
                fromDate,
                toDate,
                NormalizeOptional(newName),
                NormalizeOptional(newPervasiveDept),
                NormalizeOptional(newPervasiveShift),
                allowSkipConflicts,
                cancellationToken);

            pervasiveUpdatedRows = pervasiveApply.UpdatedRows;
            pervasiveSkippedConflicts = pervasiveApply.SkippedConflicts;
        }

        var sqlSkippedConflicts = allowSkipConflicts ? preview.SqlConflictRows : 0;
        var message =
            $"Cambio por rango aplicado. SQL actualizadas: {sqlUpdatedRows}, conflictos SQL omitidos: {sqlSkippedConflicts}. " +
            (pervasiveEnabled
                ? $"Pervasive actualizadas: {pervasiveUpdatedRows}, conflictos Pervasive omitidos: {pervasiveSkippedConflicts}."
                : "Pervasive deshabilitado.");

        return new EmployeeReassignApplyResult(
            oldUserId,
            newUserId,
            fromDate.Date,
            toDate.Date,
            sqlUpdatedRows,
            sqlSkippedConflicts,
            true,
            pervasiveEnabled,
            pervasiveUpdatedRows,
            pervasiveSkippedConflicts,
            message);
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(_options.ConnectionString);
        var catalog = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(catalog))
        {
            throw new InvalidOperationException("La conexion SQL requiere Database (Initial Catalog). Ejemplo: Database=ZKBD.");
        }

        var masterBuilder = new SqlConnectionStringBuilder(_options.ConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var escapedDb = catalog.Replace("]", "]]", StringComparison.Ordinal);
        var createDbSql = $@"
IF DB_ID(N'{catalog.Replace("'", "''", StringComparison.Ordinal)}') IS NULL
BEGIN
    EXEC('CREATE DATABASE [{escapedDb}]');
END";

        await using var cmd = new SqlCommand(createDbSql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string schemaSql = @"
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

IF COL_LENGTH('dbo.Employees', 'Department') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD Department NVARCHAR(150) NULL;
END;

IF COL_LENGTH('dbo.Employees', 'Position') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD [Position] NVARCHAR(150) NULL;
END;

IF COL_LENGTH('dbo.Employees', 'PervasiveDept') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD PervasiveDept NVARCHAR(30) NULL;
END;

IF COL_LENGTH('dbo.Employees', 'PervasiveShift') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD PervasiveShift NVARCHAR(30) NULL;
END;

IF COL_LENGTH('dbo.Employees', 'WorkScheduleCode') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD WorkScheduleCode INT NULL;
END;

IF COL_LENGTH('dbo.Employees', 'OvertimeAuthorized') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD OvertimeAuthorized BIT NOT NULL CONSTRAINT DF_Employees_OvertimeAuthorized_Default DEFAULT (0);
END;

IF COL_LENGTH('dbo.Employees', 'StandardShiftHours') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD StandardShiftHours DECIMAL(5,2) NOT NULL CONSTRAINT DF_Employees_StandardShiftHours_Default DEFAULT (9.60);
END;

IF COL_LENGTH('dbo.Employees', 'Photo') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD Photo VARBINARY(MAX) NULL;
END;

IF COL_LENGTH('dbo.Employees', 'PhotoContentType') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD PhotoContentType NVARCHAR(100) NULL;
END;

IF COL_LENGTH('dbo.Employees', 'PhotoUpdatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD PhotoUpdatedAt DATETIME2 NULL;
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

IF NOT EXISTS (SELECT 1 FROM dbo.WorkSchedules WHERE Code = 1)
BEGIN
    INSERT INTO dbo.WorkSchedules(Code, [Name], StartTime, EndTime, StandardHours, IsActive)
    VALUES (1, N'Horario 1', '08:00', '17:36', 9.60, 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.WorkSchedules WHERE Code = 2)
BEGIN
    INSERT INTO dbo.WorkSchedules(Code, [Name], StartTime, EndTime, StandardHours, IsActive)
    VALUES (2, N'Horario 2', '09:00', '18:36', 9.60, 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.WorkSchedules WHERE Code = 3)
BEGIN
    INSERT INTO dbo.WorkSchedules(Code, [Name], StartTime, EndTime, StandardHours, IsActive)
    VALUES (3, N'Horario 3', '18:00', '07:00', 9.60, 1);
END;

IF EXISTS (
    SELECT 1
    FROM dbo.WorkSchedules
    WHERE Code = 3
      AND StartTime <= EndTime
)
BEGIN
    UPDATE dbo.WorkSchedules
    SET StartTime = '18:00',
        EndTime = '07:00',
        StandardHours = 9.60,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Code = 3;
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

IF COL_LENGTH('dbo.PunchLogs', 'InOutMode') IS NULL
BEGIN
    ALTER TABLE dbo.PunchLogs ADD InOutMode INT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SyncState WHERE Id = 1)
BEGIN
    INSERT INTO dbo.SyncState(Id, LastSync, LastRun, LastStatus, LastMessage)
    VALUES (1, NULL, NULL, 'N/A', 'Sin sincronizacion ejecutada.');
END;";

        await using var cmd = new SqlCommand(schemaSql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<DateTime?> GetLastSyncAsync(CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT LastSync FROM dbo.SyncState WHERE Id = 1;";
        await using var cmd = new SqlCommand(sql, connection);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);

        if (result is null || result is DBNull)
        {
            return null;
        }

        return Convert.ToDateTime(result);
    }

    private static async Task<int> ApplySqlCorrectiveCleanupAsync(
        SqlConnection connection,
        SqlTransaction tx,
        DateTime fromDate,
        int maxGapSeconds,
        CancellationToken cancellationToken)
    {
        var totalDeleted = 0;

        if (maxGapSeconds > 0)
        {
            const string sqlByMode = @"
;WITH Ordered AS
(
    SELECT
        Id,
        CheckTime,
        LAG(CheckTime) OVER
        (
            PARTITION BY UserId, CAST(CheckTime AS date), ISNULL(InOutMode, -999)
            ORDER BY CheckTime, Id
        ) AS PrevCheckTime
    FROM dbo.PunchLogs
    WHERE CheckTime >= @FromDate
),
ToDelete AS
(
    SELECT Id
    FROM Ordered
    WHERE PrevCheckTime IS NOT NULL
      AND DATEDIFF(SECOND, PrevCheckTime, CheckTime) >= 0
      AND DATEDIFF(SECOND, PrevCheckTime, CheckTime) < @MaxGapSeconds
)
DELETE p
FROM dbo.PunchLogs p
INNER JOIN ToDelete d ON d.Id = p.Id;

SELECT @@ROWCOUNT;";

            await using var cmdByMode = new SqlCommand(sqlByMode, connection, tx);
            cmdByMode.Parameters.AddWithValue("@FromDate", fromDate);
            cmdByMode.Parameters.AddWithValue("@MaxGapSeconds", maxGapSeconds);
            var removedByModeObj = await cmdByMode.ExecuteScalarAsync(cancellationToken);
            totalDeleted += removedByModeObj is null or DBNull ? 0 : Convert.ToInt32(removedByModeObj);
        }

        const string sqlSameMinute = @"
;WITH Ranked AS
(
    SELECT
        Id,
        ROW_NUMBER() OVER
        (
            PARTITION BY UserId, CAST(CheckTime AS date), DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CheckTime), 0)
            ORDER BY
                CASE ISNULL(InOutMode, -1)
                    WHEN 0 THEN 1
                    WHEN 3 THEN 2
                    WHEN 1 THEN 3
                    WHEN 2 THEN 4
                    ELSE 5
                END,
                CheckTime,
                Id
        ) AS Rn
    FROM dbo.PunchLogs
    WHERE CheckTime >= @FromDate
),
ToDelete AS
(
    SELECT Id
    FROM Ranked
    WHERE Rn > 1
)
DELETE p
FROM dbo.PunchLogs p
INNER JOIN ToDelete d ON d.Id = p.Id;

SELECT @@ROWCOUNT;";

        await using var cmdSameMinute = new SqlCommand(sqlSameMinute, connection, tx);
        cmdSameMinute.Parameters.AddWithValue("@FromDate", fromDate);
        var removedSameMinuteObj = await cmdSameMinute.ExecuteScalarAsync(cancellationToken);
        totalDeleted += removedSameMinuteObj is null or DBNull ? 0 : Convert.ToInt32(removedSameMinuteObj);

        return totalDeleted;
    }

    private static async Task UpsertEmployeeAsync(SqlConnection connection, SqlTransaction? tx, int userId, string name, CancellationToken cancellationToken)
    {
        const string sql = @"
MERGE dbo.Employees AS target
USING (SELECT @UserId AS UserId, @EmployeeName AS EmployeeName) AS source
ON target.UserId = source.UserId
WHEN MATCHED THEN
    UPDATE SET
        EmployeeName =
            CASE
                WHEN target.EmployeeName IS NULL
                  OR LTRIM(RTRIM(target.EmployeeName)) = ''
                  OR target.EmployeeName LIKE 'Empleado[_]%'
                THEN source.EmployeeName
                ELSE target.EmployeeName
            END,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT(UserId, EmployeeName, Department, [Position], PervasiveDept, PervasiveShift, WorkScheduleCode, OvertimeAuthorized, StandardShiftHours, UpdatedAt)
    VALUES(source.UserId, source.EmployeeName, NULL, NULL, NULL, NULL, NULL, 0, 9.60, SYSUTCDATETIME());";

        await using var cmd = new SqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@EmployeeName", name);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertEmployeeAdminDataAsync(
        SqlConnection connection,
        SqlTransaction? tx,
        int userId,
        string name,
        string? department,
        string? position,
        string? pervasiveDept,
        string? pervasiveShift,
        int? workScheduleCode,
        bool overtimeAuthorized,
        CancellationToken cancellationToken)
    {
        var standardShiftHours = await ResolveStandardShiftHoursAsync(connection, tx, workScheduleCode, cancellationToken);

        const string sql = @"
MERGE dbo.Employees AS target
USING (
    SELECT
        @UserId AS UserId,
        @EmployeeName AS EmployeeName,
        @Department AS Department,
        @Position AS [Position],
        @PervasiveDept AS PervasiveDept,
        @PervasiveShift AS PervasiveShift,
        @WorkScheduleCode AS WorkScheduleCode,
        @OvertimeAuthorized AS OvertimeAuthorized,
        @StandardShiftHours AS StandardShiftHours
) AS source
ON target.UserId = source.UserId
WHEN MATCHED THEN
    UPDATE SET
        EmployeeName = source.EmployeeName,
        Department = source.Department,
        [Position] = source.[Position],
        PervasiveDept = source.PervasiveDept,
        PervasiveShift = source.PervasiveShift,
        WorkScheduleCode = source.WorkScheduleCode,
        OvertimeAuthorized = source.OvertimeAuthorized,
        StandardShiftHours = source.StandardShiftHours,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT(UserId, EmployeeName, Department, [Position], PervasiveDept, PervasiveShift, WorkScheduleCode, OvertimeAuthorized, StandardShiftHours, UpdatedAt)
    VALUES(source.UserId, source.EmployeeName, source.Department, source.[Position], source.PervasiveDept, source.PervasiveShift, source.WorkScheduleCode, source.OvertimeAuthorized, source.StandardShiftHours, SYSUTCDATETIME());";

        await using var cmd = new SqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@EmployeeName", name);
        cmd.Parameters.AddWithValue("@Department", string.IsNullOrWhiteSpace(department) ? DBNull.Value : department.Trim());
        cmd.Parameters.AddWithValue("@Position", string.IsNullOrWhiteSpace(position) ? DBNull.Value : position.Trim());
        cmd.Parameters.AddWithValue("@PervasiveDept", string.IsNullOrWhiteSpace(pervasiveDept) ? DBNull.Value : pervasiveDept.Trim());
        cmd.Parameters.AddWithValue("@PervasiveShift", string.IsNullOrWhiteSpace(pervasiveShift) ? DBNull.Value : pervasiveShift.Trim());
        cmd.Parameters.AddWithValue("@WorkScheduleCode", workScheduleCode.HasValue ? workScheduleCode.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@OvertimeAuthorized", overtimeAuthorized);
        cmd.Parameters.AddWithValue("@StandardShiftHours", standardShiftHours);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TryInsertPunchAsync(SqlConnection connection, SqlTransaction tx, PunchRecord punch, string source, CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO dbo.PunchLogs(UserId, EmployeeName, CheckTime, InOutMode, Source)
VALUES(
    @UserId,
    COALESCE(
        (SELECT TOP 1 NULLIF(LTRIM(RTRIM(e.EmployeeName)), '') FROM dbo.Employees e WHERE e.UserId = @UserId),
        NULLIF(LTRIM(RTRIM(@EmployeeName)), ''),
        CONCAT('Empleado_', @UserId)
    ),
    @CheckTime,
    @InOutMode,
    @Source
);";

        await using var cmd = new SqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("@UserId", punch.UserId);
        cmd.Parameters.AddWithValue("@EmployeeName", punch.EmployeeName);
        cmd.Parameters.AddWithValue("@CheckTime", punch.CheckTime);
        cmd.Parameters.AddWithValue("@InOutMode", punch.InOutMode.HasValue ? punch.InOutMode.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Source", source);

        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            return false;
        }
    }

    private static async Task UpsertSyncStateAsync(SqlConnection connection, SqlTransaction tx, DateTime? lastSync, string status, string message, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE dbo.SyncState
SET LastSync = @LastSync,
    LastRun = SYSUTCDATETIME(),
    LastStatus = @Status,
    LastMessage = @Message
WHERE Id = 1;";

        await using var cmd = new SqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("@LastSync", lastSync.HasValue ? lastSync.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@Message", message);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SaveErrorSyncStateAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureReadyAsync(cancellationToken);
            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
UPDATE dbo.SyncState
SET LastRun = SYSUTCDATETIME(),
    LastStatus = 'ERROR',
    LastMessage = @Message
WHERE Id = 1;";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Message", message.Length > 1900 ? message[..1900] : message);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // no-op
        }
    }

    private async Task<Dictionary<int, (string Name, DateTime? UpdatedAt, bool HasPhoto, string? Department, string? Position, string? PervasiveDept, string? PervasiveShift, int? WorkScheduleCode, bool OvertimeAuthorized, decimal StandardShiftHours)>> GetSqlEmployeesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        return await GetSqlEmployeesAsync(connection, null, cancellationToken);
    }

    private static async Task<Dictionary<int, (string Name, DateTime? UpdatedAt, bool HasPhoto, string? Department, string? Position, string? PervasiveDept, string? PervasiveShift, int? WorkScheduleCode, bool OvertimeAuthorized, decimal StandardShiftHours)>> GetSqlEmployeesAsync(
        SqlConnection connection,
        SqlTransaction? tx,
        CancellationToken cancellationToken)
    {

        const string sql = @"
SELECT
    UserId,
    EmployeeName,
    Department,
    [Position],
    PervasiveDept,
    PervasiveShift,
    WorkScheduleCode,
    OvertimeAuthorized,
    StandardShiftHours,
    UpdatedAt,
    CASE WHEN Photo IS NULL THEN 0 ELSE 1 END AS HasPhoto
FROM dbo.Employees;";
        await using var cmd = new SqlCommand(sql, connection, tx);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var map = new Dictionary<int, (string Name, DateTime? UpdatedAt, bool HasPhoto, string? Department, string? Position, string? PervasiveDept, string? PervasiveShift, int? WorkScheduleCode, bool OvertimeAuthorized, decimal StandardShiftHours)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var department = await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetString(2);
            var position = await reader.IsDBNullAsync(3, cancellationToken) ? null : reader.GetString(3);
            var pervasiveDept = await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetString(4);
            var pervasiveShift = await reader.IsDBNullAsync(5, cancellationToken) ? null : reader.GetString(5);
            var workScheduleCode = await reader.IsDBNullAsync(6, cancellationToken) ? (int?)null : reader.GetInt32(6);
            var overtimeAuthorized = !await reader.IsDBNullAsync(7, cancellationToken) && reader.GetBoolean(7);
            var standardShiftHours = await reader.IsDBNullAsync(8, cancellationToken) ? 9.60m : reader.GetDecimal(8);
            DateTime? updatedAt = await reader.IsDBNullAsync(9, cancellationToken) ? (DateTime?)null : reader.GetDateTime(9);
            var hasPhoto = !await reader.IsDBNullAsync(10, cancellationToken) && reader.GetInt32(10) == 1;
            map[reader.GetInt32(0)] = (reader.GetString(1), updatedAt, hasPhoto, department, position, pervasiveDept, pervasiveShift, workScheduleCode, overtimeAuthorized, standardShiftHours);
        }

        return map;
    }

    private static async Task<Dictionary<int, int>> GetEmployeeReassignMapAsync(
        SqlConnection connection,
        SqlTransaction? tx,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT OldUserId, NewUserId
FROM dbo.EmployeeReassignMap;";

        await using var cmd = new SqlCommand(sql, connection, tx);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var map = new Dictionary<int, int>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var oldUserId = reader.GetInt32(0);
            var newUserId = reader.GetInt32(1);
            if (oldUserId > 0 && newUserId > 0 && oldUserId != newUserId)
            {
                map[oldUserId] = newUserId;
            }
        }

        return map;
    }

    private static async Task<Dictionary<int, DateTime>> GetSuppressedEmployeesAsync(
        SqlConnection connection,
        SqlTransaction? tx,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT UserId, UpdatedAt
FROM dbo.EmployeeSuppression;";

        await using var cmd = new SqlCommand(sql, connection, tx);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var map = new Dictionary<int, DateTime>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var userId = reader.GetInt32(0);
            var updatedAt = await reader.IsDBNullAsync(1, cancellationToken)
                ? DateTime.MinValue
                : reader.GetDateTime(1);
            if (userId > 0 && !map.ContainsKey(userId))
            {
                map[userId] = updatedAt;
            }
        }

        return map;
    }

    private static async Task UpsertSuppressedEmployeeAsync(
        SqlConnection connection,
        SqlTransaction? tx,
        int userId,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string sql = @"
MERGE dbo.EmployeeSuppression AS target
USING (
    SELECT @UserId AS UserId, @Reason AS Reason
) AS source
ON target.UserId = source.UserId
WHEN MATCHED THEN
    UPDATE SET
        Reason = COALESCE(NULLIF(source.Reason, ''), target.Reason),
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (UserId, Reason, UpdatedAt)
    VALUES (source.UserId, source.Reason, SYSUTCDATETIME());";

        await using var cmd = new SqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Reason", string.IsNullOrWhiteSpace(reason) ? (object)DBNull.Value : reason.Trim());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RemoveSuppressedEmployeesAsync(
        SqlConnection connection,
        SqlTransaction? tx,
        IEnumerable<int> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return;
        }

        foreach (var userId in ids)
        {
            await using var cmd = new SqlCommand("DELETE FROM dbo.EmployeeSuppression WHERE UserId = @UserId;", connection, tx);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<int> ExecuteScalarIntAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(sql, connection);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static async Task<DateTime?> ExecuteScalarDateTimeAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(sql, connection);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        if (value is null || value is DBNull)
        {
            return null;
        }

        return Convert.ToDateTime(value);
    }

    private static async Task<decimal> ResolveStandardShiftHoursAsync(SqlConnection connection, SqlTransaction? tx, int? workScheduleCode, CancellationToken cancellationToken)
    {
        if (!workScheduleCode.HasValue)
        {
            return 9.60m;
        }

        const string sql = @"
SELECT TOP 1 StandardHours
FROM dbo.WorkSchedules
WHERE Code = @Code;";

        await using var cmd = new SqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("@Code", workScheduleCode.Value);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        if (value is null || value is DBNull)
        {
            return 9.60m;
        }

        return Convert.ToDecimal(value);
    }

    private static bool IsTimeoutLikeException(Exception ex)
    {
        if (ex is OdbcException odbcEx)
        {
            foreach (OdbcError error in odbcEx.Errors)
            {
                if (string.Equals(error.SQLState, "HYT00", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(error.SQLState, "HYT01", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        var message = ex.Message ?? string.Empty;
        return message.Contains("tiempo de espera", StringComparison.OrdinalIgnoreCase)
               || message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
               || message.Contains("time-out", StringComparison.OrdinalIgnoreCase);
    }

    private static string FlattenExceptionMessages(Exception ex)
    {
        var parts = new List<string>();
        Exception? current = ex;
        while (current is not null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                parts.Add($"{current.GetType().Name}: {current.Message}");
            }

            current = current.InnerException;
        }

        return parts.Count == 0 ? ex.ToString() : string.Join(" | ", parts);
    }

    private async Task<PervasiveMirrorResult> MirrorWithRetriesAsync(
        IReadOnlyList<EmployeeInfo> employees,
        IReadOnlyList<PunchRecord> punches,
        IReadOnlyDictionary<int, (string? Department, string? Shift)> mirrorMeta,
        CancellationToken cancellationToken)
    {
        const int mirrorAttempts = 6;
        Exception? lastMirrorException = null;

        for (var attempt = 1; attempt <= mirrorAttempts; attempt++)
        {
            try
            {
                return await _pervasiveMirror!.MirrorSnapshotAsync(employees, punches, mirrorMeta, cancellationToken);
            }
            catch (Exception ex) when (IsTimeoutLikeException(ex) && attempt < mirrorAttempts)
            {
                lastMirrorException = ex;
                await Task.Delay(2000 * attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                lastMirrorException = ex;
                break;
            }
        }

        throw lastMirrorException ?? new InvalidOperationException("Error desconocido en espejo Pervasive.");
    }

    private static IReadOnlyList<PunchRecord> BuildRecentMirrorSubset(IReadOnlyList<PunchRecord> punches, int maxGroups)
    {
        if (punches.Count == 0)
        {
            return punches;
        }

        return punches
            .OrderByDescending(p => p.CheckTime)
            .GroupBy(p => (p.UserId, Date: p.CheckTime.Date))
            .Take(Math.Max(1, maxGroups))
            .SelectMany(g => g.OrderBy(p => p.CheckTime))
            .ToList();
    }

    private static DateTime MinDate(DateTime a, DateTime b)
        => a <= b ? a : b;

    private static IReadOnlyList<PunchRecord> BuildMirrorCandidatePunches(
        IReadOnlyList<PunchRecord> allPunches,
        IReadOnlyList<PunchRecord> insertedPunches,
        int maxGroups)
    {
        if (insertedPunches.Count == 0)
        {
            return Array.Empty<PunchRecord>();
        }

        var affectedUsers = insertedPunches.Select(p => p.UserId).Distinct().ToHashSet();
        var minDate = insertedPunches.Min(p => p.CheckTime).Date.AddDays(-1);

        var scoped = allPunches
            .Where(p => affectedUsers.Contains(p.UserId) && p.CheckTime.Date >= minDate)
            .OrderByDescending(p => p.CheckTime)
            .GroupBy(p => (p.UserId, Date: p.CheckTime.Date))
            .Take(Math.Max(1, maxGroups))
            .SelectMany(g => g.OrderBy(p => p.CheckTime))
            .ToList();

        return scoped.Count > 0 ? scoped : insertedPunches;
    }

    private static async Task<IReadOnlyList<PunchRecord>> GetSqlPunchesForMirrorAsync(
        SqlConnection connection,
        SqlTransaction tx,
        DateTime fromDate,
        int maxRows,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT TOP (@TopRows)
    p.UserId,
    COALESCE(NULLIF(e.EmployeeName, ''), p.EmployeeName) AS EmployeeName,
    p.CheckTime,
    p.InOutMode
FROM dbo.PunchLogs p
LEFT JOIN dbo.Employees e ON e.UserId = p.UserId
WHERE p.CheckTime >= @FromDate
ORDER BY p.CheckTime DESC;";

        await using var cmd = new SqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("@TopRows", Math.Max(1, maxRows));
        cmd.Parameters.AddWithValue("@FromDate", fromDate);

        var rows = new List<PunchRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PunchRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                await reader.IsDBNullAsync(3, cancellationToken) ? (int?)null : reader.GetInt32(3)));
        }

        return rows;
    }

    private static async Task<(List<PunchRecord> Rows, int TotalRows)> GetSqlNightShiftPunchesForCleanupAsync(
        SqlConnection connection,
        DateTime fromDate,
        DateTime toDate,
        int? userId,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var explicitUserId = userId.GetValueOrDefault();
        var forceNightForExplicitUser = explicitUserId > 0;

        const string whereNightShift = @"
(
    e.WorkScheduleCode = 3
    OR LEFT(LTRIM(RTRIM(ISNULL(e.PervasiveShift, ''))), 1) = '3'
)";

        var baseSql = $@"
FROM dbo.PunchLogs p
INNER JOIN dbo.Employees e ON e.UserId = p.UserId
WHERE p.CheckTime >= @FromDate
  AND p.CheckTime < @ToDate
  AND (
        (@UserId > 0 AND p.UserId = @UserId)
        OR {whereNightShift}
      )
  AND (@UserId <= 0 OR p.UserId = @UserId)";

        var countSql = $"SELECT COUNT(1) {baseSql};";
        await using var countCmd = new SqlCommand(countSql, connection);
        countCmd.Parameters.AddWithValue("@FromDate", fromDate);
        countCmd.Parameters.AddWithValue("@ToDate", toDate.AddDays(1));
        countCmd.Parameters.AddWithValue("@UserId", userId.GetValueOrDefault());
        var totalObj = await countCmd.ExecuteScalarAsync(cancellationToken);
        var totalRows = totalObj is null or DBNull ? 0 : Convert.ToInt32(totalObj);

        var dataSql = $@"
SELECT TOP (@TopRows)
    p.UserId,
    COALESCE(NULLIF(e.EmployeeName, ''), p.EmployeeName) AS EmployeeName,
    p.CheckTime,
    p.InOutMode,
    e.WorkScheduleCode,
    CAST(CASE
        WHEN @ForceNight = 1
          OR e.WorkScheduleCode = 3
          OR LEFT(LTRIM(RTRIM(ISNULL(e.PervasiveShift, ''))), 1) = '3'
        THEN 1 ELSE 0 END AS bit) AS IsNightShift
{baseSql}
ORDER BY p.CheckTime DESC;";

        await using var cmd = new SqlCommand(dataSql, connection);
        cmd.Parameters.AddWithValue("@TopRows", Math.Max(1, maxRows));
        cmd.Parameters.AddWithValue("@FromDate", fromDate);
        cmd.Parameters.AddWithValue("@ToDate", toDate.AddDays(1));
        cmd.Parameters.AddWithValue("@UserId", explicitUserId);
        cmd.Parameters.AddWithValue("@ForceNight", forceNightForExplicitUser ? 1 : 0);

        var rows = new List<PunchRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var workScheduleCode = await reader.IsDBNullAsync(4, cancellationToken) ? (int?)null : reader.GetInt32(4);
            var isNightShift = !await reader.IsDBNullAsync(5, cancellationToken) && reader.GetBoolean(5);
            if (forceNightForExplicitUser)
            {
                workScheduleCode ??= 3;
                isNightShift = true;
            }

            rows.Add(new PunchRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                await reader.IsDBNullAsync(3, cancellationToken) ? (int?)null : reader.GetInt32(3),
                workScheduleCode,
                null,
                null,
                isNightShift));
        }

        return (rows, totalRows);
    }

    private static void ValidateEmployeeReassignInput(int oldUserId, int newUserId, DateTime fromDate, DateTime toDate)
    {
        if (oldUserId <= 0 || newUserId <= 0)
        {
            throw new InvalidOperationException("USERID origen/destino invalido.");
        }

        if (fromDate.Date > toDate.Date)
        {
            throw new InvalidOperationException("Rango de fechas invalido. La fecha inicial no puede ser mayor a la final.");
        }
    }

    private static string FormatEmployeeCode(int userId)
    {
        if (userId < 0)
        {
            userId = 0;
        }

        return userId.ToString("D5");
    }

    private static string BuildDeviceSafeName(string fullName)
    {
        var normalized = fullName.Trim();
        if (normalized.Length == 0)
        {
            return normalized;
        }

        const int maxDeviceChars = 24;
        return normalized.Length <= maxDeviceChars
            ? normalized
            : normalized[..maxDeviceChars].TrimEnd();
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static async Task<int> ExecuteCountSqlAsync(
        SqlConnection connection,
        string sql,
        int userId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@FromDate", fromDate);
        cmd.Parameters.AddWithValue("@ToDate", toDate);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static async Task<int> ExecuteConflictCountSqlAsync(
        SqlConnection connection,
        int oldUserId,
        int newUserId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT COUNT(1)
FROM dbo.PunchLogs o
WHERE o.UserId = @OldUserId
  AND o.CheckTime >= @FromDate
  AND o.CheckTime <= @ToDate
  AND EXISTS
      (
          SELECT 1
          FROM dbo.PunchLogs n
          WHERE n.UserId = @NewUserId
            AND n.CheckTime = o.CheckTime
      );";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@OldUserId", oldUserId);
        cmd.Parameters.AddWithValue("@NewUserId", newUserId);
        cmd.Parameters.AddWithValue("@FromDate", fromDate);
        cmd.Parameters.AddWithValue("@ToDate", toDate);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static async Task<IReadOnlyList<DateTime>> GetSqlAffectedDatesAsync(
        SqlConnection connection,
        int oldUserId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT DISTINCT CAST(CheckTime AS date) AS WorkDate
FROM dbo.PunchLogs
WHERE UserId = @OldUserId
  AND CheckTime >= @FromDate
  AND CheckTime <= @ToDate
ORDER BY WorkDate;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@OldUserId", oldUserId);
        cmd.Parameters.AddWithValue("@FromDate", fromDate);
        cmd.Parameters.AddWithValue("@ToDate", toDate);

        var dates = new List<DateTime>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            dates.Add(reader.GetDateTime(0).Date);
        }

        return dates;
    }
}
