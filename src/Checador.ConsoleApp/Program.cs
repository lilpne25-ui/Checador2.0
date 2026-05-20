using Checador.Application;
using Checador.ConsoleApp;
using Checador.Domain.Models;
using Checador.Infrastructure.Access;
using Checador.Infrastructure.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "CHECADOR_")
    .Build();

var appConfig = configuration.Get<AppConfiguration>() ?? new AppConfiguration();

var localJsonConfiguration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
    .Build();

var localSqlConnectionString = localJsonConfiguration["Sql:ConnectionString"];
if (!string.IsNullOrWhiteSpace(localSqlConnectionString))
{
    appConfig.Sql.ConnectionString = localSqlConnectionString;
}

var startupAccessOptions = new AccessConnectionOptions
{
    ConnectionString = appConfig.Access.ConnectionString,
    CommandTimeoutSeconds = appConfig.Access.CommandTimeoutSeconds
};

        var startupDeviceOptions = new DirectDeviceOptions
        {
            Enabled = appConfig.Device.Enabled,
            PreferDirect = appConfig.Device.PreferDirect,
            FallbackToAccess = appConfig.Device.FallbackToAccess,
            EnforceSyncAgentOnly = appConfig.Device.EnforceSyncAgentOnly,
            AllowedEntryAssemblies = appConfig.Device.AllowedEntryAssemblies.ToList(),
            Ip = appConfig.Device.Ip,
            Port = appConfig.Device.Port,
            CommKey = appConfig.Device.CommKey,
            MachineNumber = appConfig.Device.MachineNumber,
            Devices = appConfig.Device.Devices.Select(d => new DirectDeviceEndpointOptions
    {
        Label = d.Label,
        Ip = d.Ip,
        Port = d.Port,
        CommKey = d.CommKey,
        MachineNumber = d.MachineNumber
    }).ToList()
};

var startupSqlOptions = new SqlSyncOptions
{
    ConnectionString = appConfig.Sql.ConnectionString,
    InitialLookbackDays = appConfig.Sql.InitialLookbackDays,
    AutoSyncMinutes = appConfig.Sql.AutoSyncMinutes
};

var startupDataSourceOptions = new DataSourceOptions
{
    Primary = appConfig.DataSource.Primary
};

var startupPervasiveOptions = new PervasiveSyncOptions
{
    Enabled = appConfig.PervasiveSync.Enabled,
    ConnectionString = appConfig.PervasiveSync.ConnectionString,
    AttendanceTable = appConfig.PervasiveSync.AttendanceTable,
    InProcessTable = appConfig.PervasiveSync.InProcessTable,
    DeleteInProcessOnExit = appConfig.PervasiveSync.DeleteInProcessOnExit,
    ProgramName = appConfig.PervasiveSync.ProgramName,
    DefaultShift = appConfig.PervasiveSync.DefaultShift,
    DefaultDepartment = appConfig.PervasiveSync.DefaultDepartment,
    CommandTimeoutSeconds = appConfig.PervasiveSync.CommandTimeoutSeconds,
    StopSyncOnMirrorFailure = appConfig.PervasiveSync.StopSyncOnMirrorFailure,
    SimulationMode = appConfig.PervasiveSync.SimulationMode,
    OnlyCurrentDayWrites = appConfig.PervasiveSync.OnlyCurrentDayWrites,
    CloseOnlyWithFinalOutMode = appConfig.PervasiveSync.CloseOnlyWithFinalOutMode,
    MinimumOutGapMinutes = appConfig.PervasiveSync.MinimumOutGapMinutes,
    MaxRowsPerRun = appConfig.PervasiveSync.MaxRowsPerRun
};

var startupValidator = new StartupRequirementsValidator(startupAccessOptions, startupDeviceOptions, startupSqlOptions, startupDataSourceOptions, startupPervasiveOptions);
var startupErrors = startupValidator.Validate(appConfig.Report.OutputDirectory);
if (startupErrors.Count > 0)
{
    Console.WriteLine("No se puede arrancar el programa porque faltan requisitos:\n");
    foreach (var error in startupErrors)
    {
        Console.WriteLine($"- {error}");
    }

    Environment.ExitCode = 1;
    return;
}

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
}));

services.AddSingleton(new AccessConnectionOptions
{
    ConnectionString = startupAccessOptions.ConnectionString,
    CommandTimeoutSeconds = startupAccessOptions.CommandTimeoutSeconds
});

        services.AddSingleton(new DirectDeviceOptions
        {
            Enabled = startupDeviceOptions.Enabled,
            PreferDirect = startupDeviceOptions.PreferDirect,
            FallbackToAccess = startupDeviceOptions.FallbackToAccess,
            EnforceSyncAgentOnly = startupDeviceOptions.EnforceSyncAgentOnly,
            AllowedEntryAssemblies = startupDeviceOptions.AllowedEntryAssemblies.ToList(),
            Ip = startupDeviceOptions.Ip,
            Port = startupDeviceOptions.Port,
            CommKey = startupDeviceOptions.CommKey,
            MachineNumber = startupDeviceOptions.MachineNumber,
            Devices = startupDeviceOptions.Devices.Select(d => new DirectDeviceEndpointOptions
    {
        Label = d.Label,
        Ip = d.Ip,
        Port = d.Port,
        CommKey = d.CommKey,
        MachineNumber = d.MachineNumber
    }).ToList()
});

services.AddSingleton(new SqlSyncOptions
{
    ConnectionString = startupSqlOptions.ConnectionString,
    InitialLookbackDays = startupSqlOptions.InitialLookbackDays,
    AutoSyncMinutes = startupSqlOptions.AutoSyncMinutes
});

services.AddSingleton(new DataSourceOptions
{
    Primary = startupDataSourceOptions.Primary
});

services.AddSingleton(startupPervasiveOptions);

services.AddSingleton(new AttendanceProcessingOptions
{
    StandardWorkDayHours = appConfig.Processing.StandardWorkDayHours,
    RoundToMinutes = appConfig.Processing.RoundToMinutes,
    UseStrictFourPunchPolicy = appConfig.Processing.UseStrictFourPunchPolicy,
    EnableNightShiftSupport = appConfig.Processing.EnableNightShiftSupport,
    NightShiftScheduleCode = appConfig.Processing.NightShiftScheduleCode,
    NightShiftStartHour = appConfig.Processing.NightShiftStartHour,
    NightShiftRegularEndHour = appConfig.Processing.NightShiftRegularEndHour,
    NightShiftCarryToPreviousDayUntilHour = appConfig.Processing.NightShiftCarryToPreviousDayUntilHour,
    NightShiftStandardHours = appConfig.Processing.NightShiftStandardHours,
    Schedule1ExtraCountFromHour = appConfig.Processing.Schedule1ExtraCountFromHour,
    Schedule2ExtraCountFromHour = appConfig.Processing.Schedule2ExtraCountFromHour,
    Schedule3ExtraCountFromHour = appConfig.Processing.Schedule3ExtraCountFromHour,
    Schedule1ExtraMaxHours = appConfig.Processing.Schedule1ExtraMaxHours,
    Schedule2ExtraMaxHours = appConfig.Processing.Schedule2ExtraMaxHours,
    Schedule3ExtraMaxHours = appConfig.Processing.Schedule3ExtraMaxHours
});

services.AddSingleton(new ExcelReportOptions
{
    DefaultFileNamePattern = appConfig.Report.DefaultFileNamePattern,
    DataStartRow = appConfig.Report.DataStartRow,
    ForceWriteHeaders = appConfig.Report.ForceWriteHeaders
});

services.AddSingleton<AccessAttendanceDataSource>();
services.AddSingleton<ZkTecoDirectAttendanceDataSource>();
services.AddSingleton<SqlDeviceSyncService>();
services.AddSingleton<PervasiveAttendanceMirrorService>();
services.AddSingleton<SqlAttendanceDataSource>();
services.AddSingleton<Checador.Domain.Interfaces.IAttendanceDataSource, AttendanceDataSourceRouter>();
services.AddSingleton<Checador.Domain.Interfaces.IReportGenerator, ClosedXmlReportGenerator>();
services.AddSingleton<AttendanceProcessor>();
services.AddSingleton<AttendanceReportOrchestrator>();

var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Checador");
var argsSet = args.Select(a => a.Trim()).Where(a => !string.IsNullOrWhiteSpace(a)).ToHashSet(StringComparer.OrdinalIgnoreCase);

try
{
    if (argsSet.Contains("--reconcile-march-2026-once") || argsSet.Contains("reconcile-march-2026-once"))
    {
        await RunOneTimeMarchPervasiveReconciliationAsync(provider, logger);
        await RunReportedIdsAuditAsync(
            provider,
            logger,
            new[] { 23, 24, 81, 120, 163, 165, 170 },
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 31));
        await RunMonthQualityAuditAsync(
            provider,
            logger,
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 31));
        await RunMonthFullDetailAuditAsync(
            provider,
            logger,
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 31));
        return;
    }

    if (argsSet.Contains("--audit-march-2026-only") || argsSet.Contains("audit-march-2026-only"))
    {
        await RunReportedIdsAuditAsync(
            provider,
            logger,
            new[] { 23, 24, 81, 120, 163, 165, 170 },
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 31));
        await RunMonthQualityAuditAsync(
            provider,
            logger,
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 31));
        await RunMonthFullDetailAuditAsync(
            provider,
            logger,
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 31));
        return;
    }

    if (argsSet.Contains("--audit-march-2026-full-detail") || argsSet.Contains("audit-march-2026-full-detail"))
    {
        await RunMonthFullDetailAuditAsync(
            provider,
            logger,
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 31));
        return;
    }

    if (argsSet.Contains("--daemon-sync") || argsSet.Contains("sync-daemon"))
    {
        var syncService = provider.GetRequiredService<SqlDeviceSyncService>();
        var intervalMinutes = Math.Max(1, startupSqlOptions.AutoSyncMinutes);
        var delay = TimeSpan.FromMinutes(intervalMinutes);

        logger.LogInformation("Modo autosync servidor activado. Intervalo: {Minutes} min.", intervalMinutes);
        logger.LogInformation("Presiona Ctrl+C para detener el proceso.");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            if (!cts.IsCancellationRequested)
            {
                logger.LogInformation("Señal de cierre recibida. Deteniendo autosync...");
                cts.Cancel();
            }
        };

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await syncService.DownloadAttendanceLogsAsync(cts.Token);
                logger.LogInformation("Autosync OK. Leídas={Read} Insertadas={Inserted} Duplicadas={Dup}.", result.DevicePunches, result.InsertedPunches, result.DuplicatePunches);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Autosync falló en este ciclo.");
            }

            try
            {
                await Task.Delay(delay, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Autosync detenido.");
        return;
    }

    var userId = ReadInt("USERID de empleado", 1);
    var fechaInicio = ReadDate("Fecha inicio (yyyy-MM-dd)");
    var fechaFin = ReadDate("Fecha fin (yyyy-MM-dd)").AddDays(1).AddTicks(-1);

    logger.LogInformation("Iniciando generación de reporte para empleado {UserId} del {From} al {To}",
        userId,
        fechaInicio,
        fechaFin);

    var orchestrator = provider.GetRequiredService<AttendanceReportOrchestrator>();
    var resultPath = await orchestrator.GenerateAsync(new ReportRequest
    {
        UserId = userId,
        FechaInicio = fechaInicio,
        FechaFin = fechaFin,
        OutputDirectory = appConfig.Report.OutputDirectory,
        TemplatePath = appConfig.Report.TemplatePath
    });

    logger.LogInformation("Reporte generado: {Path}", resultPath);
}
catch (Exception ex)
{
    if (ex is InvalidOperationException && ex.Message.Contains("No hay registros procesados"))
    {
        logger.LogWarning("No se encontraron marcas para el empleado y rango indicados.");
    }
    else
    {
        logger.LogError(ex, "Error durante la ejecución.");
        Environment.ExitCode = 1;
    }
}

static int ReadInt(string label, int fallback)
{
    Console.Write($"{label} [{fallback}]: ");
    var input = Console.ReadLine();
    return int.TryParse(input, out var value) ? value : fallback;
}

static DateTime ReadDate(string label)
{
    while (true)
    {
        Console.Write($"{label}: ");
        var input = Console.ReadLine();
        if (DateTime.TryParse(input, out var value))
        {
            return value;
        }

        Console.WriteLine("Fecha inválida. Usa formato yyyy-MM-dd.");
    }
}

static async Task RunOneTimeMarchPervasiveReconciliationAsync(
    IServiceProvider provider,
    ILogger logger,
    CancellationToken cancellationToken = default)
{
    var mirror = provider.GetRequiredService<PervasiveAttendanceMirrorService>();
    if (!mirror.IsEnabled)
    {
        throw new InvalidOperationException("No se puede ejecutar reconciliación histórica: PervasiveSync está deshabilitado.");
    }

    var sqlDataSource = provider.GetRequiredService<SqlAttendanceDataSource>();
    var syncService = provider.GetRequiredService<SqlDeviceSyncService>();
    var mirrorOptions = provider.GetRequiredService<PervasiveSyncOptions>();

    var from = new DateTime(2026, 3, 1);
    var to = new DateTime(2026, 3, 31, 23, 59, 59);

    logger.LogWarning("RECALCULO EXCEPCIONAL: reconciliación masiva Pervasive para marzo 2026 (una sola vez).");

    var employees = await sqlDataSource.GetEmployeesAsync(cancellationToken);
    var punches = await sqlDataSource.GetPunchesAsync(null, from, to, cancellationToken);
    if (punches.Count == 0)
    {
        logger.LogWarning("No hay marcas SQL en marzo 2026; no hay nada que reconciliar.");
        return;
    }

    var dayGroups = punches
        .OrderBy(p => p.CheckTime)
        .GroupBy(p => (p.UserId, Date: p.CheckTime.Date))
        .OrderBy(g => g.Key.Date)
        .ThenBy(g => g.Key.UserId)
        .ToList();

    var catalog = await syncService.GetEmployeeCatalogAsync(cancellationToken);
    var employeeMeta = catalog
        .GroupBy(x => x.UserId)
        .ToDictionary(
            g => g.Key,
            g =>
            {
                var row = g.First();
                return (Department: row.PervasiveDept, Shift: row.PervasiveShift);
            });

    var configuredMaxRows = mirrorOptions.MaxRowsPerRun <= 0 ? 200 : mirrorOptions.MaxRowsPerRun;
    var groupsPerBatch = Math.Clamp(configuredMaxRows - 10, 20, 120);
    var totalBatches = (dayGroups.Count + groupsPerBatch - 1) / groupsPerBatch;

    logger.LogInformation(
        "Inicio reconciliación marzo: empleados={Employees}, marcas={Punches}, días={Days}, lote_días={BatchDays}, lotes={Batches}.",
        employees.Count,
        punches.Count,
        dayGroups.Count,
        groupsPerBatch,
        totalBatches);

    var totalInserted = 0;
    var totalUpdated = 0;
    var totalSkipped = 0;

    for (var offset = 0; offset < dayGroups.Count; offset += groupsPerBatch)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var batchNumber = (offset / groupsPerBatch) + 1;
        var batchGroups = dayGroups
            .Skip(offset)
            .Take(groupsPerBatch)
            .ToList();

        var batchPunches = batchGroups
            .SelectMany(g => g)
            .OrderBy(p => p.CheckTime)
            .ToList();

        var mirrorResult = await mirror.MirrorSnapshotAsync(
            employees,
            batchPunches,
            employeeMeta,
            cancellationToken);

        totalInserted += mirrorResult.Inserted;
        totalUpdated += mirrorResult.Updated;
        totalSkipped += mirrorResult.Skipped;

        logger.LogInformation(
            "Lote {Batch}/{Total}: días={Days}, marcas={Punches}, insertados={Inserted}, actualizados={Updated}, omitidos={Skipped}.",
            batchNumber,
            totalBatches,
            batchGroups.Count,
            batchPunches.Count,
            mirrorResult.Inserted,
            mirrorResult.Updated,
            mirrorResult.Skipped);
    }

    logger.LogInformation(
        "Reconciliación marzo completada: insertados={Inserted}, actualizados={Updated}, omitidos={Skipped}.",
        totalInserted,
        totalUpdated,
        totalSkipped);
}

static async Task RunReportedIdsAuditAsync(
    IServiceProvider provider,
    ILogger logger,
    IReadOnlyList<int> userIds,
    DateTime fromDate,
    DateTime toDate,
    CancellationToken cancellationToken = default)
{
    var dataSource = provider.GetRequiredService<Checador.Domain.Interfaces.IAttendanceDataSource>();
    var processor = provider.GetRequiredService<AttendanceProcessor>();

    var from = fromDate.Date;
    var to = toDate.Date.AddDays(1).AddTicks(-1);
    var fromDateOnly = DateOnly.FromDateTime(fromDate.Date);
    var toDateOnly = DateOnly.FromDateTime(toDate.Date);

    logger.LogInformation(
        "Auditoría posterior de IDs reportados: {Count} IDs en rango {From:yyyy-MM-dd}..{To:yyyy-MM-dd}.",
        userIds.Count,
        fromDate.Date,
        toDate.Date);

    foreach (var userId in userIds.OrderBy(x => x))
    {
        var punches = await dataSource.GetPunchesAsync(userId, from, to, cancellationToken);
        var processedDays = processor
            .Process(punches)
            .Where(d => d.Date >= fromDateOnly && d.Date <= toDateOnly)
            .OrderBy(d => d.Date)
            .ToList();

        var rawByDate = punches
            .GroupBy(p => DateOnly.FromDateTime(p.CheckTime))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CheckTime).ToList());

        var suspicious = new List<AttendanceDayResult>();
        foreach (var day in processedDays)
        {
            if (!day.Entrada.HasValue || !day.Salida.HasValue)
            {
                continue;
            }

            if (!rawByDate.TryGetValue(day.Date, out var rawDayPunches))
            {
                rawDayPunches = new List<PunchRecord>();
            }

            var isNight = rawDayPunches.Any(p => p.IsNightShift == true || p.WorkScheduleCode == 3);
            if (isNight)
            {
                continue;
            }

            var entryTime = day.Entrada.Value.TimeOfDay;
            var exitTime = day.Salida.Value.TimeOfDay;
            if (entryTime >= TimeSpan.FromHours(12)
                || exitTime <= TimeSpan.FromHours(12)
                || day.Salida.Value <= day.Entrada.Value)
            {
                suspicious.Add(day);
            }
        }

        logger.LogInformation(
            "USERID {UserId}: marcas={Punches}, días={Days}, incidencias={Incidences}, sospechosos={Suspicious}.",
            userId,
            punches.Count,
            processedDays.Count,
            processedDays.Count(d => d.Estatus is "INCIDENCIA" or "INCOMPLETO"),
            suspicious.Count);

        foreach (var day in suspicious.Take(6))
        {
            logger.LogWarning(
                "Sospechoso USERID {UserId} {Date}: Entrada={Entrada}, SalidaComida={SalidaComida}, EntradaComida={EntradaComida}, Salida={Salida}, Estado={Status}.",
                userId,
                day.Date.ToString("yyyy-MM-dd"),
                FormatTime(day.Entrada),
                FormatTime(day.SalidaComida),
                FormatTime(day.EntradaComida),
                FormatTime(day.Salida),
                day.Estatus);
        }
    }
}

static string FormatTime(DateTime? value)
    => value.HasValue ? value.Value.ToString("HH:mm") : "-";

static async Task RunMonthQualityAuditAsync(
    IServiceProvider provider,
    ILogger logger,
    DateTime fromDate,
    DateTime toDate,
    CancellationToken cancellationToken = default)
{
    var dataSource = provider.GetRequiredService<Checador.Domain.Interfaces.IAttendanceDataSource>();
    var processor = provider.GetRequiredService<AttendanceProcessor>();

    var from = fromDate.Date;
    var to = toDate.Date.AddDays(1).AddTicks(-1);
    var fromDateOnly = DateOnly.FromDateTime(fromDate.Date);
    var toDateOnly = DateOnly.FromDateTime(toDate.Date);

    var allPunches = await dataSource.GetPunchesAsync(null, from, to, cancellationToken);
    var byUser = allPunches
        .GroupBy(p => p.UserId)
        .OrderBy(g => g.Key)
        .ToList();

    var totalDays = 0;
    var totalIncidences = 0;
    var totalSuspiciousDays = 0;
    var usersWithSuspicious = 0;
    var top = new List<(int UserId, string Name, int Days, int Incidences, int Suspicious)>();

    foreach (var userGroup in byUser)
    {
        var punches = userGroup.OrderBy(p => p.CheckTime).ToList();
        var processedDays = processor
            .Process(punches)
            .Where(d => d.Date >= fromDateOnly && d.Date <= toDateOnly)
            .OrderBy(d => d.Date)
            .ToList();

        var rawByDate = punches
            .GroupBy(p => DateOnly.FromDateTime(p.CheckTime))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CheckTime).ToList());

        var incidenceDays = processedDays.Count(d => d.Estatus is "INCIDENCIA" or "INCOMPLETO");
        var suspiciousDays = 0;

        foreach (var day in processedDays)
        {
            if (!day.Entrada.HasValue || !day.Salida.HasValue)
            {
                continue;
            }

            if (!rawByDate.TryGetValue(day.Date, out var rawDayPunches))
            {
                rawDayPunches = new List<PunchRecord>();
            }

            var isNight = rawDayPunches.Any(p => p.IsNightShift == true || p.WorkScheduleCode == 3);
            if (isNight)
            {
                continue;
            }

            var entryTime = day.Entrada.Value.TimeOfDay;
            var exitTime = day.Salida.Value.TimeOfDay;
            if (entryTime >= TimeSpan.FromHours(12)
                || exitTime <= TimeSpan.FromHours(12)
                || day.Salida.Value <= day.Entrada.Value)
            {
                suspiciousDays++;
            }
        }

        totalDays += processedDays.Count;
        totalIncidences += incidenceDays;
        totalSuspiciousDays += suspiciousDays;

        if (suspiciousDays > 0)
        {
            usersWithSuspicious++;
        }

        var employeeName = punches
            .OrderByDescending(p => p.CheckTime)
            .Select(p => p.EmployeeName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? $"Empleado_{userGroup.Key}";

        top.Add((userGroup.Key, employeeName, processedDays.Count, incidenceDays, suspiciousDays));
    }

    logger.LogInformation(
        "Auditoría mensual completa: empleados={Employees}, días={Days}, incidencias={Incidences}, días_sospechosos={SuspiciousDays}, empleados_con_sospechosos={UsersSuspicious}.",
        byUser.Count,
        totalDays,
        totalIncidences,
        totalSuspiciousDays,
        usersWithSuspicious);

    foreach (var row in top
                 .Where(x => x.Suspicious > 0)
                 .OrderByDescending(x => x.Suspicious)
                 .ThenByDescending(x => x.Incidences)
                 .ThenBy(x => x.UserId)
                 .Take(20))
    {
        logger.LogWarning(
            "Top sospechoso USERID {UserId} {Name}: días={Days}, incidencias={Incidences}, sospechosos={Suspicious}.",
            row.UserId,
            row.Name,
            row.Days,
            row.Incidences,
            row.Suspicious);
    }
}

static async Task RunMonthFullDetailAuditAsync(
    IServiceProvider provider,
    ILogger logger,
    DateTime fromDate,
    DateTime toDate,
    CancellationToken cancellationToken = default)
{
    var dataSource = provider.GetRequiredService<Checador.Domain.Interfaces.IAttendanceDataSource>();
    var processor = provider.GetRequiredService<AttendanceProcessor>();

    var from = fromDate.Date;
    var to = toDate.Date.AddDays(1).AddTicks(-1);
    var fromDateOnly = DateOnly.FromDateTime(fromDate.Date);
    var toDateOnly = DateOnly.FromDateTime(toDate.Date);

    var employees = await dataSource.GetEmployeesAsync(cancellationToken);
    var allPunches = await dataSource.GetPunchesAsync(null, from, to, cancellationToken);
    var byUserPunches = allPunches
        .GroupBy(p => p.UserId)
        .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CheckTime).ToList());

    var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
    Directory.CreateDirectory(outputDir);
    var reportPath = Path.Combine(outputDir, $"auditoria-marzo-2026-detalle-{DateTime.Now:yyyyMMdd-HHmmss}.csv");

    var totalRows = 0;
    var suspiciousRows = 0;
    var usersWithSuspicious = 0;
    var userSuspiciousCounter = new List<(int UserId, string Name, int SuspiciousRows)>();

    await using var writer = new StreamWriter(reportPath, false);
    await writer.WriteLineAsync("UserId,Empleado,Fecha,Entrada,SalidaComida,EntradaComida,Salida,Estatus,Incidencia,Nocturno,RawCount,RawPunches,Checks,Observaciones");

    foreach (var employee in employees.OrderBy(e => e.UserId))
    {
        cancellationToken.ThrowIfCancellationRequested();

        byUserPunches.TryGetValue(employee.UserId, out var userPunches);
        userPunches ??= new List<PunchRecord>();

        var processedByDate = processor
            .Process(userPunches)
            .Where(d => d.Date >= fromDateOnly && d.Date <= toDateOnly)
            .ToDictionary(d => d.Date, d => d);

        var rawByDate = userPunches
            .GroupBy(p => DateOnly.FromDateTime(p.CheckTime))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CheckTime).ToList());

        var userSuspiciousRows = 0;
        for (var cursor = fromDateOnly; cursor <= toDateOnly; cursor = cursor.AddDays(1))
        {
            processedByDate.TryGetValue(cursor, out var day);
            rawByDate.TryGetValue(cursor, out var rawDayPunches);
            rawDayPunches ??= new List<PunchRecord>();

            var isNight = rawDayPunches.Any(p => p.IsNightShift == true || p.WorkScheduleCode == 3);
            var checks = EvaluateDayChecks(day, rawDayPunches, isNight);
            var hasIssue = checks.Count > 0;

            if (hasIssue)
            {
                suspiciousRows++;
                userSuspiciousRows++;
            }

            var rawPunchesText = rawDayPunches.Count == 0
                ? "-"
                : string.Join(" | ", rawDayPunches.Select(FormatRawPunch));

            var status = day?.Estatus ?? "SIN_REGISTRO";
            var incidencia = status is "INCIDENCIA" or "INCOMPLETO" ? "Y" : "N";
            var checksText = checks.Count == 0 ? "OK" : string.Join(";", checks);
            var observations = day?.Observaciones ?? string.Empty;

            await writer.WriteLineAsync(string.Join(",",
                Csv(employee.UserId.ToString()),
                Csv(employee.EmployeeName),
                Csv(cursor.ToString("yyyy-MM-dd")),
                Csv(FormatTime(day?.Entrada)),
                Csv(FormatTime(day?.SalidaComida)),
                Csv(FormatTime(day?.EntradaComida)),
                Csv(FormatTime(day?.Salida)),
                Csv(status),
                Csv(incidencia),
                Csv(isNight ? "Y" : "N"),
                Csv(rawDayPunches.Count.ToString()),
                Csv(rawPunchesText),
                Csv(checksText),
                Csv(observations)));

            totalRows++;
        }

        if (userSuspiciousRows > 0)
        {
            usersWithSuspicious++;
            userSuspiciousCounter.Add((employee.UserId, employee.EmployeeName, userSuspiciousRows));
        }
    }

    logger.LogInformation(
        "Auditoría detallada completa finalizada: filas={Rows}, filas_sospechosas={SuspiciousRows}, empleados_con_sospecha={UsersWithSuspicious}, archivo={ReportPath}.",
        totalRows,
        suspiciousRows,
        usersWithSuspicious,
        reportPath);

    foreach (var row in userSuspiciousCounter
                 .OrderByDescending(x => x.SuspiciousRows)
                 .ThenBy(x => x.UserId)
                 .Take(25))
    {
        logger.LogWarning(
            "Detalle: USERID {UserId} {Name} con {SuspiciousRows} filas sospechosas en marzo.",
            row.UserId,
            row.Name,
            row.SuspiciousRows);
    }
}

static List<string> EvaluateDayChecks(
    AttendanceDayResult? day,
    IReadOnlyList<PunchRecord> rawDayPunches,
    bool isNight)
{
    var checks = new List<string>();
    if (day is null)
    {
        return checks;
    }

    if (day.Entrada.HasValue && day.Salida.HasValue)
    {
        if (day.Salida.Value <= day.Entrada.Value)
        {
            checks.Add("EXIT_BEFORE_ENTRY");
        }

        if (!isNight)
        {
            if (day.Entrada.Value.TimeOfDay >= TimeSpan.FromHours(12))
            {
                checks.Add("ENTRY_AFTER_NOON");
            }

            if (day.Salida.Value.TimeOfDay <= TimeSpan.FromHours(12))
            {
                checks.Add("EXIT_BEFORE_NOON");
            }
        }
    }
    else
    {
        if (day.Entrada.HasValue && !day.Salida.HasValue)
        {
            checks.Add("MISSING_EXIT");
        }

        if (!day.Entrada.HasValue && day.Salida.HasValue)
        {
            checks.Add("MISSING_ENTRY");
        }
    }

    if (day.Entrada.HasValue && day.SalidaComida.HasValue)
    {
        var breakAfterEntry = (day.SalidaComida.Value - day.Entrada.Value).TotalMinutes;
        if (breakAfterEntry < 90)
        {
            checks.Add("BREAK_OUT_NEAR_ENTRY");
        }
    }

    if (day.SalidaComida.HasValue && day.Salida.HasValue)
    {
        var breakBeforeExit = (day.Salida.Value - day.SalidaComida.Value).TotalMinutes;
        if (breakBeforeExit < 60)
        {
            checks.Add("BREAK_OUT_NEAR_EXIT");
        }
    }

    if (day.SalidaComida.HasValue && day.EntradaComida.HasValue && day.EntradaComida.Value <= day.SalidaComida.Value)
    {
        checks.Add("BREAK_IN_BEFORE_BREAK_OUT");
    }

    if (!isNight && rawDayPunches.Count >= 2)
    {
        var firstMode = rawDayPunches[0].InOutMode;
        var lastMode = rawDayPunches[^1].InOutMode;
        if ((firstMode is 1 or 2)
            && (lastMode is 0 or 3)
            && !IsClearlyValidInvertedPairForAudit(day, rawDayPunches))
        {
            checks.Add("RAW_MODE_INVERTED");
        }
    }

    return checks;
}

static bool IsClearlyValidInvertedPairForAudit(
    AttendanceDayResult day,
    IReadOnlyList<PunchRecord> rawDayPunches)
{
    if (!day.Entrada.HasValue || !day.Salida.HasValue)
    {
        return false;
    }

    if (rawDayPunches.Count != 2)
    {
        return false;
    }

    var first = rawDayPunches[0];
    var last = rawDayPunches[^1];
    if (!(first.InOutMode is 1 or 2) || !(last.InOutMode is 0 or 3))
    {
        return false;
    }

    if (day.Entrada.Value != first.CheckTime || day.Salida.Value != last.CheckTime)
    {
        return false;
    }

    var spanMinutes = (day.Salida.Value - day.Entrada.Value).TotalMinutes;
    if (spanMinutes < 240 || spanMinutes > 1080)
    {
        return false;
    }

    return day.Entrada.Value.TimeOfDay < TimeSpan.FromHours(12)
        && day.Salida.Value.TimeOfDay > TimeSpan.FromHours(12);
}

static string FormatRawPunch(PunchRecord punch)
{
    var mode = punch.InOutMode.HasValue ? punch.InOutMode.Value.ToString() : "N";
    return $"{punch.CheckTime:HH:mm:ss}(m:{mode})";
}

static string Csv(string? value)
{
    if (string.IsNullOrEmpty(value))
    {
        return "\"\"";
    }

    var safe = value.Replace("\"", "\"\"");
    return $"\"{safe}\"";
}
