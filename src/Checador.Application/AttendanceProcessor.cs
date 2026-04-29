using Checador.Domain.Models;

namespace Checador.Application;

public sealed class AttendanceProcessor
{
    private static readonly TimeSpan MiddayBoundary = TimeSpan.FromHours(12);
    private static readonly TimeSpan DayShiftEarliestEntryBoundary = TimeSpan.FromHours(4);
    private const int MinBreakOutAfterEntryMinutes = 90;
    private const int MinBreakOutBeforeExitMinutes = 60;
    private const int MaxBreakPairGapMinutesForMissingEntry = 120;
    private const int MinChronologicalFallbackSpanMinutes = 240;
    private const int MaxClearlyValidShiftSpanMinutes = 1080;
    private const int MinUnknownEntryFallbackGapMinutes = 180;
    private readonly AttendanceProcessingOptions _options;

    public AttendanceProcessor(AttendanceProcessingOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<AttendanceDayResult> Process(IEnumerable<PunchRecord> punches)
    {
        var assignedPunches = punches
            .GroupBy(p => p.UserId)
            .SelectMany(g => AssignWorkDatesForUser(g.OrderBy(p => p.CheckTime).ToList()))
            .ToList();

        var dailyGroups = assignedPunches
            .GroupBy(x => new { x.UserId, Date = x.WorkDate })
            .Select(g => new DailyPunches
            {
                UserId = g.Key.UserId,
                EmployeeName = g
                    .OrderByDescending(x => x.Punch.CheckTime)
                    .Select(x => x.Punch.EmployeeName)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                    ?? $"Empleado_{g.Key.UserId}",
                Date = g.Key.Date,
                Punches = g.OrderBy(x => x.Punch.CheckTime).Select(x => x.Punch).ToList()
            })
            .OrderBy(x => x.Date)
            .ToList();

        return dailyGroups.Select(ProcessDay).ToList();
    }

    private AttendanceDayResult ProcessDay(DailyPunches day)
    {
        var ordered = CollapsePunchesByMinute(day.Punches);

        if (ordered.Count < 2)
        {
            return BuildIncomplete(day, "INCOMPLETO", "Menos de 2 marcas registradas.", ordered);
        }

        var hasAnyMode = ordered.Any(x => x.InOutMode.HasValue);
        var entradaPunch = ResolveEntryPunch(ordered, hasAnyMode);
        if (entradaPunch is null)
        {
            return BuildIncomplete(day, "INCIDENCIA", "Solo se detectaron salidas; no hay entrada valida segun modo del checador.", ordered);
        }

        var isDayShiftContext = !ordered.Any(IsNightShiftPunch);

        PunchRecord? salidaIntermediaPunch = null;
        PunchRecord? entradaIntermediaPunch = null;
        PunchRecord? salidaPunch;
        var usedFallbackOutMode = false;
        var usedChronologicalOutFallback = false;
        var usedModeOrderFallback = false;
        var usedSingleBreakOutAsFinalOut = false;
        var usedTemporalEntryGuard = false;
        var usedTemporalExitGuard = false;
        var usedTemporalLunchGuard = false;

        if (isDayShiftContext)
        {
            var morningEntryCandidate = ResolveDayShiftMorningEntryCandidate(ordered);
            if (morningEntryCandidate is not null
                && entradaPunch.CheckTime.TimeOfDay >= MiddayBoundary
                && morningEntryCandidate.CheckTime < entradaPunch.CheckTime
                && (ordered[^1].CheckTime - morningEntryCandidate.CheckTime).TotalMinutes >= MinChronologicalFallbackSpanMinutes)
            {
                entradaPunch = morningEntryCandidate;
                usedTemporalEntryGuard = true;
            }
        }

        if (hasAnyMode)
        {
            salidaIntermediaPunch = ordered
                .Where(x => x.CheckTime > entradaPunch.CheckTime)
                .FirstOrDefault(x => IsBreakOutMode(x.InOutMode));

            if (salidaIntermediaPunch is not null)
            {
                entradaIntermediaPunch = ordered
                    .Where(x => x.CheckTime > salidaIntermediaPunch.CheckTime)
                    .FirstOrDefault(x => IsBreakInMode(x.InOutMode));
            }

            var salidaSearchStart = entradaIntermediaPunch?.CheckTime
                ?? salidaIntermediaPunch?.CheckTime
                ?? entradaPunch.CheckTime;

            salidaPunch = ordered
                .Where(x => x.CheckTime > salidaSearchStart)
                .FirstOrDefault(x => IsFinalOutMode(x.InOutMode));

            if (salidaPunch is null)
            {
                salidaPunch = ordered
                    .Where(x => x.CheckTime > salidaSearchStart)
                    .LastOrDefault(x => IsOutMode(x.InOutMode));

                if (salidaPunch is null)
                {
                    salidaPunch = ResolveChronologicalOutFallback(ordered, entradaPunch, salidaSearchStart);
                    usedChronologicalOutFallback = salidaPunch is not null;
                }

                if (salidaPunch is not null && salidaPunch.CheckTime > entradaPunch.CheckTime)
                {
                    if (!usedChronologicalOutFallback)
                    {
                        usedFallbackOutMode = true;
                    }
                }
                else if (CanTreatSingleBreakOutAsFinalOut(ordered, entradaPunch, salidaIntermediaPunch, entradaIntermediaPunch))
                {
                    salidaPunch = salidaIntermediaPunch;
                    salidaIntermediaPunch = null;
                    entradaIntermediaPunch = null;
                    usedSingleBreakOutAsFinalOut = true;
                }
                else if (HasInvertedModeOrdering(ordered))
                {
                    entradaPunch = ordered.First();
                    salidaPunch = ordered.Last();
                    salidaIntermediaPunch = null;
                    entradaIntermediaPunch = null;
                    hasAnyMode = false;
                    usedModeOrderFallback = true;
                }
                else if (salidaIntermediaPunch is not null)
                {
                    return BuildIncomplete(day, "INCIDENCIA", "Se detecto salida intermedia (modo 2) sin salida final (modo 1). No se cierra jornada.", ordered);
                }
                else if (!ordered.Any(x => IsOutMode(x.InOutMode)))
                {
                    return BuildIncomplete(day, "INCIDENCIA", "Solo se detectaron entradas; no hay salida valida segun modo del checador.", ordered);
                }
                else
                {
                    return BuildIncomplete(day, "INCIDENCIA", "No se detecto salida final valida (modo 1).", ordered);
                }
            }
        }
        else
        {
            salidaPunch = ordered.Last();
        }

        if (salidaPunch is null)
        {
            return BuildIncomplete(day, "INCIDENCIA", "No se pudo determinar salida valida para la jornada.", ordered);
        }

        if (isDayShiftContext
            && salidaPunch.CheckTime.TimeOfDay <= MiddayBoundary)
        {
            var latestAfternoonPunch = ordered
                .Where(x => x.CheckTime > entradaPunch.CheckTime && x.CheckTime.TimeOfDay > MiddayBoundary)
                .OrderBy(x => x.CheckTime)
                .LastOrDefault();

            if (latestAfternoonPunch is not null
                && (latestAfternoonPunch.CheckTime - entradaPunch.CheckTime).TotalMinutes >= MinChronologicalFallbackSpanMinutes)
            {
                salidaPunch = latestAfternoonPunch;
                usedTemporalExitGuard = true;
            }
        }

        var entrada = entradaPunch.CheckTime;
        var salida = salidaPunch.CheckTime;
        var total = salida - entrada;
        var normalizedInvertedModePair = usedChronologicalOutFallback
            && hasAnyMode
            && IsClearlyValidInvertedModePair(ordered, entradaPunch, salidaPunch, !isDayShiftContext);

        if (total.TotalMinutes <= 0)
        {
            return BuildIncomplete(day, "INCIDENCIA", "El calculo de horas resulto no valido.", ordered);
        }

        if (isDayShiftContext && total.TotalMinutes > MaxClearlyValidShiftSpanMinutes)
        {
            return BuildIncomplete(day, "INCIDENCIA", "Jornada diurna fuera de ventana valida; se evita clasificacion de alto impacto.", ordered);
        }

        var intermediatePunches = ordered
            .Where(x => x.CheckTime > entrada && x.CheckTime < salida)
            .OrderBy(x => x.CheckTime)
            .ToList();

        DateTime? salidaComida = salidaIntermediaPunch?.CheckTime;
        DateTime? entradaComida = entradaIntermediaPunch?.CheckTime;

        if (hasAnyMode && intermediatePunches.Any(x => x.InOutMode.HasValue))
        {
            if (!salidaComida.HasValue)
            {
                var salidaIntermedia = intermediatePunches.FirstOrDefault(x => IsBreakOutMode(x.InOutMode));
                salidaComida = salidaIntermedia?.CheckTime;
            }

            if (salidaComida.HasValue && !entradaComida.HasValue)
            {
                var entradaIntermedia = intermediatePunches
                    .Where(x => x.CheckTime > salidaComida.Value)
                    .FirstOrDefault(x => IsBreakInMode(x.InOutMode));
                entradaComida = entradaIntermedia?.CheckTime;
            }
        }
        else
        {
            salidaComida = intermediatePunches.Count >= 1 ? intermediatePunches[0].CheckTime : null;
            entradaComida = intermediatePunches.Count >= 2 ? intermediatePunches[1].CheckTime : null;
        }

        if (isDayShiftContext && salidaComida.HasValue)
        {
            var minutesAfterEntry = (salidaComida.Value - entrada).TotalMinutes;
            var minutesBeforeExit = (salida - salidaComida.Value).TotalMinutes;
            if (minutesAfterEntry < MinBreakOutAfterEntryMinutes
                || minutesBeforeExit < MinBreakOutBeforeExitMinutes)
            {
                salidaComida = null;
                entradaComida = null;
                usedTemporalLunchGuard = true;
            }
        }

        if (salidaComida.HasValue && entradaComida.HasValue && entradaComida.Value <= salidaComida.Value)
        {
            entradaComida = null;
            usedTemporalLunchGuard = true;
        }

        var horasTrabajadas = RoundHours((decimal)total.TotalMinutes / 60m);
        var standardShiftHours = ResolveStandardShiftHours(entradaPunch);
        var overtimeAuthorized = entradaPunch.OvertimeAuthorized ?? false;
        var horasExtra = ResolveExtraHours(
            entradaPunch,
            day.Date,
            salida,
            horasTrabajadas,
            overtimeAuthorized,
            standardShiftHours);

        var analysisNotes = BuildIntermediateAnalysisNotes(intermediatePunches.Select(x => x.CheckTime).ToList(), salidaComida, entradaComida, hasAnyMode, overtimeAuthorized, standardShiftHours, horasExtra);
        var status = hasAnyMode
            ? ResolveStatusFromMode(salidaComida, entradaComida)
            : ResolveStatus(ordered.Count, intermediatePunches.Count);

        if (usedFallbackOutMode)
        {
            status = "INCIDENCIA";
            analysisNotes = string.Concat(
                analysisNotes,
                " No se detecto salida final en modo 1; se calculo jornada usando la ultima salida valida disponible.");
        }

        if (usedChronologicalOutFallback)
        {
            if (normalizedInvertedModePair)
            {
                analysisNotes = string.Concat(
                    analysisNotes,
                    " Se detecto par invertido m1->m0 con cronologia valida; se normalizo como jornada continua.");
            }
            else
            {
                status = "INCIDENCIA";
                analysisNotes = string.Concat(
                    analysisNotes,
                    " No se detecto salida por modo despues de la entrada; se tomo la ultima marca cronologica por inconsistencia de modos.");
            }
        }

        if (usedSingleBreakOutAsFinalOut)
        {
            analysisNotes = string.Concat(
                analysisNotes,
                " Se detecto una sola salida en modo 2 sin marcas posteriores; se tomo como salida final para evitar incidencia falsa.");
        }

        if (usedModeOrderFallback)
        {
            analysisNotes = string.Concat(
                analysisNotes,
                " Se detecto inversion de modos (entrada/salida) y se aplico clasificacion cronologica para preservar horas y extra.");
        }

        if (usedTemporalEntryGuard || usedTemporalExitGuard)
        {
            status = "INCIDENCIA";
            analysisNotes = string.Concat(
                analysisNotes,
                " Se aplicaron validaciones temporales de turno diurno para corregir entrada/salida incongruente.");
        }

        if (usedTemporalLunchGuard)
        {
            analysisNotes = string.Concat(
                analysisNotes,
                " Se ignoraron marcas de comida fuera de ventana valida para evitar clasificacion ilogica.");
        }

        return new AttendanceDayResult
        {
            UserId = day.UserId,
            EmployeeName = day.EmployeeName,
            Date = day.Date,
            Entrada = entrada,
            SalidaComida = salidaComida,
            EntradaComida = entradaComida,
            Salida = salida,
            HorasTrabajadas = horasTrabajadas,
            HorasExtra = horasExtra,
            Estatus = status,
            Observaciones = analysisNotes
        };
    }

    private string ResolveStatus(int punchesCount, int intermediateCount)
    {
        if (_options.UseStrictFourPunchPolicy)
        {
            return punchesCount switch
            {
                2 => "JORNADA_CONTINUA",
                4 => "OK",
                _ => "INCIDENCIA"
            };
        }

        if (intermediateCount == 0)
        {
            return "JORNADA_CONTINUA";
        }

        return intermediateCount % 2 == 0 ? "OK" : "INCIDENCIA";
    }

    private static string ResolveStatusFromMode(DateTime? salidaComida, DateTime? entradaComida)
    {
        if (!salidaComida.HasValue)
        {
            return "JORNADA_CONTINUA";
        }

        return entradaComida.HasValue ? "OK" : "INCIDENCIA";
    }

    private static string BuildIntermediateAnalysisNotes(
        IReadOnlyList<DateTime> intermediatePunches,
        DateTime? salidaComida,
        DateTime? entradaComida,
        bool usedInOutMode,
        bool overtimeAuthorized,
        decimal standardShiftHours,
        decimal horasExtra)
    {
        var notes = new List<string>
        {
            "Calculo base: primera marca = entrada y ultima marca = salida."
        };

        notes.Add(usedInOutMode
            ? "Clasificacion de intermedias usando modo de checador (entrada/salida)."
            : "Clasificacion de intermedias por orden cronologico (sin modo disponible).");

        if (intermediatePunches.Count == 0)
        {
            notes.Add("Sin marcas intermedias.");
            return string.Join(" ", notes);
        }

        notes.Add($"Marcas intermedias detectadas: {intermediatePunches.Count}.");

        if (salidaComida.HasValue)
        {
            notes.Add($"Salida intermedia detectada: {salidaComida.Value:HH:mm}.");
        }

        if (entradaComida.HasValue)
        {
            notes.Add($"Entrada intermedia detectada: {entradaComida.Value:HH:mm}.");

            if (salidaComida.HasValue && entradaComida.Value > salidaComida.Value)
            {
                var comida = entradaComida.Value - salidaComida.Value;
                notes.Add($"Duracion estimada de comida: {comida.Hours:D2}:{comida.Minutes:D2}.");
            }
        }
        else
        {
            notes.Add("Marca intermedia impar: no se detecto regreso intermedio emparejado.");
        }

        if (intermediatePunches.Count > 2)
        {
            var extras = intermediatePunches
                .Skip(2)
                .Select(t => t.ToString("HH:mm"))
                .ToArray();
            notes.Add($"Marcas intermedias adicionales ({extras.Length}): {string.Join(", ", extras)}.");
        }

        notes.Add($"Jornada estandar aplicada: {standardShiftHours:0.##} h.");
        notes.Add(overtimeAuthorized
            ? $"Horas extra autorizadas para este empleado. Extra calculada: {horasExtra:0.##} h."
            : "Horas extra no autorizadas para este empleado; extra reportada en 0.00 h.");

        return string.Join(" ", notes);
    }

    private AttendanceDayResult BuildIncomplete(
        DailyPunches day,
        string status,
        string message,
        IReadOnlyList<PunchRecord>? orderedPunches = null)
    {
        var ordered = orderedPunches
            ?.OrderBy(x => x.CheckTime)
            .ToList()
            ?? day.Punches.OrderBy(x => x.CheckTime).ToList();

        var (entrada, salidaComida, entradaComida, salida) = MapPunchesForIncomplete(ordered);

        return new AttendanceDayResult
        {
            UserId = day.UserId,
            EmployeeName = day.EmployeeName,
            Date = day.Date,
            Entrada = entrada,
            SalidaComida = salidaComida,
            EntradaComida = entradaComida,
            Salida = salida,
            HorasTrabajadas = null,
            HorasExtra = null,
            Estatus = status,
            Observaciones = message
        };
    }

    private static (DateTime? Entrada, DateTime? SalidaComida, DateTime? EntradaComida, DateTime? Salida) MapPunchesForIncomplete(IReadOnlyList<PunchRecord> ordered)
    {
        if (ordered.Count == 0)
        {
            return (null, null, null, null);
        }

        if (ordered.Count == 1)
        {
            var singlePunch = ordered[0];
            if (ShouldTreatSinglePunchAsExitInDayShift(singlePunch))
            {
                return (null, null, null, singlePunch.CheckTime);
            }
        }

        var hasAnyMode = ordered.Any(x => x.InOutMode.HasValue);
        if (!hasAnyMode)
        {
            if (ordered.Count == 1)
            {
                var singlePunch = ordered[0];
                var isNightShiftContext = singlePunch.IsNightShift == true || singlePunch.WorkScheduleCode == 3;
                if (!isNightShiftContext && singlePunch.CheckTime.TimeOfDay >= MiddayBoundary)
                {
                    return (null, null, null, singlePunch.CheckTime);
                }
            }

            return (
                ordered.Count > 0 ? ordered[0].CheckTime : null,
                ordered.Count > 1 ? ordered[1].CheckTime : null,
                ordered.Count > 2 ? ordered[2].CheckTime : null,
                ordered.Count > 3 ? ordered[3].CheckTime : null);
        }

        var entradaPunch = ResolveEntryPunch(ordered, hasAnyMode);
        if (entradaPunch is null)
        {
            var salidaComidaSolo = ordered.FirstOrDefault(x => IsBreakOutMode(x.InOutMode));
            var entradaComidaSolo = salidaComidaSolo is not null
                ? ordered
                    .Where(x => x.CheckTime > salidaComidaSolo.CheckTime)
                    .FirstOrDefault(x => IsBreakInMode(x.InOutMode))
                : null;

            var salidaSolo = ordered
                .FirstOrDefault(x => IsFinalOutMode(x.InOutMode))
                ?? ordered.LastOrDefault(x => IsOutMode(x.InOutMode));

            return (
                null,
                salidaComidaSolo?.CheckTime,
                entradaComidaSolo?.CheckTime,
                salidaSolo?.CheckTime);
        }

        var salidaComidaPunch = ordered
            .Where(x => x.CheckTime > entradaPunch.CheckTime)
            .FirstOrDefault(x => IsBreakOutMode(x.InOutMode));

        var entradaComidaPunch = salidaComidaPunch is not null
            ? ordered
                .Where(x => x.CheckTime > salidaComidaPunch.CheckTime)
                .FirstOrDefault(x => IsBreakInMode(x.InOutMode))
            : null;

        var salidaSearchStart = entradaComidaPunch?.CheckTime
            ?? salidaComidaPunch?.CheckTime
            ?? entradaPunch.CheckTime;

        var salidaPunch = ordered
            .Where(x => x.CheckTime > salidaSearchStart)
            .FirstOrDefault(x => IsFinalOutMode(x.InOutMode))
            ?? ordered
                .Where(x => x.CheckTime > entradaPunch.CheckTime)
                .LastOrDefault(x => IsOutMode(x.InOutMode))
            ?? ResolveChronologicalOutFallback(ordered, entradaPunch, salidaSearchStart);

        if (salidaComidaPunch is not null
            && entradaComidaPunch is not null
            && entradaComidaPunch.CheckTime <= salidaComidaPunch.CheckTime)
        {
            entradaComidaPunch = null;
        }

        if (salidaPunch is not null
            && salidaPunch.CheckTime <= entradaPunch.CheckTime)
        {
            salidaPunch = null;
        }

        if (salidaPunch is not null
            && entradaComidaPunch is not null
            && salidaPunch.CheckTime <= entradaComidaPunch.CheckTime)
        {
            salidaPunch = null;
        }

        if (salidaPunch is not null
            && salidaComidaPunch is not null
            && salidaPunch.CheckTime == salidaComidaPunch.CheckTime)
        {
            if (entradaComidaPunch is null)
            {
                salidaComidaPunch = null;
            }
            else
            {
                salidaPunch = null;
            }
        }

        return (
            entradaPunch.CheckTime,
            salidaComidaPunch?.CheckTime,
            entradaComidaPunch?.CheckTime,
            salidaPunch?.CheckTime);
    }

    private IReadOnlyList<PunchWithWorkDate> AssignWorkDatesForUser(IReadOnlyList<PunchRecord> orderedByTime)
    {
        var result = new List<PunchWithWorkDate>(orderedByTime.Count);
        foreach (var punch in orderedByTime)
        {
            var workDate = ResolveWorkDateForGrouping(punch);
            result.Add(new PunchWithWorkDate(punch.UserId, workDate, punch));
        }

        return result;
    }

    private DateOnly ResolveWorkDateForGrouping(PunchRecord punch)
    {
        var workDate = DateOnly.FromDateTime(punch.CheckTime);
        if (!IsNightShiftPunch(punch))
        {
            return workDate;
        }

        var carryUntilHour = ClampHour(_options.NightShiftCarryToPreviousDayUntilHour);
        var carryUntil = TimeSpan.FromHours(carryUntilHour);
        if (punch.CheckTime.TimeOfDay < carryUntil)
        {
            return workDate.AddDays(-1);
        }

        return workDate;
    }

    private decimal ResolveStandardShiftHours(PunchRecord entryPunch)
    {
        if (entryPunch.StandardShiftHours.HasValue && entryPunch.StandardShiftHours.Value > 0m)
        {
            return entryPunch.StandardShiftHours.Value;
        }

        if (IsNightShiftPunch(entryPunch))
        {
            return _options.NightShiftStandardHours > 0m
                ? _options.NightShiftStandardHours
                : _options.StandardWorkDayHours;
        }

        return _options.StandardWorkDayHours;
    }

    private decimal ResolveExtraHours(
        PunchRecord entryPunch,
        DateOnly workDate,
        DateTime exitTime,
        decimal workedHours,
        bool overtimeAuthorized,
        decimal standardShiftHours)
    {
        if (!overtimeAuthorized)
        {
            return 0m;
        }

        var rawExtraHours = workedHours - standardShiftHours;
        if (rawExtraHours <= 0m)
        {
            return 0m;
        }

        var extraCountStart = ResolveExtraCountingStartTime(entryPunch, workDate);
        if (extraCountStart.HasValue && entryPunch.CheckTime < extraCountStart.Value)
        {
            var nonExtraEnd = exitTime < extraCountStart.Value
                ? exitTime
                : extraCountStart.Value;

            if (nonExtraEnd > entryPunch.CheckTime)
            {
                var earlyNonExtraHours = (decimal)(nonExtraEnd - entryPunch.CheckTime).TotalMinutes / 60m;
                rawExtraHours -= earlyNonExtraHours;
            }
        }

        if (rawExtraHours <= 0m)
        {
            return 0m;
        }

        var roundedExtraHours = RoundHours(rawExtraHours);
        if (roundedExtraHours <= 0m)
        {
            return 0m;
        }

        var maxExtraHours = ResolveExtraHoursCap(entryPunch);
        if (maxExtraHours.HasValue && roundedExtraHours > maxExtraHours.Value)
        {
            roundedExtraHours = maxExtraHours.Value;
        }

        return roundedExtraHours;
    }

    private DateTime? ResolveExtraCountingStartTime(PunchRecord entryPunch, DateOnly workDate)
    {
        if (!entryPunch.WorkScheduleCode.HasValue)
        {
            return null;
        }

        var hour = entryPunch.WorkScheduleCode.Value switch
        {
            1 => ClampHour(_options.Schedule1ExtraCountFromHour),
            2 => ClampHour(_options.Schedule2ExtraCountFromHour),
            3 => ClampHour(_options.Schedule3ExtraCountFromHour),
            _ => -1
        };

        if (hour < 0)
        {
            return null;
        }

        return workDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(hour)));
    }

    private decimal? ResolveExtraHoursCap(PunchRecord entryPunch)
    {
        if (!entryPunch.WorkScheduleCode.HasValue)
        {
            return null;
        }

        var cap = entryPunch.WorkScheduleCode.Value switch
        {
            1 => _options.Schedule1ExtraMaxHours,
            2 => _options.Schedule2ExtraMaxHours,
            3 => _options.Schedule3ExtraMaxHours,
            _ => 0m
        };

        return cap > 0m ? cap : null;
    }

    private decimal RoundHours(decimal hours)
    {
        if (_options.RoundToMinutes <= 0)
        {
            return decimal.Round(hours, 2, MidpointRounding.AwayFromZero);
        }

        var totalMinutes = hours * 60m;
        var factor = _options.RoundToMinutes;
        var roundedMinutes = Math.Round(totalMinutes / factor, MidpointRounding.AwayFromZero) * factor;
        return decimal.Round(roundedMinutes / 60m, 2, MidpointRounding.AwayFromZero);
    }

    private bool IsNightShiftPunch(PunchRecord punch)
    {
        if (!_options.EnableNightShiftSupport)
        {
            return false;
        }

        if (punch.IsNightShift.HasValue)
        {
            return punch.IsNightShift.Value;
        }

        if (!punch.WorkScheduleCode.HasValue)
        {
            return false;
        }

        return punch.WorkScheduleCode.Value == _options.NightShiftScheduleCode;
    }

    private static bool HasInvertedModeOrdering(IReadOnlyList<PunchRecord> ordered)
    {
        var firstInIndex = -1;
        var firstOutIndex = -1;

        for (var i = 0; i < ordered.Count; i++)
        {
            var mode = ordered[i].InOutMode;

            if (firstInIndex < 0 && IsInMode(mode))
            {
                firstInIndex = i;
            }

            if (firstOutIndex < 0 && IsOutMode(mode))
            {
                firstOutIndex = i;
            }
        }

        return firstInIndex >= 0
            && firstOutIndex >= 0
            && firstOutIndex < firstInIndex;
    }

    private static bool CanTreatSingleBreakOutAsFinalOut(
        IReadOnlyList<PunchRecord> ordered,
        PunchRecord entradaPunch,
        PunchRecord? salidaIntermediaPunch,
        PunchRecord? entradaIntermediaPunch)
    {
        if (salidaIntermediaPunch is null || entradaIntermediaPunch is not null)
        {
            return false;
        }

        var outPunchesAfterEntry = ordered
            .Where(x => x.CheckTime > entradaPunch.CheckTime && IsOutMode(x.InOutMode))
            .OrderBy(x => x.CheckTime)
            .ToList();

        if (outPunchesAfterEntry.Count != 1)
        {
            return false;
        }

        var onlyOut = outPunchesAfterEntry[0];
        if (onlyOut.CheckTime != salidaIntermediaPunch.CheckTime)
        {
            return false;
        }

        return !ordered.Any(x => x.CheckTime > onlyOut.CheckTime);
    }

    private static PunchRecord? ResolveEntryPunch(IReadOnlyList<PunchRecord> ordered, bool hasAnyMode)
    {
        if (ordered.Count == 0)
        {
            return null;
        }

        var firstPunch = ordered[0];

        if (!hasAnyMode)
        {
            return firstPunch;
        }

        var modeBasedEntry = ordered.FirstOrDefault(x => IsEntryStartMode(x.InOutMode))
            ?? ordered.FirstOrDefault(x => IsInMode(x.InOutMode))
            ?? ordered.FirstOrDefault(x => !x.InOutMode.HasValue);

        if (modeBasedEntry is not null)
        {
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

            if (modeBasedEntry.InOutMode == 3)
            {
                var breakOutBeforeBreakIn = ordered
                    .Where(x => x.CheckTime < modeBasedEntry.CheckTime)
                    .LastOrDefault(x => IsBreakOutMode(x.InOutMode));

                if (breakOutBeforeBreakIn is not null)
                {
                    var breakPairGapMinutes = (modeBasedEntry.CheckTime - breakOutBeforeBreakIn.CheckTime).TotalMinutes;
                    var hasFinalOutAfterBreakIn = ordered.Any(x => x.CheckTime > modeBasedEntry.CheckTime && IsFinalOutMode(x.InOutMode));

                    if (hasFinalOutAfterBreakIn && breakPairGapMinutes <= MaxBreakPairGapMinutesForMissingEntry)
                    {
                        return null;
                    }
                }
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

        return preferredWindow.FirstOrDefault(x => IsEntryStartMode(x.InOutMode))
            ?? preferredWindow.FirstOrDefault(x => IsInMode(x.InOutMode))
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

        if (!punchesBeforeModeEntry.All(x => IsOutMode(x.InOutMode)))
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
            if (singlePunch.InOutMode is not 1 and not 2)
            {
                return false;
            }

            if (ordered.Any(x => x.IsNightShift == true || x.WorkScheduleCode == 3))
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

        if (ordered.Any(x => IsInMode(x.InOutMode) || !x.InOutMode.HasValue))
        {
            return false;
        }

        var spanMinutes = (ordered[^1].CheckTime - ordered[0].CheckTime).TotalMinutes;
        return spanMinutes >= MinChronologicalFallbackSpanMinutes;
    }

    private static PunchRecord? ResolveChronologicalOutFallback(
        IReadOnlyList<PunchRecord> ordered,
        PunchRecord entradaPunch,
        DateTime salidaSearchStart)
    {
        if (ordered.Count == 0)
        {
            return null;
        }

        var lastPunch = ordered[^1];
        if (lastPunch.CheckTime <= salidaSearchStart || lastPunch.CheckTime <= entradaPunch.CheckTime)
        {
            return null;
        }

        if (IsOutMode(lastPunch.InOutMode))
        {
            return lastPunch;
        }

        var spanMinutes = (lastPunch.CheckTime - entradaPunch.CheckTime).TotalMinutes;
        if (IsInMode(lastPunch.InOutMode)
            && !CanUseInModeAsChronologicalOut(ordered, entradaPunch, lastPunch, spanMinutes))
        {
            return null;
        }

        return spanMinutes >= MinChronologicalFallbackSpanMinutes
            ? lastPunch
            : null;
    }

    private static bool CanUseInModeAsChronologicalOut(
        IReadOnlyList<PunchRecord> ordered,
        PunchRecord entradaPunch,
        PunchRecord outCandidate,
        double spanMinutes)
    {
        if (spanMinutes < MinChronologicalFallbackSpanMinutes || spanMinutes > MaxClearlyValidShiftSpanMinutes)
        {
            return false;
        }

        var isNightShiftContext = ordered.Any(x => x.IsNightShift == true || x.WorkScheduleCode == 3);
        if (isNightShiftContext)
        {
            return true;
        }

        return entradaPunch.CheckTime.TimeOfDay < MiddayBoundary
            && outCandidate.CheckTime.TimeOfDay > MiddayBoundary;
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
        if (!IsOutMode(first.InOutMode) || !IsInMode(last.InOutMode))
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

    private static bool ShouldTreatSinglePunchAsExitInDayShift(PunchRecord punch)
    {
        if (punch.IsNightShift == true || punch.WorkScheduleCode == 3)
        {
            return false;
        }

        return punch.CheckTime.TimeOfDay >= MiddayBoundary;
    }

    private static int ClampHour(int hour)
        => Math.Clamp(hour, 0, 23);

    private readonly record struct PunchWithWorkDate(int UserId, DateOnly WorkDate, PunchRecord Punch);

    private static List<PunchRecord> CollapsePunchesByMinute(IEnumerable<PunchRecord> punches)
    {
        return punches
            .OrderBy(x => x.CheckTime)
            .GroupBy(x => new DateTime(x.CheckTime.Year, x.CheckTime.Month, x.CheckTime.Day, x.CheckTime.Hour, x.CheckTime.Minute, 0))
            .Select(ChooseBestPunchOfMinute)
            .OrderBy(x => x.CheckTime)
            .ToList();
    }

    private static PunchRecord ChooseBestPunchOfMinute(IGrouping<DateTime, PunchRecord> minuteGroup)
    {
        var ordered = minuteGroup
            .OrderBy(x => ModePriority(x.InOutMode))
            .ThenBy(x => x.CheckTime)
            .ToList();

        return ordered[0];
    }

    private static int ModePriority(int? mode)
    {
        return mode switch
        {
            0 => 1,
            3 => 2,
            1 => 3,
            2 => 4,
            _ => 5
        };
    }

    private static bool IsInMode(int? mode)
        => mode is 0 or 3;

    private static bool IsOutMode(int? mode)
        => mode is 1 or 2;

    private static bool IsEntryStartMode(int? mode)
        => mode is 0;

    private static bool IsFinalOutMode(int? mode)
        => mode is 1;

    private static bool IsBreakOutMode(int? mode)
        => mode is 2;

    private static bool IsBreakInMode(int? mode)
        => mode is 3;
}

