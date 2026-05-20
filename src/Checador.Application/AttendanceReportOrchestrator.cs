using Checador.Domain.Interfaces;
using Checador.Domain.Models;

namespace Checador.Application;

public sealed class AttendanceReportOrchestrator
{
    private readonly IAttendanceDataSource _dataSource;
    private readonly AttendanceProcessor _processor;
    private readonly IReportGenerator _reportGenerator;

    public AttendanceReportOrchestrator(
        IAttendanceDataSource dataSource,
        AttendanceProcessor processor,
        IReportGenerator reportGenerator)
    {
        _dataSource = dataSource;
        _processor = processor;
        _reportGenerator = reportGenerator;
    }

    public async Task<string> GenerateAsync(
        ReportRequest request,
        CancellationToken cancellationToken = default,
        ReportOutputFormat outputFormat = ReportOutputFormat.Excel)
    {
        var summary = await GenerateWithSummaryAsync(request, cancellationToken, outputFormat);
        return summary.ReportPaths.First();
    }

    public async Task<ReportRunSummary> GenerateWithSummaryAsync(
        ReportRequest request,
        CancellationToken cancellationToken = default,
        ReportOutputFormat outputFormat = ReportOutputFormat.Excel)
    {
        var preview = await BuildPreviewAsync(request, cancellationToken);

        var reportPath = await _reportGenerator.GenerateWeeklyReportAsync(
            preview.Days,
            request.OutputDirectory,
            request.TemplatePath,
            new ReportTemplateContext
            {
                EmployeeName = preview.EmployeeName,
                Company = preview.Company,
                Area = preview.Area,
                Position = preview.Position,
                WorkScheduleCode = preview.WorkScheduleCode,
                WorkScheduleDisplay = preview.WorkScheduleDisplay,
                WorkShiftType = preview.WorkShiftType
            },
            cancellationToken,
            outputFormat);

        var hasAnyRecord = preview.Days.Any(d => d.Estatus != "SIN_REGISTRO");
        return new ReportRunSummary
        {
            EmployeesProcessed = 1,
            EmployeesWithoutRecords = hasAnyRecord ? 0 : 1,
            TotalWorkedHours = preview.TotalWorkedHours,
            TotalExtraHours = preview.TotalExtraHours,
            ReportPaths = new[] { reportPath }
        };
    }

    public async Task<ReportPreviewResult> BuildPreviewAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        var punches = await _dataSource.GetPunchesAsync(
            request.UserId,
            request.FechaInicio,
            request.FechaFin,
            cancellationToken);
        var rawPunches = punches;

        if (request.UseSimulatedDataWhenEmpty && punches.Count == 0)
        {
            var simulatedEmployeeName = await ResolveEmployeeNameAsync(request.UserId, request.EmployeeName, cancellationToken);
            punches = BuildSimulatedPunches(
                request.UserId,
                simulatedEmployeeName,
                request.FechaInicio,
                request.FechaFin,
                request.WorkScheduleCode);
        }

        var processed = _processor.Process(punches);
        var employeeName = request.EmployeeName;
        if (string.IsNullOrWhiteSpace(employeeName))
        {
            employeeName = processed.FirstOrDefault()?.EmployeeName;
        }

        if (string.IsNullOrWhiteSpace(employeeName))
        {
            var employees = await _dataSource.GetEmployeesAsync(cancellationToken);
            employeeName = employees.FirstOrDefault(e => e.UserId == request.UserId)?.EmployeeName ?? $"Empleado_{request.UserId}";
        }

        processed = EnsureDateCoverage(processed, request.FechaInicio, request.FechaFin, request.UserId, employeeName);

        var derivedScheduleCode = request.WorkScheduleCode ?? rawPunches
            .Select(p => p.WorkScheduleCode)
            .FirstOrDefault(code => code.HasValue)
            ?? punches.Select(p => p.WorkScheduleCode).FirstOrDefault(code => code.HasValue);

        var derivedShiftType = !string.IsNullOrWhiteSpace(request.WorkShiftType)
            ? request.WorkShiftType
            : ResolveShiftType(derivedScheduleCode);

        var derivedScheduleDisplay = !string.IsNullOrWhiteSpace(request.WorkScheduleDisplay)
            ? request.WorkScheduleDisplay
            : (derivedScheduleCode.HasValue ? $"Horario {derivedScheduleCode.Value}" : null);

        return new ReportPreviewResult
        {
            UserId = request.UserId,
            EmployeeName = employeeName,
            Company = request.Company,
            Area = request.Area,
            Position = request.Position,
            WorkScheduleCode = derivedScheduleCode,
            WorkScheduleDisplay = derivedScheduleDisplay,
            WorkShiftType = derivedShiftType,
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            TotalWorkedHours = processed.Where(d => d.HorasTrabajadas.HasValue).Sum(d => d.HorasTrabajadas!.Value),
            TotalExtraHours = processed.Where(d => d.HorasExtra.HasValue).Sum(d => d.HorasExtra!.Value),
            Days = processed
        };
    }

    public async Task<IReadOnlyList<string>> GenerateForAllAsync(
        ReportRequest request,
        CancellationToken cancellationToken = default,
        ReportOutputFormat outputFormat = ReportOutputFormat.Excel)
    {
        var summary = await GenerateForAllWithSummaryAsync(request, cancellationToken, outputFormat);
        return summary.ReportPaths;
    }

    public async Task<IReadOnlyList<ReportPreviewResult>> BuildPreviewForAllAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        var employees = await _dataSource.GetEmployeesAsync(cancellationToken);
        var punches = await _dataSource.GetPunchesAsync(
            null,
            request.FechaInicio,
            request.FechaFin,
            cancellationToken);

        if (employees.Count == 0 && punches.Count == 0)
        {
            throw new InvalidOperationException("No se encontraron empleados ni marcas en el rango seleccionado.");
        }

        var employeeNameByUserId = employees
            .GroupBy(e => e.UserId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Select(x => x.EmployeeName)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                    ?? $"Empleado_{g.Key}");

        foreach (var groupedPunches in punches.GroupBy(p => p.UserId))
        {
            if (employeeNameByUserId.ContainsKey(groupedPunches.Key))
            {
                continue;
            }

            var inferredName = groupedPunches
                .OrderByDescending(p => p.CheckTime)
                .Select(p => p.EmployeeName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? $"Empleado_{groupedPunches.Key}";

            employeeNameByUserId[groupedPunches.Key] = inferredName;
        }

        var mergedEmployees = employeeNameByUserId
            .OrderBy(x => x.Key)
            .Select(x => new EmployeeInfo(x.Key, x.Value))
            .ToList();

        var punchesByUser = punches
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        var previews = new List<ReportPreviewResult>(mergedEmployees.Count);

        foreach (var employee in mergedEmployees)
        {
            punchesByUser.TryGetValue(employee.UserId, out var employeePunches);
            var employeePunchesList = (employeePunches ?? Enumerable.Empty<PunchRecord>())
                .OrderBy(p => p.CheckTime)
                .ToList();

            if (request.UseSimulatedDataWhenEmpty && employeePunchesList.Count == 0)
            {
                employeePunchesList = BuildSimulatedPunches(
                    employee.UserId,
                    employee.EmployeeName,
                    request.FechaInicio,
                    request.FechaFin,
                    request.WorkScheduleCode).ToList();
            }

            var processed = _processor.Process(employeePunchesList);
            processed = EnsureDateCoverage(processed, request.FechaInicio, request.FechaFin, employee.UserId, employee.EmployeeName);

            var derivedScheduleCode = employeePunchesList
                .Select(p => p.WorkScheduleCode)
                .FirstOrDefault(code => code.HasValue);

            var derivedShiftType = ResolveShiftType(derivedScheduleCode);
            var derivedScheduleDisplay = derivedScheduleCode.HasValue
                ? $"Horario {derivedScheduleCode.Value}"
                : null;

            previews.Add(new ReportPreviewResult
            {
                UserId = employee.UserId,
                EmployeeName = employee.EmployeeName,
                Company = request.Company,
                Area = request.Area,
                Position = request.Position,
                WorkScheduleCode = derivedScheduleCode,
                WorkScheduleDisplay = derivedScheduleDisplay,
                WorkShiftType = derivedShiftType,
                FechaInicio = request.FechaInicio,
                FechaFin = request.FechaFin,
                TotalWorkedHours = processed.Where(d => d.HorasTrabajadas.HasValue).Sum(d => d.HorasTrabajadas!.Value),
                TotalExtraHours = processed.Where(d => d.HorasExtra.HasValue).Sum(d => d.HorasExtra!.Value),
                Days = processed
            });
        }

        return previews;
    }

    public async Task<ReportRunSummary> BuildPreviewForAllWithSummaryAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        var previews = await BuildPreviewForAllAsync(request, cancellationToken);

        var employeesWithoutRecords = previews.Count(p => p.Days.All(d => d.Estatus == "SIN_REGISTRO"));
        var totalWorkedHours = previews.Sum(p => p.TotalWorkedHours);
        var totalExtraHours = previews.Sum(p => p.TotalExtraHours);

        return new ReportRunSummary
        {
            EmployeesProcessed = previews.Count,
            EmployeesWithoutRecords = employeesWithoutRecords,
            TotalWorkedHours = totalWorkedHours,
            TotalExtraHours = totalExtraHours,
            ReportPaths = Array.Empty<string>()
        };
    }

    public async Task<ReportRunSummary> GenerateForAllWithSummaryAsync(
        ReportRequest request,
        CancellationToken cancellationToken = default,
        ReportOutputFormat outputFormat = ReportOutputFormat.Excel)
    {
        var employees = await _dataSource.GetEmployeesAsync(cancellationToken);
        if (employees.Count == 0)
        {
            throw new InvalidOperationException("No se encontraron empleados en USERINFO.");
        }

        var punches = await _dataSource.GetPunchesAsync(
            null,
            request.FechaInicio,
            request.FechaFin,
            cancellationToken);

        var punchesByUser = punches
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        var reports = new List<string>();
        var employeesWithoutRecords = 0;
        decimal totalWorkedHours = 0m;
        decimal totalExtraHours = 0m;

        foreach (var employee in employees.OrderBy(e => e.UserId))
        {
            punchesByUser.TryGetValue(employee.UserId, out var employeePunches);
            employeePunches ??= Enumerable.Empty<PunchRecord>();
            var employeePunchesList = employeePunches.OrderBy(p => p.CheckTime).ToList();

            if (request.UseSimulatedDataWhenEmpty && employeePunchesList.Count == 0)
            {
                employeePunchesList = BuildSimulatedPunches(
                    employee.UserId,
                    employee.EmployeeName,
                    request.FechaInicio,
                    request.FechaFin,
                    request.WorkScheduleCode).ToList();
            }

            var processed = _processor.Process(employeePunchesList);
            processed = EnsureDateCoverage(processed, request.FechaInicio, request.FechaFin, employee.UserId, employee.EmployeeName);

            var hasAnyRecord = processed.Any(d => d.Estatus != "SIN_REGISTRO");
            if (!hasAnyRecord)
            {
                employeesWithoutRecords++;
            }

            totalWorkedHours += processed.Where(d => d.HorasTrabajadas.HasValue).Sum(d => d.HorasTrabajadas!.Value);
            totalExtraHours += processed.Where(d => d.HorasExtra.HasValue).Sum(d => d.HorasExtra!.Value);

            var reportPath = await _reportGenerator.GenerateWeeklyReportAsync(
                processed,
                request.OutputDirectory,
                request.TemplatePath,
                new ReportTemplateContext
                {
                    EmployeeName = employee.EmployeeName,
                    Area = request.Area,
                    Position = request.Position,
                    WorkScheduleCode = request.WorkScheduleCode,
                    WorkScheduleDisplay = request.WorkScheduleDisplay,
                    WorkShiftType = request.WorkShiftType
                },
                cancellationToken,
                outputFormat);

            reports.Add(reportPath);
        }

        return new ReportRunSummary
        {
            EmployeesProcessed = employees.Count,
            EmployeesWithoutRecords = employeesWithoutRecords,
            TotalWorkedHours = totalWorkedHours,
            TotalExtraHours = totalExtraHours,
            ReportPaths = reports
        };
    }

    private static IReadOnlyList<AttendanceDayResult> EnsureDateCoverage(
        IReadOnlyList<AttendanceDayResult> processed,
        DateTime from,
        DateTime to,
        int userId,
        string employeeName)
    {
        var byDate = processed.ToDictionary(x => x.Date);
        if (string.IsNullOrWhiteSpace(employeeName))
        {
            employeeName = $"Empleado_{userId}";
        }

        var start = DateOnly.FromDateTime(from.Date);
        var end = DateOnly.FromDateTime(to.Date);

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (byDate.ContainsKey(date))
            {
                continue;
            }

            byDate[date] = new AttendanceDayResult
            {
                UserId = userId,
                EmployeeName = employeeName,
                Date = date,
                HorasTrabajadas = null,
                HorasExtra = null,
                Estatus = "SIN_REGISTRO",
                Observaciones = "Sin registro"
            };
        }

        return byDate.Values.OrderBy(x => x.Date).ToList();
    }

    private async Task<string> ResolveEmployeeNameAsync(int userId, string? requestedName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            return requestedName.Trim();
        }

        var employees = await _dataSource.GetEmployeesAsync(cancellationToken);
        return employees.FirstOrDefault(e => e.UserId == userId)?.EmployeeName ?? $"Empleado_{userId}";
    }

    private static IReadOnlyList<PunchRecord> BuildSimulatedPunches(
        int userId,
        string employeeName,
        DateTime from,
        DateTime to,
        int? workScheduleCode)
    {
        var punches = new List<PunchRecord>();
        var start = from.Date;
        var end = to.Date;
        var scheduleCode = workScheduleCode ?? 1;
        var scheduledStart = scheduleCode == 3 ? new TimeSpan(18, 0, 0) : new TimeSpan(7, 0, 0);
        var standardHours = scheduleCode == 3 ? 9.6m : 8m;

        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var offset = (day.Day + userId) % 4;
            var entry = day.Date.Add(scheduledStart).AddMinutes(offset == 0 ? 12 : -3 + offset);
            var exit = entry.AddHours((double)standardHours).AddMinutes(offset == 1 ? 45 : 10);

            punches.Add(new PunchRecord(userId, employeeName, entry, 0, scheduleCode, true, standardHours, scheduleCode == 3));
            punches.Add(new PunchRecord(userId, employeeName, exit, 1, scheduleCode, true, standardHours, scheduleCode == 3));
        }

        return punches;
    }

    private static string? ResolveShiftType(int? workScheduleCode)
    {
        return workScheduleCode switch
        {
            1 or 2 => "Diurna",
            3 => "Nocturna",
            _ => null
        };
    }
}
