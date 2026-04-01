using System.Data.Odbc;
using System.Diagnostics;
using Checador.Domain.Models;

namespace Checador.Infrastructure.Access;

public sealed class PervasiveAttendanceMirrorService
{
    private static readonly DateTime PervasiveEpoch = new(1968, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly TimeSpan MiddayBoundary = TimeSpan.FromHours(12);
    private static readonly TimeSpan DayShiftEarliestEntryBoundary = TimeSpan.FromHours(4);
    private const int MinBreakOutAfterEntryMinutes = 90;
    private const int MinBreakOutBeforeExitMinutes = 60;
    private const int MaxClearlyValidShiftSpanMinutes = 1080;
    private const string FixedLastChangeProgram = "OLL302";
    private const int MinChronologicalFallbackSpanMinutes = 240;
    private const int MinUnknownEntryFallbackGapMinutes = 180;
    private const int TimeoutRetryAttempts = 3;
    private const int TimeoutRetryDelayMs = 900;
    private static readonly Dictionary<string, string> DeptShiftCodeByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = "0",
        ["RHH"] = "0",
        ["1"] = "1",
        ["PROD"] = "1",
        ["2"] = "2",
        ["ADMI"] = "2",
        ["3"] = "3",
        ["LOGI"] = "3",
        ["4"] = "4",
        ["VENT"] = "4",
        ["5"] = "5",
        ["MANF"] = "5",
        ["6"] = "6",
        ["ENSA"] = "6",
        ["7"] = "7",
        ["CALI"] = "7",
        ["8"] = "8",
        ["DIR"] = "8",
        ["9"] = "9",
        ["MTTO"] = "9",
        ["MTOO"] = "9",
        ["A"] = "A",
        ["PRACT"] = "A"
    };
    private static readonly Dictionary<string, string> DeptLabelByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = "RHH",
        ["1"] = "PROD",
        ["2"] = "ADMI",
        ["3"] = "LOGI",
        ["4"] = "VENT",
        ["5"] = "MANF",
        ["6"] = "ENSA",
        ["7"] = "CALI",
        ["8"] = "DIR",
        ["9"] = "MTTO",
        ["A"] = "PRACT"
    };
    private readonly PervasiveSyncOptions _options;

    public PervasiveAttendanceMirrorService(PervasiveSyncOptions options)
    {
        _options = options;
    }

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ConnectionString);

    public bool StopOnFailure => _options.StopSyncOnMirrorFailure;

    private bool IsSimulationMode => _options.SimulationMode;

    private bool OnlyCurrentDayWrites => _options.OnlyCurrentDayWrites;

    private bool CloseOnlyWithFinalOutMode => _options.CloseOnlyWithFinalOutMode;

    private int MinimumOutGapMinutes => Math.Max(0, _options.MinimumOutGapMinutes);

    private int MaxRowsPerRun => _options.MaxRowsPerRun <= 0 ? int.MaxValue : _options.MaxRowsPerRun;

    private string MirrorProgramName => string.IsNullOrWhiteSpace(_options.ProgramName) ? FixedLastChangeProgram : _options.ProgramName.Trim();

    public async Task<PervasiveMirrorResult> MirrorSnapshotAsync(
        IReadOnlyList<EmployeeInfo> employees,
        IReadOnlyList<PunchRecord> punches,
        IReadOnlyDictionary<int, (string? Department, string? Shift)>? employeeMeta = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new PervasiveMirrorResult(0, 0, 0, "Espejo Pervasive deshabilitado.");
        }

        var attendanceTable = EscapeIdentifier(_options.AttendanceTable, "TIME_ATTENDANCE");
        var employeeNameById = employees
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.First().EmployeeName);

        var grouped = punches
            .OrderBy(p => p.CheckTime)
            .GroupBy(p => (p.UserId, Date: p.CheckTime.Date))
            .OrderByDescending(g => g.Key.Date)
            .ThenBy(g => g.Key.UserId)
            .ToList();

        if (OnlyCurrentDayWrites)
        {
            grouped = grouped.Where(g => g.Key.Date == DateTime.Today).ToList();
        }

        if (grouped.Count > MaxRowsPerRun)
        {
            return new PervasiveMirrorResult(0, 0, grouped.Count,
                $"Espejo Pervasive ABORTADO por candado: filas de dÃ­a={grouped.Count} superan MaxRowsPerRun={MaxRowsPerRun}. Sin escrituras.");
        }

        var inserted = 0;
        var updated = 0;
        var correctiveUpdated = 0;
        var skipped = 0;
        var timeoutSkipped = 0;
        var timeoutByStage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await using var connection = new OdbcConnection(_options.ConnectionString);
        var openSw = Stopwatch.StartNew();
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            openSw.Stop();
            throw new InvalidOperationException($"Pervasive: fallo abriendo conexiÃ³n ({openSw.ElapsedMilliseconds} ms). {ex.Message}", ex);
        }

        foreach (var dayGroup in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userId = dayGroup.Key.UserId;
            var ordered = dayGroup.OrderBy(x => x.CheckTime).ToList();
            if (ordered.Count == 0)
            {
                skipped++;
                continue;
            }

            var entryPunch = ResolveMirrorEntryPunch(ordered);
            if (entryPunch is null)
            {
                skipped++;
                continue;
            }

            var segments = BuildMirrorSegments(ordered);
            if (segments.Count == 0)
            {
                skipped++;
                continue;
            }

            var mainPunch = entryPunch;
            var employeeCode = FormatEmployeeId(userId);
            var meta = (Department: (string?)null, Shift: (string?)null);
            if (employeeMeta is not null && employeeMeta.TryGetValue(userId, out var loadedMeta))
            {
                meta = loadedMeta;
            }
            var deptLabel = NormalizeDeptLabel(meta.Department, _options.DefaultDepartment);
            var shiftCode = NormalizeShiftCode(meta.Shift, _options.DefaultShift);

            var employeeName = employeeNameById.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : mainPunch.EmployeeName;

            var completed = false;
            for (var attempt = 1; attempt <= TimeoutRetryAttempts && !completed; attempt++)
            {
                var tx = (OdbcTransaction)await connection.BeginTransactionAsync(cancellationToken);
                var stage = "inicio";
                try
                {
                    foreach (var segment in segments)
                    {
                        stage = "ExistsAttendanceSegment";
                        var existsSegment = await ExistsAttendanceSegmentByInAsync(
                            connection,
                            tx,
                            attendanceTable,
                            employeeCode,
                            segment.InTime,
                            cancellationToken);

                        if (IsSimulationMode)
                        {
                            if (existsSegment)
                            {
                                updated++;
                            }
                            else
                            {
                                inserted++;
                            }

                            continue;
                        }

                        if (!existsSegment)
                        {
                            stage = "InsertAttendanceEntry";
                            await InsertAttendanceEntryAsync(
                                connection,
                                tx,
                                attendanceTable,
                                employeeCode,
                                employeeName,
                                deptLabel,
                                shiftCode,
                                segment.InTime,
                                segment.OutTime,
                                cancellationToken);
                            inserted++;
                            continue;
                        }

                        stage = "UpdateAttendanceSegment";
                        var changed = await UpdateAttendanceSegmentByInAsync(
                            connection,
                            tx,
                            attendanceTable,
                            employeeCode,
                            employeeName,
                            deptLabel,
                            shiftCode,
                            segment.InTime,
                            segment.InTime,
                            segment.OutTime,
                            cancellationToken);

                        if (changed)
                        {
                            updated++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }

                    if (IsSimulationMode)
                    {
                        await tx.RollbackAsync(cancellationToken);
                    }
                    else
                    {
                        await tx.CommitAsync(cancellationToken);
                    }

                    completed = true;
                }
                catch (Exception ex) when (IsTimeoutException(ex) && attempt < TimeoutRetryAttempts)
                {
                    if (!timeoutByStage.TryAdd(stage, 1))
                    {
                        timeoutByStage[stage]++;
                    }
                    try { await tx.RollbackAsync(cancellationToken); } catch { }
                    await Task.Delay(TimeoutRetryDelayMs * attempt, cancellationToken);
                }
                catch (Exception ex) when (IsTimeoutException(ex))
                {
                    if (!timeoutByStage.TryAdd(stage, 1))
                    {
                        timeoutByStage[stage]++;
                    }
                    try { await tx.RollbackAsync(cancellationToken); } catch { }
                    timeoutSkipped++;
                    skipped++;
                    completed = true;
                }
                catch
                {
                    try { await tx.RollbackAsync(cancellationToken); } catch { }
                    throw;
                }
            }
        }

        var modeLabel = IsSimulationMode ? "SIMULACIÃ“N" : "ESCRITURA";
        var timeoutStagesText = timeoutByStage.Count == 0
            ? "-"
            : string.Join(", ", timeoutByStage.OrderByDescending(x => x.Value).Select(x => $"{x.Key}:{x.Value}"));
        return new PervasiveMirrorResult(inserted, updated, skipped,
            $"Espejo Pervasive {modeLabel}. Insertados={inserted}, actualizados={updated}, actualizados_correctivo={correctiveUpdated}, omitidos={skipped} (timeouts omitidos: {timeoutSkipped}; etapas: {timeoutStagesText}). Candados: current-day={OnlyCurrentDayWrites}, close-final-only={CloseOnlyWithFinalOutMode}, min-out-gap-min={MinimumOutGapMinutes}, max-rows={MaxRowsPerRun}.");
    }

    public async Task<IReadOnlyList<PervasivePayloadPreviewItem>> BuildPreviewAsync(
        IReadOnlyList<EmployeeInfo> employees,
        IReadOnlyList<PunchRecord> punches,
        IReadOnlyDictionary<int, (string? Department, string? Shift)>? employeeMeta = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return Array.Empty<PervasivePayloadPreviewItem>();
        }

        var attendanceTable = EscapeIdentifier(_options.AttendanceTable, "TIME_ATTENDANCE");
        var employeeNameById = employees
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.First().EmployeeName);

        var grouped = punches
            .OrderBy(p => p.CheckTime)
            .GroupBy(p => (p.UserId, Date: p.CheckTime.Date))
            .ToList();

        var writableGroups = OnlyCurrentDayWrites
            ? grouped.Where(g => g.Key.Date == DateTime.Today).ToList()
            : grouped;

        if (writableGroups.Count > MaxRowsPerRun)
        {
            return new List<PervasivePayloadPreviewItem>
            {
                new(
                    0,
                    "ABORT_GUARD",
                    "00000",
                    "(candado)",
                    DateTime.Today,
                    0m,
                    0m,
                    "00000000",
                    "000000",
                    "00000000",
                    "000000",
                    "N/D",
                    "N/D",
                    "N",
                    MirrorProgramName,
                    FormatActualDate(DateTime.Today),
                    $"Preview abortado por candado: filas de dÃ­a={writableGroups.Count} superan MaxRowsPerRun={MaxRowsPerRun}.")
            };
        }

        var rows = new List<PervasivePayloadPreviewItem>();

        await using var connection = new OdbcConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var dayGroup in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userId = dayGroup.Key.UserId;
            var ordered = dayGroup.OrderBy(x => x.CheckTime).ToList();
            if (ordered.Count == 0)
            {
                continue;
            }

            var employeeCode = FormatEmployeeId(userId);
            var employeeNameFromPunch = ordered.First().EmployeeName;
            var previewName = employeeNameById.TryGetValue(userId, out var loadedName) && !string.IsNullOrWhiteSpace(loadedName)
                ? loadedName
                : employeeNameFromPunch;

            if (OnlyCurrentDayWrites && dayGroup.Key.Date != DateTime.Today)
            {
                rows.Add(new PervasivePayloadPreviewItem(
                    userId,
                    "SKIP_GUARD",
                    employeeCode,
                    previewName,
                    dayGroup.Key.Date,
                    0m,
                    0m,
                    "00000000",
                    "000000",
                    "00000000",
                    "000000",
                    "N/D",
                    "N/D",
                    "N",
                    MirrorProgramName,
                    FormatActualDate(DateTime.Today),
                    "Candado activo: OnlyCurrentDayWrites=true. DÃ­a fuera de alcance; no se escribirÃ¡."));
                continue;
            }

            var entryPunch = ResolveMirrorEntryPunch(ordered);
            if (entryPunch is null)
            {
                rows.Add(new PervasivePayloadPreviewItem(
                    userId,
                    "SKIP_GUARD",
                    employeeCode,
                    previewName,
                    dayGroup.Key.Date,
                    0m,
                    0m,
                    "00000000",
                    "000000",
                    "00000000",
                    "000000",
                    "N/D",
                    "N/D",
                    "N",
                    MirrorProgramName,
                    FormatActualDate(DateTime.Today),
                    "Candado activo: no se pudo inferir una entrada valida (modo/cronologia insuficiente)."));
                continue;
            }

            var segments = BuildMirrorSegments(ordered);
            if (segments.Count == 0)
            {
                rows.Add(new PervasivePayloadPreviewItem(
                    userId,
                    "SKIP",
                    employeeCode,
                    previewName,
                    dayGroup.Key.Date,
                    0m,
                    0m,
                    "00000000",
                    "000000",
                    "00000000",
                    "000000",
                    "N/D",
                    "N/D",
                    "N",
                    MirrorProgramName,
                    FormatActualDate(DateTime.Today),
                    "Sin segmentos calculados para espejo Pervasive."));
                continue;
            }

            var meta = (Department: (string?)null, Shift: (string?)null);
            if (employeeMeta is not null && employeeMeta.TryGetValue(userId, out var loadedMeta))
            {
                meta = loadedMeta;
            }

            var deptLabel = NormalizeDeptLabel(meta.Department, _options.DefaultDepartment);
            var shiftCode = NormalizeShiftCode(meta.Shift, _options.DefaultShift);

            var employeeName = employeeNameById.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : ordered.First().EmployeeName;

            foreach (var segment in segments)
            {
                var existsSegment = await ExistsAttendanceSegmentByInAsync(
                    connection,
                    null,
                    attendanceTable,
                    employeeCode,
                    segment.InTime,
                    cancellationToken);

                var action = existsSegment ? "UPDATE" : "INSERT";
                var notes = existsSegment
                    ? "Segmento existente: se actualizaria."
                    : "Segmento nuevo: se insertaria.";

                if (segment.IsIntermediate)
                {
                    notes = $"{notes} Segmento de salida/entrada intermedia.";
                }

                if (!string.IsNullOrWhiteSpace(segment.Note))
                {
                    notes = $"{notes} {segment.Note}";
                }

                rows.Add(new PervasivePayloadPreviewItem(
                    userId,
                    action,
                    employeeCode,
                    employeeName,
                    segment.InTime.Date,
                    ToPervasiveMinutes(segment.InTime),
                    segment.OutTime.HasValue ? ToPervasiveMinutes(segment.OutTime.Value) : 0m,
                    FormatDate(segment.InTime),
                    FormatTime(segment.InTime),
                    segment.OutTime.HasValue ? FormatDate(segment.OutTime.Value) : "00000000",
                    segment.OutTime.HasValue ? FormatTime(segment.OutTime.Value) : "000000",
                    deptLabel,
                    shiftCode,
                    segment.OutTime.HasValue ? string.Empty : "Y",
                    MirrorProgramName,
                    FormatActualDate(DateTime.Today),
                    notes));
            }
        }

        return rows
            .OrderByDescending(r => r.ChargeDate)
            .ThenBy(r => r.UserId)
            .ToList();
    }

    public async Task<PervasiveMirrorResult> MirrorSnapshotFastFallbackAsync(
        IReadOnlyList<EmployeeInfo> employees,
        IReadOnlyList<PunchRecord> punches,
        IReadOnlyDictionary<int, (string? Department, string? Shift)>? employeeMeta = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new PervasiveMirrorResult(0, 0, 0, "Espejo Pervasive deshabilitado.");
        }

        var attendanceTable = EscapeIdentifier(_options.AttendanceTable, "TIME_ATTENDANCE");
        var employeeNameById = employees
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.First().EmployeeName);

        var grouped = punches
            .OrderByDescending(p => p.CheckTime)
            .GroupBy(p => (p.UserId, Date: p.CheckTime.Date))
            .OrderByDescending(g => g.Key.Date)
            .ThenBy(g => g.Key.UserId)
            .ToList();

        if (OnlyCurrentDayWrites)
        {
            grouped = grouped.Where(g => g.Key.Date == DateTime.Today).ToList();
        }

        if (grouped.Count > MaxRowsPerRun)
        {
            grouped = grouped.Take(MaxRowsPerRun).ToList();
        }

        var inserted = 0;
        var updated = 0;
        var correctiveUpdated = 0;
        var skipped = 0;

        await using var connection = new OdbcConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var dayGroup in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userId = dayGroup.Key.UserId;
            var ordered = dayGroup.OrderBy(x => x.CheckTime).ToList();
            if (ordered.Count == 0)
            {
                skipped++;
                continue;
            }

            var entryPunch = ResolveMirrorEntryPunch(ordered);
            if (entryPunch is null)
            {
                skipped++;
                continue;
            }

            var segments = BuildMirrorSegments(ordered);
            if (segments.Count == 0)
            {
                skipped++;
                continue;
            }

            var employeeCode = FormatEmployeeId(userId);
            var employeeName = employeeNameById.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : entryPunch.EmployeeName;

            var meta = (Department: (string?)null, Shift: (string?)null);
            if (employeeMeta is not null && employeeMeta.TryGetValue(userId, out var loadedMeta))
            {
                meta = loadedMeta;
            }

            var deptLabel = NormalizeDeptLabel(meta.Department, _options.DefaultDepartment);
            var shiftCode = NormalizeShiftCode(meta.Shift, _options.DefaultShift);

            var tx = (OdbcTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                if (IsSimulationMode)
                {
                    foreach (var segment in segments)
                    {
                        var existsSegment = await ExistsAttendanceSegmentByInAsync(
                            connection,
                            tx,
                            attendanceTable,
                            employeeCode,
                            segment.InTime,
                            cancellationToken);
                        if (existsSegment)
                        {
                            updated++;
                        }
                        else
                        {
                            inserted++;
                        }
                    }
                    await tx.RollbackAsync(cancellationToken);
                    continue;
                }

                foreach (var segment in segments)
                {
                    var existsSegment = await ExistsAttendanceSegmentByInAsync(
                        connection,
                        tx,
                        attendanceTable,
                        employeeCode,
                        segment.InTime,
                        cancellationToken);

                    if (!existsSegment)
                    {
                        await InsertAttendanceEntryAsync(
                            connection,
                            tx,
                            attendanceTable,
                            employeeCode,
                            employeeName,
                            deptLabel,
                            shiftCode,
                            segment.InTime,
                            segment.OutTime,
                            cancellationToken);
                        inserted++;
                        continue;
                    }

                    var changed = await UpdateAttendanceSegmentByInAsync(
                        connection,
                        tx,
                        attendanceTable,
                        employeeCode,
                        employeeName,
                        deptLabel,
                        shiftCode,
                        segment.InTime,
                        segment.InTime,
                        segment.OutTime,
                        cancellationToken);

                    if (changed)
                    {
                        updated++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                try { await tx.RollbackAsync(cancellationToken); } catch { }
                skipped++;
            }
        }

        return new PervasiveMirrorResult(inserted, updated, skipped,
            $"Espejo Pervasive CONTINGENCIA-RAPIDA. Insertados={inserted}, actualizados={updated}, actualizados_correctivo={correctiveUpdated}, omitidos={skipped}.");
    }

    public async Task<PervasiveEmployeeRemapPreviewResult> PreviewEmployeeRemapByDateRangeAsync(
        string oldEmployeeCode,
        string newEmployeeCode,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new PervasiveEmployeeRemapPreviewResult(false, 0, 0, 0, 0, Array.Empty<DateTime>(), "Espejo Pervasive deshabilitado.");
        }

        var attendanceTable = EscapeIdentifier(_options.AttendanceTable, "TIME_ATTENDANCE");
        var fromChargeDate = FormatDate(fromDate.Date);
        var toChargeDate = FormatDate(toDate.Date);

        await using var connection = new OdbcConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var oldRows = await ExecuteCountByEmployeeAndDateRangeAsync(connection, attendanceTable, oldEmployeeCode, fromChargeDate, toChargeDate, cancellationToken);
        var targetRows = await ExecuteCountByEmployeeAndDateRangeAsync(connection, attendanceTable, newEmployeeCode, fromChargeDate, toChargeDate, cancellationToken);
        var conflictRows = await ExecuteConflictCountByDateRangeAsync(connection, attendanceTable, oldEmployeeCode, newEmployeeCode, fromChargeDate, toChargeDate, cancellationToken);
        var affectedDates = await GetAffectedChargeDatesAsync(connection, attendanceTable, oldEmployeeCode, fromChargeDate, toChargeDate, cancellationToken);

        return new PervasiveEmployeeRemapPreviewResult(
            true,
            oldRows,
            targetRows,
            conflictRows,
            affectedDates.Count,
            affectedDates,
            $"Preview Pervasive OK. Filas origen: {oldRows}, destino: {targetRows}, conflictos: {conflictRows}, dÃ­as: {affectedDates.Count}.");
    }

    public async Task<PervasiveEmployeeRemapApplyResult> ApplyEmployeeRemapByDateRangeAsync(
        string oldEmployeeCode,
        string newEmployeeCode,
        DateTime fromDate,
        DateTime toDate,
        string? newName,
        string? newDept,
        string? newShift,
        bool allowSkipConflicts,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new PervasiveEmployeeRemapApplyResult(false, 0, 0, "Espejo Pervasive deshabilitado.");
        }

        var attendanceTable = EscapeIdentifier(_options.AttendanceTable, "TIME_ATTENDANCE");
        var fromChargeDate = FormatDate(fromDate.Date);
        var toChargeDate = FormatDate(toDate.Date);

        await using var connection = new OdbcConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var conflicts = await ExecuteConflictCountByDateRangeAsync(connection, attendanceTable, oldEmployeeCode, newEmployeeCode, fromChargeDate, toChargeDate, cancellationToken);
        if (!allowSkipConflicts && oldEmployeeCode != newEmployeeCode && conflicts > 0)
        {
            throw new InvalidOperationException($"Pervasive: existen {conflicts} conflictos por CHARGE_DATE en el rango seleccionado.");
        }

        await using var tx = (OdbcTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var sql = $@"
UPDATE {attendanceTable}
SET EMPLOYEE = ?,
    NAME = CASE WHEN ? IS NULL THEN NAME ELSE ? END,
    DEPT = CASE WHEN ? IS NULL THEN DEPT ELSE ? END,
    SHIFT = CASE WHEN ? IS NULL THEN SHIFT ELSE ? END
WHERE EMPLOYEE = ?
  AND CHARGE_DATE >= ?
  AND CHARGE_DATE <= ?";

            if (oldEmployeeCode != newEmployeeCode && allowSkipConflicts)
            {
                sql += $@"
  AND NOT EXISTS
      (
          SELECT 1
          FROM {attendanceTable} x
          WHERE x.EMPLOYEE = ?
            AND x.CHARGE_DATE = {attendanceTable}.CHARGE_DATE
      )";
            }

            await using var cmd = new OdbcCommand(sql, connection, tx);
            cmd.CommandTimeout = _options.CommandTimeoutSeconds;

            var normalizedName = NormalizeOptional(newName);
            var normalizedDept = NormalizeOptional(newDept);
            var normalizedShift = NormalizeOptional(newShift);

            cmd.Parameters.AddWithValue("@P1", newEmployeeCode);
            cmd.Parameters.AddWithValue("@P2", normalizedName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P3", normalizedName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P4", normalizedDept ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P5", normalizedDept ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P6", normalizedShift ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P7", normalizedShift ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@P8", oldEmployeeCode);
            cmd.Parameters.AddWithValue("@P9", fromChargeDate);
            cmd.Parameters.AddWithValue("@P10", toChargeDate);
            if (oldEmployeeCode != newEmployeeCode && allowSkipConflicts)
            {
                cmd.Parameters.AddWithValue("@P11", newEmployeeCode);
            }

            var updatedRows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            var skippedConflicts = oldEmployeeCode != newEmployeeCode && allowSkipConflicts ? conflicts : 0;
            return new PervasiveEmployeeRemapApplyResult(
                true,
                updatedRows,
                skippedConflicts,
                $"Pervasive actualizado. Filas: {updatedRows}. Conflictos omitidos: {skippedConflicts}.");
        }
        catch
        {
            try { await tx.RollbackAsync(cancellationToken); } catch { }
            throw;
        }
    }

    private IReadOnlyList<MirrorSegment> BuildMirrorSegments(IReadOnlyList<PunchRecord> ordered)
    {
        if (ordered.Count == 0)
        {
            return Array.Empty<MirrorSegment>();
        }

        var inPunch = ResolveMirrorEntryPunch(ordered);
        if (inPunch is null)
        {
            return Array.Empty<MirrorSegment>();
        }

        var salidaIntermedia = ordered
            .Where(x => x.CheckTime > inPunch.CheckTime)
            .FirstOrDefault(x => x.InOutMode == 2);

        if (!IsNightShiftContext(ordered) && salidaIntermedia is not null)
        {
            var gapAfterEntry = (salidaIntermedia.CheckTime - inPunch.CheckTime).TotalMinutes;
            if (gapAfterEntry < MinBreakOutAfterEntryMinutes)
            {
                salidaIntermedia = null;
            }
        }

        var entradaIntermedia = salidaIntermedia is not null
            ? ordered.Where(x => x.CheckTime > salidaIntermedia.CheckTime).FirstOrDefault(x => x.InOutMode == 3)
            : null;

        if (salidaIntermedia is not null && entradaIntermedia is not null && entradaIntermedia.CheckTime > salidaIntermedia.CheckTime)
        {
            var gapIntermedio = salidaIntermedia.CheckTime - inPunch.CheckTime;
            var gapToExitWindow = ordered[^1].CheckTime - salidaIntermedia.CheckTime;
            if ((MinimumOutGapMinutes <= 0 || gapIntermedio.TotalMinutes >= MinimumOutGapMinutes)
                && gapToExitWindow.TotalMinutes >= MinBreakOutBeforeExitMinutes)
            {
                var segment2Candidates = ordered
                    .Where(x => x.CheckTime >= entradaIntermedia.CheckTime)
                    .OrderBy(x => x.CheckTime)
                    .ToList();

                var (segment2Out, gapNote) = ResolveOutPunchCandidate(segment2Candidates, entradaIntermedia);
                return new List<MirrorSegment>
                {
                    new(inPunch.CheckTime, salidaIntermedia.CheckTime, false, "Segmento 1 con salida intermedia."),
                    new(entradaIntermedia.CheckTime, segment2Out?.CheckTime, true, gapNote)
                };
            }
        }

        var (outPunch, singleGapNote) = ResolveOutPunchCandidate(ordered, inPunch);
        return new List<MirrorSegment>
        {
            new(inPunch.CheckTime, outPunch?.CheckTime, false, singleGapNote)
        };
    }

    private async Task<bool> ExistsAttendanceSegmentByInAsync(
        OdbcConnection connection,
        OdbcTransaction? tx,
        string attendanceTable,
        string employeeCode,
        DateTime inTime,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT EMPLOYEE
FROM {attendanceTable}
WHERE EMPLOYEE = ?
  AND CHARGE_DATE = ?
  AND DATE_IN = ?
  AND TIME_IN = ?";

        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@P1", employeeCode);
        cmd.Parameters.AddWithValue("@P2", FormatDate(inTime.Date));
        cmd.Parameters.AddWithValue("@P3", FormatDate(inTime));
        cmd.Parameters.AddWithValue("@P4", FormatTime(inTime));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    private async Task<bool> UpdateAttendanceSegmentByInAsync(
        OdbcConnection connection,
        OdbcTransaction tx,
        string attendanceTable,
        string employeeCode,
        string employeeName,
        string deptCode,
        string shiftCode,
        DateTime matchInTime,
        DateTime inTime,
        DateTime? outTime,
        CancellationToken cancellationToken)
    {
        var inTsMin = ToPervasiveMinutes(inTime);
        var outTsMin = outTime.HasValue ? ToPervasiveMinutes(outTime.Value) : 0m;
        var (hoursHh, hoursMm) = CalculateHours(inTime, outTime);
        var lastChange = outTime ?? inTime;

        var sql = $@"
UPDATE {attendanceTable}
SET IN_TS_MIN = ?,
    OUT_TS_MIN = ?,
    DATE_IN = ?,
    TIME_IN = ?,
    DATE_OUT = ?,
    TIME_OUT = ?,
    NAME = ?,
    DEPT = ?,
    SHIFT = ?,
    REC_TYPE = 'T',
    CLOCKED_IN = ?,
    HOURS_HH = ?,
    HOURS_MM = ?,
    ACTUAL_DATE = ?,
    DATE_LAST_CHG = ?,
    TIME_LAST_CHG = ?,
    LAST_CHG_BY = ?,
    LAST_CHG_PROG = ?
WHERE EMPLOYEE = ?
  AND CHARGE_DATE = ?
  AND DATE_IN = ?
  AND TIME_IN = ?";

        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@IN_TS_MIN", inTsMin);
        cmd.Parameters.AddWithValue("@OUT_TS_MIN", outTsMin);
        cmd.Parameters.AddWithValue("@DATE_IN", FormatDate(inTime));
        cmd.Parameters.AddWithValue("@TIME_IN", FormatTime(inTime));
        cmd.Parameters.AddWithValue("@DATE_OUT", outTime.HasValue ? FormatDate(outTime.Value) : "00000000");
        cmd.Parameters.AddWithValue("@TIME_OUT", outTime.HasValue ? FormatTime(outTime.Value) : "000000");
        cmd.Parameters.AddWithValue("@NAME", employeeName);
        cmd.Parameters.AddWithValue("@DEPT", deptCode);
        cmd.Parameters.AddWithValue("@SHIFT", shiftCode);
        cmd.Parameters.AddWithValue("@CLOCKED_IN", outTime.HasValue ? string.Empty : "Y");
        cmd.Parameters.AddWithValue("@HOURS_HH", hoursHh);
        cmd.Parameters.AddWithValue("@HOURS_MM", hoursMm);
        cmd.Parameters.AddWithValue("@ACTUAL_DATE", FormatActualDate(DateTime.Today));
        cmd.Parameters.AddWithValue("@DATE_LAST_CHG", FormatDate(lastChange));
        cmd.Parameters.AddWithValue("@TIME_LAST_CHG", FormatTime(lastChange) + "00");
        cmd.Parameters.AddWithValue("@LAST_CHG_BY", employeeCode);
        cmd.Parameters.AddWithValue("@LAST_CHG_PROG", MirrorProgramName);
        cmd.Parameters.AddWithValue("@EMPLOYEE", employeeCode);
        cmd.Parameters.AddWithValue("@CHARGE_DATE", FormatDate(matchInTime.Date));
        cmd.Parameters.AddWithValue("@MATCH_DATE_IN", FormatDate(matchInTime));
        cmd.Parameters.AddWithValue("@MATCH_TIME_IN", FormatTime(matchInTime));

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private async Task<bool> ExistsAttendanceRowAsync(
        OdbcConnection connection,
        OdbcTransaction? tx,
        string attendanceTable,
        string employeeCode,
        DateTime date,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT EMPLOYEE FROM {attendanceTable} WHERE EMPLOYEE = ? AND CHARGE_DATE = ?";
        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@p1", employeeCode);
        cmd.Parameters.AddWithValue("@p2", FormatDate(date));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    private async Task<bool> HasAttendanceEntryAsync(
        OdbcConnection connection,
        OdbcTransaction? tx,
        string attendanceTable,
        string employeeCode,
        DateTime date,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT IN_TS_MIN, DATE_IN, TIME_IN FROM {attendanceTable} WHERE EMPLOYEE = ? AND CHARGE_DATE = ?";
        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@p1", employeeCode);
        cmd.Parameters.AddWithValue("@p2", FormatDate(date));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return false;
        }

        var inTsMin = await reader.IsDBNullAsync(0, cancellationToken) ? 0m : Convert.ToDecimal(reader[0]);
        var dateIn = await reader.IsDBNullAsync(1, cancellationToken) ? null : Convert.ToString(reader[1]);
        var timeIn = await reader.IsDBNullAsync(2, cancellationToken) ? null : Convert.ToString(reader[2]);

        var hasInTs = inTsMin > 0m;
        var hasDateIn = !string.IsNullOrWhiteSpace(dateIn) && !string.Equals(dateIn, "00000000", StringComparison.OrdinalIgnoreCase);
        var hasTimeIn = !string.IsNullOrWhiteSpace(timeIn) && !string.Equals(timeIn, "000000", StringComparison.OrdinalIgnoreCase);

        return hasInTs || hasDateIn || hasTimeIn;
    }

    private async Task<DateTime?> TryGetExistingInTimeAsync(
        OdbcConnection connection,
        OdbcTransaction? tx,
        string attendanceTable,
        string employeeCode,
        DateTime date,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT DATE_IN, TIME_IN FROM {attendanceTable} WHERE EMPLOYEE = ? AND CHARGE_DATE = ?";
        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@p1", employeeCode);
        cmd.Parameters.AddWithValue("@p2", FormatDate(date));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var dateIn = await reader.IsDBNullAsync(0, cancellationToken) ? null : Convert.ToString(reader[0]);
        var timeIn = await reader.IsDBNullAsync(1, cancellationToken) ? null : Convert.ToString(reader[1]);

        if (string.IsNullOrWhiteSpace(dateIn) || dateIn == "00000000")
        {
            return null;
        }

        if (!DateTime.TryParseExact(dateIn, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(timeIn) && timeIn != "000000")
        {
            var digits = new string(timeIn.Where(char.IsDigit).ToArray());
            if (digits.Length >= 6)
            {
                var hh = int.Parse(digits[..2]);
                var mm = int.Parse(digits.Substring(2, 2));
                var ss = int.Parse(digits.Substring(4, 2));
                if (hh is >= 0 and <= 23 && mm is >= 0 and <= 59 && ss is >= 0 and <= 59)
                {
                    return new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, hh, mm, ss);
                }
            }
        }

        return parsedDate.Date;
    }

    private async Task<DateTime?> TryGetExistingOutTimeAsync(
        OdbcConnection connection,
        OdbcTransaction? tx,
        string attendanceTable,
        string employeeCode,
        DateTime date,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT DATE_OUT, TIME_OUT FROM {attendanceTable} WHERE EMPLOYEE = ? AND CHARGE_DATE = ?";
        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@p1", employeeCode);
        cmd.Parameters.AddWithValue("@p2", FormatDate(date));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var dateOut = await reader.IsDBNullAsync(0, cancellationToken) ? null : Convert.ToString(reader[0]);
        var timeOut = await reader.IsDBNullAsync(1, cancellationToken) ? null : Convert.ToString(reader[1]);

        if (string.IsNullOrWhiteSpace(dateOut) || dateOut == "00000000")
        {
            return null;
        }

        if (!DateTime.TryParseExact(dateOut, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(timeOut) && timeOut != "000000")
        {
            var digits = new string(timeOut.Where(char.IsDigit).ToArray());
            if (digits.Length >= 6)
            {
                var hh = int.Parse(digits[..2]);
                var mm = int.Parse(digits.Substring(2, 2));
                var ss = int.Parse(digits.Substring(4, 2));
                if (hh is >= 0 and <= 23 && mm is >= 0 and <= 59 && ss is >= 0 and <= 59)
                {
                    return new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, hh, mm, ss);
                }
            }
        }

        return parsedDate.Date;
    }

    private async Task<bool> ShouldClearInvalidExistingOutAsync(
        OdbcConnection connection,
        OdbcTransaction? tx,
        string attendanceTable,
        string employeeCode,
        DateTime day,
        CancellationToken cancellationToken)
    {
        if (MinimumOutGapMinutes <= 0)
        {
            return false;
        }

        var existingIn = await TryGetExistingInTimeAsync(connection, tx, attendanceTable, employeeCode, day, cancellationToken);
        var existingOut = await TryGetExistingOutTimeAsync(connection, tx, attendanceTable, employeeCode, day, cancellationToken);
        if (!existingIn.HasValue || !existingOut.HasValue)
        {
            return false;
        }

        var gap = existingOut.Value - existingIn.Value;
        return gap.TotalMinutes < MinimumOutGapMinutes;
    }

    private async Task<bool> ClearInvalidExistingOutAsync(
        OdbcConnection connection,
        OdbcTransaction tx,
        string attendanceTable,
        string employeeCode,
        DateTime day,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var sql = $@"
UPDATE {attendanceTable}
SET OUT_TS_MIN = 0,
    DATE_OUT = '00000000',
    TIME_OUT = '000000',
    CLOCKED_IN = 'Y',
    HOURS_HH = 0,
    HOURS_MM = 0,
    ACTUAL_DATE = ?,
    DATE_LAST_CHG = ?,
    TIME_LAST_CHG = ?,
    LAST_CHG_BY = ?,
    LAST_CHG_PROG = ?
WHERE EMPLOYEE = ? AND CHARGE_DATE = ?";

        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@ACTUAL_DATE", FormatActualDate(now));
        cmd.Parameters.AddWithValue("@DATE_LAST_CHG", FormatDate(now));
        cmd.Parameters.AddWithValue("@TIME_LAST_CHG", FormatTime(now) + "00");
        cmd.Parameters.AddWithValue("@LAST_CHG_BY", employeeCode);
        cmd.Parameters.AddWithValue("@LAST_CHG_PROG", MirrorProgramName);
        cmd.Parameters.AddWithValue("@EMPLOYEE", employeeCode);
        cmd.Parameters.AddWithValue("@CHARGE_DATE", FormatDate(day));

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private async Task InsertAttendanceEntryAsync(
        OdbcConnection connection,
        OdbcTransaction tx,
        string attendanceTable,
        string employeeCode,
        string employeeName,
        string deptCode,
        string shiftCode,
        DateTime inTime,
        DateTime? outTime,
        CancellationToken cancellationToken)
    {
        var inTsMin = ToPervasiveMinutes(inTime);
        var outTsMin = outTime.HasValue ? ToPervasiveMinutes(outTime.Value) : 0m;
        var (hoursHh, hoursMm) = CalculateHours(inTime, outTime);

        var sql = $@"
INSERT INTO {attendanceTable}
(EMPLOYEE, CHARGE_DATE, IN_TS_MIN, OUT_TS_MIN, DATE_IN, TIME_IN, DATE_OUT, TIME_OUT, NAME, DEPT, SHIFT, REC_TYPE,
 CLOCKED_IN, BAL_AS_DATE, DATE_LAST_CHG, TIME_LAST_CHG, LAST_CHG_BY, LAST_CHG_PROG, HOURS_HH, HOURS_MM, DOLLAR_AMT, ACTUAL_DATE)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'T', ?, '00000000', ?, ?, ?, ?, ?, ?, 0.00, ?)";

        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@EMPLOYEE", employeeCode);
        cmd.Parameters.AddWithValue("@CHARGE_DATE", FormatDate(inTime));
        cmd.Parameters.AddWithValue("@IN_TS_MIN", inTsMin);
        cmd.Parameters.AddWithValue("@OUT_TS_MIN", outTsMin);
        cmd.Parameters.AddWithValue("@DATE_IN", FormatDate(inTime));
        cmd.Parameters.AddWithValue("@TIME_IN", FormatTime(inTime));
        cmd.Parameters.AddWithValue("@DATE_OUT", outTime.HasValue ? FormatDate(outTime.Value) : "00000000");
        cmd.Parameters.AddWithValue("@TIME_OUT", outTime.HasValue ? FormatTime(outTime.Value) : "000000");
        cmd.Parameters.AddWithValue("@NAME", employeeName);
        cmd.Parameters.AddWithValue("@DEPT", deptCode);
        cmd.Parameters.AddWithValue("@SHIFT", shiftCode);
        cmd.Parameters.AddWithValue("@CLOCKED_IN", outTime.HasValue ? string.Empty : "Y");
        cmd.Parameters.AddWithValue("@DATE_LAST_CHG", FormatDate(inTime));
        cmd.Parameters.AddWithValue("@TIME_LAST_CHG", FormatTime(inTime) + "00");
        cmd.Parameters.AddWithValue("@LAST_CHG_BY", employeeCode);
        cmd.Parameters.AddWithValue("@LAST_CHG_PROG", MirrorProgramName);
        cmd.Parameters.AddWithValue("@HOURS_HH", hoursHh);
        cmd.Parameters.AddWithValue("@HOURS_MM", hoursMm);
        cmd.Parameters.AddWithValue("@ACTUAL_DATE", FormatActualDate(DateTime.Today));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> UpdateAttendanceEntryAsync(
        OdbcConnection connection,
        OdbcTransaction tx,
        string attendanceTable,
        string employeeCode,
        string employeeName,
        string deptCode,
        string shiftCode,
        DateTime day,
        DateTime inTime,
        DateTime? outTime,
        CancellationToken cancellationToken)
    {
        var inTsMin = ToPervasiveMinutes(inTime);
        var (hoursHh, hoursMm) = CalculateHours(inTime, outTime);

        string sql;
        if (outTime.HasValue)
        {
            sql = $@"
UPDATE {attendanceTable}
SET IN_TS_MIN = ?,
    OUT_TS_MIN = ?,
    DATE_IN = ?,
    TIME_IN = ?,
    DATE_OUT = ?,
    TIME_OUT = ?,
    NAME = ?,
    DEPT = ?,
    SHIFT = ?,
    REC_TYPE = 'T',
    CLOCKED_IN = '',
    HOURS_HH = ?,
    HOURS_MM = ?,
    ACTUAL_DATE = ?,
    DATE_LAST_CHG = ?,
    TIME_LAST_CHG = ?,
    LAST_CHG_BY = ?,
    LAST_CHG_PROG = ?
WHERE EMPLOYEE = ? AND CHARGE_DATE = ?";
        }
        else
        {
            sql = $@"
UPDATE {attendanceTable}
SET IN_TS_MIN = ?,
    DATE_IN = ?,
    TIME_IN = ?,
    NAME = ?,
    DEPT = ?,
    SHIFT = ?,
    REC_TYPE = 'T',
    CLOCKED_IN = 'Y',
    HOURS_HH = 0,
    HOURS_MM = 0,
    ACTUAL_DATE = ?,
    DATE_LAST_CHG = ?,
    TIME_LAST_CHG = ?,
    LAST_CHG_BY = ?,
    LAST_CHG_PROG = ?
WHERE EMPLOYEE = ? AND CHARGE_DATE = ?";
        }

        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;

        cmd.Parameters.AddWithValue("@IN_TS_MIN", inTsMin);
        if (outTime.HasValue)
        {
            cmd.Parameters.AddWithValue("@OUT_TS_MIN", ToPervasiveMinutes(outTime.Value));
            cmd.Parameters.AddWithValue("@DATE_IN", FormatDate(inTime));
            cmd.Parameters.AddWithValue("@TIME_IN", FormatTime(inTime));
            cmd.Parameters.AddWithValue("@DATE_OUT", FormatDate(outTime.Value));
            cmd.Parameters.AddWithValue("@TIME_OUT", FormatTime(outTime.Value));
            cmd.Parameters.AddWithValue("@NAME", employeeName);
            cmd.Parameters.AddWithValue("@DEPT", deptCode);
            cmd.Parameters.AddWithValue("@SHIFT", shiftCode);
            cmd.Parameters.AddWithValue("@HOURS_HH", hoursHh);
            cmd.Parameters.AddWithValue("@HOURS_MM", hoursMm);
            cmd.Parameters.AddWithValue("@ACTUAL_DATE", FormatActualDate(DateTime.Today));
            cmd.Parameters.AddWithValue("@DATE_LAST_CHG", FormatDate(outTime.Value));
            cmd.Parameters.AddWithValue("@TIME_LAST_CHG", FormatTime(outTime.Value) + "00");
            cmd.Parameters.AddWithValue("@LAST_CHG_BY", employeeCode);
            cmd.Parameters.AddWithValue("@LAST_CHG_PROG", MirrorProgramName);
        }
        else
        {
            cmd.Parameters.AddWithValue("@DATE_IN", FormatDate(inTime));
            cmd.Parameters.AddWithValue("@TIME_IN", FormatTime(inTime));
            cmd.Parameters.AddWithValue("@NAME", employeeName);
            cmd.Parameters.AddWithValue("@DEPT", deptCode);
            cmd.Parameters.AddWithValue("@SHIFT", shiftCode);
            cmd.Parameters.AddWithValue("@ACTUAL_DATE", FormatActualDate(DateTime.Today));
            cmd.Parameters.AddWithValue("@DATE_LAST_CHG", FormatDate(inTime));
            cmd.Parameters.AddWithValue("@TIME_LAST_CHG", FormatTime(inTime) + "00");
            cmd.Parameters.AddWithValue("@LAST_CHG_BY", employeeCode);
            cmd.Parameters.AddWithValue("@LAST_CHG_PROG", MirrorProgramName);
        }

        cmd.Parameters.AddWithValue("@EMPLOYEE", employeeCode);
        cmd.Parameters.AddWithValue("@CHARGE_DATE", FormatDate(day));

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private async Task<bool> UpdateAttendanceExitAsync(
        OdbcConnection connection,
        OdbcTransaction tx,
        string attendanceTable,
        string employeeCode,
        string deptCode,
        string shiftCode,
        DateTime day,
        DateTime inTime,
        DateTime? outTime,
        CancellationToken cancellationToken)
    {
        if (!outTime.HasValue)
        {
            return false;
        }

        var outTsMin = ToPervasiveMinutes(outTime.Value);
        var (hoursHh, hoursMm) = CalculateHours(inTime, outTime);

        var sql = $@"
UPDATE {attendanceTable}
SET OUT_TS_MIN = ?,
    DATE_OUT = ?,
    TIME_OUT = ?,
    DEPT = ?,
    SHIFT = ?,
    CLOCKED_IN = '',
    HOURS_HH = ?,
    HOURS_MM = ?,
    ACTUAL_DATE = ?,
    DATE_LAST_CHG = ?,
    TIME_LAST_CHG = ?,
    LAST_CHG_BY = ?,
    LAST_CHG_PROG = ?
WHERE EMPLOYEE = ? AND CHARGE_DATE = ?";

        await using var cmd = new OdbcCommand(sql, connection, tx);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@OUT_TS_MIN", outTsMin);
        cmd.Parameters.AddWithValue("@DATE_OUT", FormatDate(outTime.Value));
        cmd.Parameters.AddWithValue("@TIME_OUT", FormatTime(outTime.Value));
        cmd.Parameters.AddWithValue("@DEPT", deptCode);
        cmd.Parameters.AddWithValue("@SHIFT", shiftCode);
        cmd.Parameters.AddWithValue("@HOURS_HH", hoursHh);
        cmd.Parameters.AddWithValue("@HOURS_MM", hoursMm);
        cmd.Parameters.AddWithValue("@ACTUAL_DATE", FormatActualDate(DateTime.Today));
        cmd.Parameters.AddWithValue("@DATE_LAST_CHG", FormatDate(outTime.Value));
        cmd.Parameters.AddWithValue("@TIME_LAST_CHG", FormatTime(outTime.Value) + "00");
        cmd.Parameters.AddWithValue("@LAST_CHG_BY", employeeCode);
        cmd.Parameters.AddWithValue("@LAST_CHG_PROG", MirrorProgramName);
        cmd.Parameters.AddWithValue("@EMPLOYEE", employeeCode);
        cmd.Parameters.AddWithValue("@CHARGE_DATE", FormatDate(day));

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private static decimal ToPervasiveMinutes(DateTime dt)
    {
        var minutes = (decimal)(dt - PervasiveEpoch).TotalMinutes;
        if (minutes < 0)
        {
            minutes = 0;
        }

        return Math.Round(minutes, 4);
    }

    private static (int HoursHh, int HoursMm) CalculateHours(DateTime inTime, DateTime? outTime)
    {
        if (!outTime.HasValue || outTime.Value <= inTime)
        {
            return (0, 0);
        }

        var span = outTime.Value - inTime;
        var totalMinutes = (int)Math.Round(span.TotalMinutes);
        if (totalMinutes < 0) totalMinutes = 0;
        return (totalMinutes / 60, totalMinutes % 60);
    }

    private static string FormatDate(DateTime dt) => dt.ToString("yyyyMMdd");

    private static string FormatTime(DateTime dt) => dt.ToString("HHmmss");

    private static string FormatActualDate(DateTime dt) => dt.ToString("yyyy-MM-dd");

    private static string FormatEmployeeId(int userId)
    {
        if (userId < 0)
        {
            userId = 0;
        }

        return userId.ToString("D5");
    }

    private static string NormalizeDeptLabel(string? value, string? fallback)
    {
        var input = string.IsNullOrWhiteSpace(value) ? fallback : value;
        if (string.IsNullOrWhiteSpace(input))
        {
            return DeptLabelByCode["0"];
        }

        var raw = input.Trim().ToUpperInvariant();

        if (DeptLabelByCode.TryGetValue(raw, out var mappedByDirectCode))
        {
            return mappedByDirectCode;
        }

        var tokens = raw
            .Split(new[] { ' ', '-', '_', '/', '.', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var explicitCode = tokens.FirstOrDefault(t => t.Length == 1 && (char.IsDigit(t[0]) || t[0] == 'A'));
        if (!string.IsNullOrWhiteSpace(explicitCode) && DeptLabelByCode.TryGetValue(explicitCode, out var mappedByTokenCode))
        {
            return mappedByTokenCode;
        }

        var firstToken = tokens.FirstOrDefault() ?? raw;
        if (DeptShiftCodeByName.TryGetValue(firstToken, out var mappedCode) && DeptLabelByCode.TryGetValue(mappedCode, out var mappedByName))
        {
            return mappedByName;
        }

        var normalized = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        if (DeptShiftCodeByName.TryGetValue(normalized, out var normalizedCode) && DeptLabelByCode.TryGetValue(normalizedCode, out var mappedByNormalizedName))
        {
            return mappedByNormalizedName;
        }

        return firstToken;
    }

    private static string NormalizeShiftCode(string? value, string? fallback)
    {
        var input = string.IsNullOrWhiteSpace(value) ? fallback : value;
        if (string.IsNullOrWhiteSpace(input))
        {
            return "0";
        }

        var compactInput = input.Trim().ToUpperInvariant();
        if (compactInput.Any(char.IsDigit))
        {
            var firstDigit = compactInput.First(char.IsDigit);
            return firstDigit.ToString();
        }

        var tokens = compactInput
            .Split(new[] { ' ', '-', '_', '/', '.', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var explicitCode = tokens.FirstOrDefault(t => t.Length == 1 && (char.IsDigit(t[0]) || t[0] == 'A'));
        if (!string.IsNullOrWhiteSpace(explicitCode))
        {
            return explicitCode;
        }

        var firstToken = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? input.Trim();
        var token = firstToken.ToUpperInvariant();
        if (DeptShiftCodeByName.TryGetValue(token, out var mappedByToken))
        {
            return mappedByToken;
        }

        var normalized = new string(input.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (DeptShiftCodeByName.TryGetValue(normalized, out var mappedByName))
        {
            return mappedByName;
        }

        return token.Length > 1 ? token[..1] : token;
    }

    private static bool IsInPunchMode(int? mode)
        => mode is 0 or 3;

    private static bool IsOutPunchMode(int? mode)
        => mode is 1 or 2;

    private static PunchRecord? ResolveMirrorEntryPunch(IReadOnlyList<PunchRecord> ordered)
    {
        if (ordered.Count == 0)
        {
            return null;
        }

        var firstPunch = ordered[0];
        var hasTypedMode = ordered.Any(x => x.InOutMode.HasValue);
        if (!hasTypedMode)
        {
            return firstPunch;
        }

        var modeBasedEntry = ordered.FirstOrDefault(x => IsInPunchMode(x.InOutMode))
            ?? ordered.FirstOrDefault(x => !x.InOutMode.HasValue);

        if (modeBasedEntry is not null)
        {
            if (!IsNightShiftContext(ordered)
                && modeBasedEntry.CheckTime.TimeOfDay >= MiddayBoundary)
            {
                var morningEntryCandidate = ResolveDayShiftMorningEntryCandidate(ordered);
                if (morningEntryCandidate is not null
                    && morningEntryCandidate.CheckTime < modeBasedEntry.CheckTime
                    && (ordered[^1].CheckTime - morningEntryCandidate.CheckTime).TotalMinutes >= MinChronologicalFallbackSpanMinutes)
                {
                    return morningEntryCandidate;
                }
            }

            var firstUnknownBeforeModeEntry = ordered
                .FirstOrDefault(x => !x.InOutMode.HasValue && x.CheckTime < modeBasedEntry.CheckTime);

            if (firstUnknownBeforeModeEntry is not null)
            {
                var unknownGapMinutes = (modeBasedEntry.CheckTime - firstUnknownBeforeModeEntry.CheckTime).TotalMinutes;
                if (unknownGapMinutes >= MinUnknownEntryFallbackGapMinutes)
                {
                    return firstUnknownBeforeModeEntry;
                }
            }

            if (modeBasedEntry.CheckTime != firstPunch.CheckTime
                && ShouldUseChronologicalEntryFallback(ordered, firstPunch, modeBasedEntry))
            {
                return firstPunch;
            }

            return modeBasedEntry;
        }

        return CanInferEntryFromOutOnlyDay(ordered)
            ? firstPunch
            : null;
    }

    private static PunchRecord? ResolveDayShiftMorningEntryCandidate(IReadOnlyList<PunchRecord> ordered)
    {
        var morningPunches = ordered
            .Where(x => x.CheckTime.TimeOfDay < MiddayBoundary)
            .OrderBy(x => x.CheckTime)
            .ToList();

        if (morningPunches.Count == 0)
        {
            return null;
        }

        var preferredWindow = morningPunches
            .Where(x => x.CheckTime.TimeOfDay >= DayShiftEarliestEntryBoundary)
            .ToList();

        if (preferredWindow.Count == 0)
        {
            preferredWindow = morningPunches;
        }

        return preferredWindow.FirstOrDefault(x => x.InOutMode == 0)
            ?? preferredWindow.FirstOrDefault(x => IsInPunchMode(x.InOutMode))
            ?? preferredWindow.FirstOrDefault(x => !x.InOutMode.HasValue)
            ?? preferredWindow[0];
    }

    private static bool ShouldUseChronologicalEntryFallback(
        IReadOnlyList<PunchRecord> ordered,
        PunchRecord firstPunch,
        PunchRecord modeBasedEntry)
    {
        if (modeBasedEntry.CheckTime <= firstPunch.CheckTime)
        {
            return false;
        }

        var punchesBeforeModeEntry = ordered
            .Where(x => x.CheckTime < modeBasedEntry.CheckTime)
            .ToList();

        if (punchesBeforeModeEntry.Count == 0)
        {
            return false;
        }

        if (!punchesBeforeModeEntry.All(x => IsOutPunchMode(x.InOutMode)))
        {
            return false;
        }

        var gapMinutes = (modeBasedEntry.CheckTime - firstPunch.CheckTime).TotalMinutes;
        if (gapMinutes >= MinChronologicalFallbackSpanMinutes)
        {
            return true;
        }

        return modeBasedEntry.InOutMode == 3 && gapMinutes >= 60;
    }

    private static bool CanInferEntryFromOutOnlyDay(IReadOnlyList<PunchRecord> ordered)
    {
        if (ordered.Count == 1)
        {
            var singlePunch = ordered[0];
            if (!singlePunch.InOutMode.HasValue || !IsOutPunchMode(singlePunch.InOutMode))
            {
                return false;
            }

            if (IsNightShiftContext(ordered))
            {
                return false;
            }

            return singlePunch.CheckTime.TimeOfDay < MiddayBoundary;
        }

        if (ordered.Count < 2)
        {
            return false;
        }

        if (!ordered.Any(x => x.InOutMode.HasValue))
        {
            return false;
        }

        if (ordered.Any(x => IsInPunchMode(x.InOutMode) || !x.InOutMode.HasValue))
        {
            return false;
        }

        var spanMinutes = (ordered[^1].CheckTime - ordered[0].CheckTime).TotalMinutes;
        return spanMinutes >= MinChronologicalFallbackSpanMinutes;
    }

    private bool IsEligibleOutForClosure(int? mode)
        => CloseOnlyWithFinalOutMode ? mode is 1 : IsOutPunchMode(mode);

    private (PunchRecord? OutPunch, string? GapNote) ResolveOutPunchCandidate(IReadOnlyList<PunchRecord> ordered, PunchRecord inPunch)
    {
        PunchRecord? outPunch = ordered
            .Where(x => x.CheckTime > inPunch.CheckTime && IsEligibleOutForClosure(x.InOutMode))
            .LastOrDefault();

        if (outPunch is null && !CloseOnlyWithFinalOutMode)
        {
            outPunch = ordered
                .Where(x => x.CheckTime > inPunch.CheckTime)
                .OrderBy(x => x.CheckTime)
                .LastOrDefault();
        }

        var isNightShiftContext = IsNightShiftContext(ordered);

        if (!isNightShiftContext
            && outPunch is not null
            && outPunch.CheckTime.TimeOfDay <= MiddayBoundary)
        {
            var latestAfternoonPunch = ordered
                .Where(x => x.CheckTime > inPunch.CheckTime && x.CheckTime.TimeOfDay > MiddayBoundary)
                .OrderBy(x => x.CheckTime)
                .LastOrDefault();

            if (latestAfternoonPunch is not null
                && (latestAfternoonPunch.CheckTime - inPunch.CheckTime).TotalMinutes >= MinChronologicalFallbackSpanMinutes)
            {
                outPunch = latestAfternoonPunch;
            }
        }

        string? inversionNote = null;
        if (outPunch is not null && IsInPunchMode(outPunch.InOutMode))
        {
            if (!IsClearlyValidInvertedModePair(ordered, inPunch, outPunch, isNightShiftContext))
            {
                return (null, "Salida inferida descartada por inversion de modos no concluyente.");
            }

            inversionNote = "Salida inferida por par invertido m1->m0 con cronologia valida.";
        }

        if (!isNightShiftContext && outPunch is not null)
        {
            var shiftSpanMinutes = (outPunch.CheckTime - inPunch.CheckTime).TotalMinutes;
            if (shiftSpanMinutes > MaxClearlyValidShiftSpanMinutes)
            {
                return (null, $"Salida inferida descartada por jornada diurna fuera de ventana valida: gap={shiftSpanMinutes:0.##} min.");
            }
        }

        if (outPunch is null || MinimumOutGapMinutes <= 0)
        {
            return (outPunch, inversionNote);
        }

        var span = outPunch.CheckTime - inPunch.CheckTime;
        if (span.TotalMinutes >= MinimumOutGapMinutes)
        {
            return (outPunch, inversionNote);
        }

        var note = $"Salida ignorada por cercania: gap={Math.Max(0d, span.TotalMinutes):0.##} min, minimo={MinimumOutGapMinutes} min.";
        return (null, note);
    }

    private static bool IsClearlyValidInvertedModePair(
        IReadOnlyList<PunchRecord> ordered,
        PunchRecord entradaPunch,
        PunchRecord salidaPunch,
        bool isNightShiftContext)
    {
        if (ordered.Count != 2)
        {
            return false;
        }

        var first = ordered[0];
        var last = ordered[^1];
        if (!IsOutPunchMode(first.InOutMode) || !IsInPunchMode(last.InOutMode))
        {
            return false;
        }

        if (entradaPunch.CheckTime != first.CheckTime || salidaPunch.CheckTime != last.CheckTime)
        {
            return false;
        }

        var spanMinutes = (salidaPunch.CheckTime - entradaPunch.CheckTime).TotalMinutes;
        if (spanMinutes < MinChronologicalFallbackSpanMinutes || spanMinutes > MaxClearlyValidShiftSpanMinutes)
        {
            return false;
        }

        if (isNightShiftContext)
        {
            return true;
        }

        return entradaPunch.CheckTime.TimeOfDay < MiddayBoundary
            && salidaPunch.CheckTime.TimeOfDay > MiddayBoundary;
    }

    private static bool IsNightShiftContext(IReadOnlyList<PunchRecord> ordered)
    {
        return ordered.Any(x => x.IsNightShift == true || x.WorkScheduleCode == 3);
    }

    private static bool IsTimeoutException(Exception ex)
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

        var msg = ex.Message ?? string.Empty;
        return msg.Contains("tiempo de espera", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("time-out", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeIdentifier(string? input, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(input) ? fallback : input.Trim();
        if (raw.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
        {
            throw new InvalidOperationException($"Nombre de tabla invÃ¡lido para Pervasive: '{raw}'. Usa solo letras, nÃºmeros y _.");
        }

        return raw;
    }

    private async Task<int> ExecuteCountByEmployeeAndDateRangeAsync(
        OdbcConnection connection,
        string attendanceTable,
        string employeeCode,
        string fromChargeDate,
        string toChargeDate,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT COUNT(1) FROM {attendanceTable} WHERE EMPLOYEE = ? AND CHARGE_DATE >= ? AND CHARGE_DATE <= ?";
        await using var cmd = new OdbcCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@P1", employeeCode);
        cmd.Parameters.AddWithValue("@P2", fromChargeDate);
        cmd.Parameters.AddWithValue("@P3", toChargeDate);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private async Task<int> ExecuteConflictCountByDateRangeAsync(
        OdbcConnection connection,
        string attendanceTable,
        string oldEmployeeCode,
        string newEmployeeCode,
        string fromChargeDate,
        string toChargeDate,
        CancellationToken cancellationToken)
    {
        if (string.Equals(oldEmployeeCode, newEmployeeCode, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var sql = $@"
SELECT COUNT(1)
FROM {attendanceTable} o
WHERE o.EMPLOYEE = ?
  AND o.CHARGE_DATE >= ?
  AND o.CHARGE_DATE <= ?
  AND EXISTS
      (
          SELECT 1
          FROM {attendanceTable} n
          WHERE n.EMPLOYEE = ?
            AND n.CHARGE_DATE = o.CHARGE_DATE
      )";

        await using var cmd = new OdbcCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@P1", oldEmployeeCode);
        cmd.Parameters.AddWithValue("@P2", fromChargeDate);
        cmd.Parameters.AddWithValue("@P3", toChargeDate);
        cmd.Parameters.AddWithValue("@P4", newEmployeeCode);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private async Task<IReadOnlyList<DateTime>> GetAffectedChargeDatesAsync(
        OdbcConnection connection,
        string attendanceTable,
        string employeeCode,
        string fromChargeDate,
        string toChargeDate,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT DISTINCT CHARGE_DATE
FROM {attendanceTable}
WHERE EMPLOYEE = ?
  AND CHARGE_DATE >= ?
  AND CHARGE_DATE <= ?
ORDER BY CHARGE_DATE";

        await using var cmd = new OdbcCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("@P1", employeeCode);
        cmd.Parameters.AddWithValue("@P2", fromChargeDate);
        cmd.Parameters.AddWithValue("@P3", toChargeDate);

        var dates = new List<DateTime>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (await reader.IsDBNullAsync(0, cancellationToken))
            {
                continue;
            }

            var raw = Convert.ToString(reader[0]);
            if (DateTime.TryParseExact(raw, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed))
            {
                dates.Add(parsed.Date);
            }
        }

        return dates;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record MirrorSegment(DateTime InTime, DateTime? OutTime, bool IsIntermediate, string? Note);
}

public sealed record PervasiveMirrorResult(int Inserted, int Updated, int Skipped, string Message);
public sealed record PervasiveEmployeeRemapPreviewResult(bool Enabled, int OldRows, int TargetRows, int ConflictRows, int DistinctDays, IReadOnlyList<DateTime> AffectedDates, string Message);
public sealed record PervasiveEmployeeRemapApplyResult(bool Enabled, int UpdatedRows, int SkippedConflicts, string Message);

