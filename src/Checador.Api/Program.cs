using Checador.Api;
using Checador.Api.Admin;
using Checador.Api.Auth;
using Checador.Api.FeatureFlags;
using Checador.Api.Ops;
using Checador.Api.Reports;
using Checador.Application;
using Checador.Domain.Interfaces;
using Checador.Domain.Models;
using Checador.Infrastructure.Access;
using Checador.Infrastructure.Excel;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Globalization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "CHECADOR_");

var sqlOptions = builder.Configuration.GetSection("Sql").Get<SqlSyncOptions>() ?? new SqlSyncOptions();
var processingOptions = builder.Configuration.GetSection("Processing").Get<AttendanceProcessingOptions>() ?? new AttendanceProcessingOptions();
var deviceOptions = builder.Configuration.GetSection("Device").Get<DirectDeviceOptions>() ?? new DirectDeviceOptions();
var pervasiveOptions = builder.Configuration.GetSection("PervasiveSync").Get<PervasiveSyncOptions>() ?? new PervasiveSyncOptions();
var reportOptions = builder.Configuration.GetSection("Report").Get<ApiReportOptions>() ?? new ApiReportOptions();
var moduleFlags = builder.Configuration.GetSection("FeatureFlags:Modules").Get<ApiModuleFeatureFlags>() ?? new ApiModuleFeatureFlags();
var allowedCorsOrigins = NormalizeAllowedOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>());
const string ApiCorsPolicyName = "Checador.Api.Origins";

// Hardening Prompt 4: API is SQL read-only and must never read ZKTeco directly.
deviceOptions.Enabled = false;
deviceOptions.PreferDirect = false;
deviceOptions.FallbackToAccess = false;
deviceOptions.EnforceSyncAgentOnly = true;
if (!deviceOptions.GetAllowedEntryAssemblies().Any(x => string.Equals(x, "Checador.SyncAgent", StringComparison.OrdinalIgnoreCase)))
{
    deviceOptions.AllowedEntryAssemblies = new List<string> { "Checador.SyncAgent" };
}

// Keep deterministic local-machine override if present, but allow explicit env override
// for isolated demo runs (for example CHECADOR_Sql__ConnectionString in launcher scripts).
var explicitSqlConnectionFromEnv = Environment.GetEnvironmentVariable("CHECADOR_Sql__ConnectionString");
if (string.IsNullOrWhiteSpace(explicitSqlConnectionFromEnv))
{
    var localOnlyConfiguration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
        .Build();
    var localSqlConnectionString = localOnlyConfiguration["Sql:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(localSqlConnectionString))
    {
        sqlOptions.ConnectionString = localSqlConnectionString;
    }
}

builder.Services.AddSingleton(sqlOptions);
builder.Services.AddSingleton(processingOptions);
builder.Services.AddSingleton(deviceOptions);
builder.Services.AddSingleton(pervasiveOptions);
builder.Services.AddSingleton(reportOptions);
builder.Services.AddSingleton(moduleFlags);
builder.Services.AddSingleton(new ExcelReportOptions
{
    DefaultFileNamePattern = reportOptions.DefaultFileNamePattern,
    DataStartRow = reportOptions.DataStartRow,
    ForceWriteHeaders = reportOptions.ForceWriteHeaders
});

builder.Services.AddSingleton<ZkTecoDirectAttendanceDataSource>();
builder.Services.AddSingleton<SqlDeviceSyncService>();
builder.Services.AddSingleton<SqlAttendanceDataSource>();
builder.Services.AddSingleton<IAttendanceDataSource>(sp => sp.GetRequiredService<SqlAttendanceDataSource>());
builder.Services.AddSingleton<AttendanceProcessor>();
builder.Services.AddSingleton<IReportGenerator, ClosedXmlReportGenerator>();
builder.Services.AddSingleton<OvertimeWorkOrderFormatGenerator>();
builder.Services.AddSingleton<LatenessReportTemplateGenerator>();
builder.Services.AddSingleton<AttendanceReportOrchestrator>();
builder.Services.AddSingleton<OpsAutoSyncController>();
builder.Services.AddSingleton<ApiEmployeeAdminQueryService>();
builder.Services.AddScoped<IApiAuthService, SqlApiAuthService>();

if (allowedCorsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(ApiCorsPolicyName, policy =>
        {
            policy
                .SetIsOriginAllowed(origin => IsOriginAllowed(origin, allowedCorsOrigins))
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

builder.Services
    .AddAuthentication(ApiAuthConstants.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
        ApiAuthConstants.AuthenticationScheme,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    static bool HasPendingPassword(AuthorizationHandlerContext context)
        => string.Equals(
            context.User.FindFirst(ApiAuthConstants.MustChangePasswordClaim)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

    void AddPermissionPolicy(string policyName, string permission)
    {
        options.AddPolicy(policyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(ApiAuthConstants.PermissionClaim, permission);
            policy.RequireAssertion(context => !HasPendingPassword(context));
        });
    }

    options.AddPolicy(ApiAuthConstants.PolicyConsultaOrAdmin, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(ApiAuthConstants.RoleConsulta, ApiAuthConstants.RoleAdmin);
        policy.RequireAssertion(context => !HasPendingPassword(context));
    });

    options.AddPolicy(ApiAuthConstants.PolicyAdminOnly, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(ApiAuthConstants.RoleAdmin);
        policy.RequireAssertion(context => !HasPendingPassword(context));
    });

    AddPermissionPolicy(ApiAuthConstants.PolicyPreviewRead, ApiAuthConstants.PermissionPreviewRead);
    AddPermissionPolicy(ApiAuthConstants.PolicyReportsGenerate, ApiAuthConstants.PermissionReportsGenerate);
    AddPermissionPolicy(ApiAuthConstants.PolicyKpiRead, ApiAuthConstants.PermissionKpiRead);
    AddPermissionPolicy(ApiAuthConstants.PolicyAdminUsersRead, ApiAuthConstants.PermissionAdminUsersRead);
    AddPermissionPolicy(ApiAuthConstants.PolicyOpsSyncRun, ApiAuthConstants.PermissionOpsSyncRun);
    AddPermissionPolicy(ApiAuthConstants.PolicyOpsSyncStatusRead, ApiAuthConstants.PermissionOpsSyncStatusRead);
    AddPermissionPolicy(ApiAuthConstants.PolicyOpsLiveRead, ApiAuthConstants.PermissionOpsLiveRead);
    AddPermissionPolicy(ApiAuthConstants.PolicyOpsRecentHistoryRead, ApiAuthConstants.PermissionOpsRecentHistoryRead);
    AddPermissionPolicy(ApiAuthConstants.PolicyOpsDeviceTimeSync, ApiAuthConstants.PermissionOpsDeviceTimeSync);
    AddPermissionPolicy(ApiAuthConstants.PolicyOpsAutoSyncManage, ApiAuthConstants.PermissionOpsAutoSyncManage);
    AddPermissionPolicy(ApiAuthConstants.PolicyAdminEmployeesRead, ApiAuthConstants.PermissionAdminEmployeesRead);
    AddPermissionPolicy(ApiAuthConstants.PolicyAdminEmployeesEdit, ApiAuthConstants.PermissionAdminEmployeesEdit);
    AddPermissionPolicy(ApiAuthConstants.PolicyAdminEmployeesDelete, ApiAuthConstants.PermissionAdminEmployeesDelete);
    AddPermissionPolicy(ApiAuthConstants.PolicyAdminEmployeesReassign, ApiAuthConstants.PermissionAdminEmployeesReassign);
    AddPermissionPolicy(ApiAuthConstants.PolicyAdminManualPunchCreate, ApiAuthConstants.PermissionAdminManualPunchCreate);
    AddPermissionPolicy(ApiAuthConstants.PolicyAdminPervasivePreview, ApiAuthConstants.PermissionAdminPervasivePreview);
    AddPermissionPolicy(ApiAuthConstants.PolicyAdminPervasiveCleanup, ApiAuthConstants.PermissionAdminPervasiveCleanup);
    AddPermissionPolicy(ApiAuthConstants.PolicyAdminSchedulesManage, ApiAuthConstants.PermissionAdminSchedulesManage);
});

var app = builder.Build();

var autoSyncController = app.Services.GetRequiredService<OpsAutoSyncController>();
app.Lifetime.ApplicationStopping.Register(() =>
{
    autoSyncController.Stop();
});
var featureFlagLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FeatureFlags");

app.Use(async (context, next) =>
{
    if (TryGetDisabledModule(context.Request.Path, moduleFlags, out var moduleName))
    {
        featureFlagLogger.LogWarning(
            "feature_flag.blocked module={Module} path={Path}",
            moduleName,
            context.Request.Path.Value ?? "/");

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            message = $"MÃ³dulo '{moduleName}' deshabilitado temporalmente por feature flag.",
            module = moduleName,
            timestamp = DateTimeOffset.Now
        }, cancellationToken: context.RequestAborted);
        return;
    }

    await next();
});

if (allowedCorsOrigins.Length > 0)
{
    app.UseCors(ApiCorsPolicyName);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth/login", async (AuthLoginRequest request, IApiAuthService authService, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Username y password son obligatorios." });
    }

    var user = await authService.ValidateCredentialsAsync(request.Username, request.Password, cancellationToken);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        user.UserId,
        user.Username,
        user.MustChangePassword,
        user.Roles
    });
}).AllowAnonymous();

app.MapPost("/auth/change-password", async (
    AuthChangePasswordRequest request,
    ClaimsPrincipal principal,
    IApiAuthService authService,
    CancellationToken cancellationToken) =>
{
    if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
    {
        return Results.BadRequest(new { message = "CurrentPassword y NewPassword son obligatorios." });
    }

    if (request.NewPassword.Length < 8)
    {
        return Results.BadRequest(new { message = "NewPassword debe tener al menos 8 caracteres." });
    }

    var changed = await authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
    if (!changed)
    {
        return Results.BadRequest(new { message = "No se pudo cambiar password. Valida password actual." });
    }

    return Results.Ok(new { message = "Password actualizada correctamente." });
}).RequireAuthorization();

app.MapGet("/health/status", async (SqlDeviceSyncService syncService, CancellationToken cancellationToken) =>
{
    var stats = await syncService.GetStatsAsync(cancellationToken);
    return Results.Ok(new
    {
        service = "Checador.Api",
        status = "ok",
        source = "sql",
        stats.TotalEmployees,
        stats.TotalPunches,
        stats.LastPunch,
        stats.LastSync,
        stats.LastStatus,
        stats.LastMessage,
        timestamp = DateTimeOffset.Now
    });
});

app.MapGet("/ops/sync/status", async (SqlDeviceSyncService syncService, CancellationToken cancellationToken) =>
{
    var stats = await syncService.GetStatsAsync(cancellationToken);
    return Results.Ok(new
    {
        stats.TotalEmployees,
        stats.TotalPunches,
        stats.LastPunch,
        stats.LastSync,
        stats.LastStatus,
        stats.LastMessage,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyOpsSyncStatusRead);

app.MapGet("/ops/sync/autosync/status", (OpsAutoSyncController controller) =>
{
    var status = controller.GetStatus();
    return Results.Ok(new
    {
        status.Enabled,
        status.Busy,
        status.IntervalMinutes,
        status.LastRunStartedAt,
        status.LastRunFinishedAt,
        status.LastSuccessAt,
        status.LastFailureAt,
        status.SuccessCount,
        status.FailureCount,
        status.LastMessage,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyOpsAutoSyncManage);

app.MapPost("/ops/sync/autosync/start", (
    OpsAutoSyncController controller,
    DirectDeviceOptions deviceOptions,
    ClaimsPrincipal principal,
    ILogger<Program> logger) =>
{
    if (deviceOptions.EnforceSyncAgentOnly && !deviceOptions.IsCurrentProcessAuthorized())
    {
        const string blockedMessage = "AutoSync desde API estÃ¡ bloqueado por hardening. Usa Checador.SyncAgent para sincronizaciÃ³n con reloj.";
        logger.LogInformation(
            "AUDIT action={Action} user={User} userId={UserId} blockedReason={Reason}",
            "ops.sync.autosync.start",
            principal.Identity?.Name ?? "unknown",
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
            "syncagent-only");

        return Results.BadRequest(new
        {
            message = blockedMessage,
            timestamp = DateTimeOffset.Now
        });
    }

    var status = controller.Start();
    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={UserId} enabled={Enabled} interval={Interval}",
        "ops.sync.autosync.start",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        status.Enabled,
        status.IntervalMinutes);

    return Results.Ok(new
    {
        status.Enabled,
        status.Busy,
        status.IntervalMinutes,
        status.LastRunStartedAt,
        status.LastRunFinishedAt,
        status.LastSuccessAt,
        status.LastFailureAt,
        status.SuccessCount,
        status.FailureCount,
        status.LastMessage,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyOpsAutoSyncManage);

app.MapPost("/ops/sync/autosync/stop", (
    OpsAutoSyncController controller,
    ClaimsPrincipal principal,
    ILogger<Program> logger) =>
{
    var status = controller.Stop();
    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={UserId} enabled={Enabled}",
        "ops.sync.autosync.stop",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        status.Enabled);

    return Results.Ok(new
    {
        status.Enabled,
        status.Busy,
        status.IntervalMinutes,
        status.LastRunStartedAt,
        status.LastRunFinishedAt,
        status.LastSuccessAt,
        status.LastFailureAt,
        status.SuccessCount,
        status.FailureCount,
        status.LastMessage,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyOpsAutoSyncManage);

app.MapPut("/ops/sync/autosync/interval", (
    OpsAutoSyncIntervalRequest request,
    OpsAutoSyncController controller,
    ClaimsPrincipal principal,
    ILogger<Program> logger) =>
{
    if (request.IntervalMinutes <= 0)
    {
        return Results.BadRequest(new { message = "IntervalMinutes debe ser mayor a 0.", timestamp = DateTimeOffset.Now });
    }

    var status = controller.SetIntervalMinutes(request.IntervalMinutes);

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={UserId} interval={Interval}",
        "ops.sync.autosync.interval",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        status.IntervalMinutes);

    return Results.Ok(new
    {
        status.Enabled,
        status.Busy,
        status.IntervalMinutes,
        status.LastRunStartedAt,
        status.LastRunFinishedAt,
        status.LastSuccessAt,
        status.LastFailureAt,
        status.SuccessCount,
        status.FailureCount,
        status.LastMessage,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyOpsAutoSyncManage);

app.MapPost("/ops/sync/run", async (
    SqlDeviceSyncService syncService,
    DirectDeviceOptions deviceOptions,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (deviceOptions.EnforceSyncAgentOnly && !deviceOptions.IsCurrentProcessAuthorized())
    {
        const string blockedMessage = "Sync desde API estÃ¡ bloqueado por hardening. Ejecuta la sincronizaciÃ³n desde Checador.SyncAgent.";
        logger.LogInformation(
            "AUDIT action={Action} user={User} userId={UserId} blockedReason={Reason}",
            "ops.sync.run",
            principal.Identity?.Name ?? "unknown",
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
            "syncagent-only");

        return Results.BadRequest(new
        {
            message = blockedMessage,
            timestamp = DateTimeOffset.Now
        });
    }

    SqlSyncRunResult result;
    try
    {
        result = await syncService.DownloadAttendanceLogsAsync(cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(
            ex,
            "ops.sync.run blocked user={User} userId={UserId}",
            principal.Identity?.Name ?? "unknown",
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a");

        return Results.BadRequest(new
        {
            message = ex.Message,
            timestamp = DateTimeOffset.Now
        });
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "ops.sync.run failed user={User} userId={UserId}",
            principal.Identity?.Name ?? "unknown",
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a");

        return Results.Problem(
            title: "Error ejecutando sync.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={UserId} inserted={Inserted} duplicated={Duplicated}",
        "ops.sync.run",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        result.InsertedPunches,
        result.DuplicatePunches);

    return Results.Ok(new
    {
        result.DeviceEmployees,
        result.DevicePunches,
        result.InsertedPunches,
        result.DuplicatePunches,
        result.LastSyncTime,
        result.Message,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyOpsSyncRun);

app.MapPost("/ops/device/time/sync", async (
    SqlDeviceSyncService syncService,
    DirectDeviceOptions deviceOptions,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (deviceOptions.EnforceSyncAgentOnly && !deviceOptions.IsCurrentProcessAuthorized())
    {
        const string blockedMessage = "Sincronizar hora desde API estÃ¡ bloqueado por hardening. Usa Checador.SyncAgent.";
        logger.LogInformation(
            "AUDIT action={Action} user={User} userId={UserId} blockedReason={Reason}",
            "ops.device.time.sync",
            principal.Identity?.Name ?? "unknown",
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
            "syncagent-only");

        return Results.BadRequest(new
        {
            Success = false,
            Message = blockedMessage,
            timestamp = DateTimeOffset.Now
        });
    }

    (bool Success, string Message) result;
    try
    {
        result = await syncService.SyncDeviceTimeAsync(cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(
            ex,
            "ops.device.time.sync blocked user={User} userId={UserId}",
            principal.Identity?.Name ?? "unknown",
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a");

        return Results.BadRequest(new
        {
            Success = false,
            Message = ex.Message,
            timestamp = DateTimeOffset.Now
        });
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "ops.device.time.sync failed user={User} userId={UserId}",
            principal.Identity?.Name ?? "unknown",
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a");

        return Results.Problem(
            title: "Error sincronizando hora del reloj.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={UserId} success={Success}",
        "ops.device.time.sync",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        result.Success);

    return result.Success
        ? Results.Ok(new { result.Success, result.Message, timestamp = DateTimeOffset.Now })
        : Results.BadRequest(new { result.Success, result.Message, timestamp = DateTimeOffset.Now });
}).RequireAuthorization(ApiAuthConstants.PolicyOpsDeviceTimeSync);

app.MapGet("/ops/live-status", async (SqlDeviceSyncService syncService, CancellationToken cancellationToken) =>
{
    var live = await syncService.GetLiveStatusAsync(cancellationToken);
    return Results.Ok(new
    {
        live.DeviceConnected,
        live.DeviceTime,
        live.DeviceUsers,
        live.SqlEmployees,
        live.SqlPunches,
        live.LastPunch,
        live.LastSync,
        live.LastSyncStatus,
        live.DeviceMessage,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyOpsLiveRead);

app.MapGet("/ops/telemetry", async (
    SqlDeviceSyncService syncService,
    OpsAutoSyncController autoSyncController,
    DirectDeviceOptions deviceOptions,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var alerts = new List<OpsTelemetryAlert>();
    var now = DateTimeOffset.Now;

    var stats = await syncService.GetStatsAsync(cancellationToken);
    if (!string.Equals(stats.LastStatus, "OK", StringComparison.OrdinalIgnoreCase))
    {
        alerts.Add(new OpsTelemetryAlert(
            "SYNC_LAST_STATUS_NOT_OK",
            "warning",
            $"Ãšltimo sync reportado como '{stats.LastStatus}'.",
            stats.LastSync.HasValue ? new DateTimeOffset(stats.LastSync.Value) : null));
    }

    if (!stats.LastSync.HasValue)
    {
        alerts.Add(new OpsTelemetryAlert(
            "SYNC_NEVER_RUN",
            "critical",
            "No existe registro de sincronizaciÃ³n previa."));
    }
    else
    {
        var minutesSinceLastSync = (DateTime.Now - stats.LastSync.Value).TotalMinutes;
        if (minutesSinceLastSync >= 360)
        {
            alerts.Add(new OpsTelemetryAlert(
                "SYNC_STALE_CRITICAL",
                "critical",
                $"Ãšltimo sync hace {minutesSinceLastSync:F1} minutos (>= 360).",
                new DateTimeOffset(stats.LastSync.Value)));
        }
        else if (minutesSinceLastSync >= 180)
        {
            alerts.Add(new OpsTelemetryAlert(
                "SYNC_STALE_WARNING",
                "warning",
                $"Ãšltimo sync hace {minutesSinceLastSync:F1} minutos (>= 180).",
                new DateTimeOffset(stats.LastSync.Value)));
        }
    }

    SqlLiveStatus? liveStatus = null;
    string? liveFetchError = null;
    try
    {
        liveStatus = await syncService.GetLiveStatusAsync(cancellationToken);
    }
    catch (Exception ex)
    {
        liveFetchError = ex.Message;
        alerts.Add(new OpsTelemetryAlert(
            "LIVE_STATUS_ERROR",
            "critical",
            $"No fue posible leer live-status: {ex.Message}"));
    }

    if (liveStatus is not null)
    {
        if (!liveStatus.DeviceConnected)
        {
            alerts.Add(new OpsTelemetryAlert(
                "DEVICE_DISCONNECTED",
                "critical",
                "Reloj no disponible para operaciones en vivo."));
        }

        if (!liveStatus.DeviceTime.HasValue)
        {
            alerts.Add(new OpsTelemetryAlert(
                "DEVICE_TIME_UNAVAILABLE",
                "warning",
                "No se pudo leer la hora del reloj por SDK/driver."));
        }
    }

    var autoSync = autoSyncController.GetStatus();
    if (autoSync.Enabled && autoSync.FailureCount > 0)
    {
        var lastFailureIsNewest = autoSync.LastFailureAt.HasValue
            && (!autoSync.LastSuccessAt.HasValue || autoSync.LastFailureAt.Value >= autoSync.LastSuccessAt.Value);

        alerts.Add(new OpsTelemetryAlert(
            "AUTOSYNC_FAILURES",
            lastFailureIsNewest ? "critical" : "warning",
            $"AutoSync registra {autoSync.FailureCount} fallo(s) acumulado(s).",
            autoSync.LastFailureAt));
    }

    if (autoSync.Enabled && autoSync.Busy && autoSync.LastRunStartedAt.HasValue)
    {
        var runningMinutes = (DateTimeOffset.Now - autoSync.LastRunStartedAt.Value).TotalMinutes;
        var busyThreshold = Math.Max(20, autoSync.IntervalMinutes * 2);
        if (runningMinutes >= busyThreshold)
        {
            alerts.Add(new OpsTelemetryAlert(
                "AUTOSYNC_STUCK",
                "critical",
                $"AutoSync lleva {runningMinutes:F1} minutos en ejecuciÃ³n (umbral={busyThreshold}).",
                autoSync.LastRunStartedAt));
        }
    }

    var overallStatus = ResolveTelemetryStatus(alerts);

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={UserId} status={Status} alerts={AlertCount}",
        "ops.telemetry.read",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        overallStatus,
        alerts.Count);

    return Results.Ok(new
    {
        status = overallStatus,
        alertCount = alerts.Count,
        alerts,
        sql = new
        {
            stats.TotalEmployees,
            stats.TotalPunches,
            stats.LastPunch,
            stats.LastSync,
            stats.LastStatus,
            stats.LastMessage
        },
        live = liveStatus is null
            ? (object)new
            {
                available = false,
                message = string.IsNullOrWhiteSpace(liveFetchError) ? "Live status no disponible." : liveFetchError
            }
            : new
            {
                available = true,
                liveStatus.DeviceConnected,
                liveStatus.DeviceTime,
                liveStatus.DeviceUsers,
                liveStatus.SqlEmployees,
                liveStatus.SqlPunches,
                liveStatus.LastPunch,
                liveStatus.LastSync,
                liveStatus.LastSyncStatus,
                liveStatus.DeviceMessage
            },
        autoSync = new
        {
            autoSync.Enabled,
            autoSync.Busy,
            autoSync.IntervalMinutes,
            autoSync.LastRunStartedAt,
            autoSync.LastRunFinishedAt,
            autoSync.LastSuccessAt,
            autoSync.LastFailureAt,
            autoSync.SuccessCount,
            autoSync.FailureCount,
            autoSync.LastMessage
        },
        hardening = new
        {
            deviceOptions.EnforceSyncAgentOnly,
            DeviceProcessAuthorized = deviceOptions.IsCurrentProcessAuthorized()
        },
        timestamp = now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyOpsLiveRead);

app.MapGet("/ops/recent-history", async (
    int? top,
    SqlDeviceSyncService syncService,
    CancellationToken cancellationToken) =>
{
    var requested = top ?? 5;
    var maxRows = requested <= 0 ? 5 : Math.Min(requested, 200);
    var rows = await syncService.GetRecentHistoryAsync(maxRows, cancellationToken);
    return Results.Ok(rows);
}).RequireAuthorization(ApiAuthConstants.PolicyOpsRecentHistoryRead);

app.MapGet("/preview", async (
    int userId,
    DateTime from,
    DateTime to,
    AttendanceReportOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    if (userId <= 0)
    {
        return Results.BadRequest("userId debe ser mayor que 0.");
    }

    if (from > to)
    {
        return Results.BadRequest("Rango invalido: 'from' no puede ser mayor que 'to'.");
    }

    var request = new ReportRequest
    {
        UserId = userId,
        FechaInicio = from,
        FechaFin = to,
        OutputDirectory = "N/A",
        TemplatePath = null
    };

    var preview = await orchestrator.BuildPreviewAsync(request, cancellationToken);
    return Results.Ok(preview);
}).RequireAuthorization(ApiAuthConstants.PolicyPreviewRead);

app.MapGet("/preview/all", async (
    DateTime from,
    DateTime to,
    AttendanceReportOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    if (from > to)
    {
        return Results.BadRequest("Rango invalido: 'from' no puede ser mayor que 'to'.");
    }

    var request = new ReportRequest
    {
        UserId = 0,
        FechaInicio = from,
        FechaFin = to,
        OutputDirectory = "N/A",
        TemplatePath = null
    };

    var preview = await orchestrator.BuildPreviewForAllAsync(request, cancellationToken);
    return Results.Ok(preview);
}).RequireAuthorization(ApiAuthConstants.PolicyPreviewRead);

app.MapGet("/kpi", async (
    DateTime from,
    DateTime to,
    AttendanceReportOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    if (from > to)
    {
        return Results.BadRequest("Rango invalido: 'from' no puede ser mayor que 'to'.");
    }

    var request = new ReportRequest
    {
        UserId = 0,
        FechaInicio = from,
        FechaFin = to,
        OutputDirectory = "N/A",
        TemplatePath = null
    };

    var summary = await orchestrator.BuildPreviewForAllWithSummaryAsync(request, cancellationToken);
    return Results.Ok(summary);
}).RequireAuthorization(ApiAuthConstants.PolicyKpiRead);

app.MapGet("/admin/users", async (IApiAuthService authService, CancellationToken cancellationToken) =>
{
    var users = await authService.GetUsersAsync(cancellationToken);
    return Results.Ok(users);
}).RequireAuthorization(ApiAuthConstants.PolicyAdminUsersRead);

app.MapGet("/admin/employees", async (
    ApiEmployeeAdminQueryService employeesQuery,
    CancellationToken cancellationToken) =>
{
    var employees = await employeesQuery.GetEmployeesAsync(cancellationToken);
    return Results.Ok(employees);
}).RequireAuthorization(ApiAuthConstants.PolicyAdminEmployeesRead);

app.MapGet("/admin/employees/{userId:int}", async (
    int userId,
    ApiEmployeeAdminQueryService employeesQuery,
    CancellationToken cancellationToken) =>
{
    if (userId <= 0)
    {
        return Results.BadRequest(new { message = "userId debe ser mayor que 0." });
    }

    var employee = await employeesQuery.GetEmployeeAsync(userId, cancellationToken);
    return employee is null
        ? Results.NotFound(new { message = "Empleado no encontrado." })
        : Results.Ok(employee);
}).RequireAuthorization(ApiAuthConstants.PolicyAdminEmployeesRead);

app.MapGet("/admin/schedules", async (
    SqlDeviceSyncService syncService,
    CancellationToken cancellationToken) =>
{
    var schedules = await syncService.GetWorkSchedulesAsync(cancellationToken);
    return Results.Ok(schedules);
}).RequireAuthorization(ApiAuthConstants.PolicyAdminSchedulesManage);

app.MapPut("/admin/schedules/{code:int}", async (
    int code,
    AdminWorkScheduleUpsertRequest request,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (code <= 0)
    {
        return Results.BadRequest(new { message = "code debe ser mayor que 0." });
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { message = "Name es obligatorio." });
    }

    if (!TryParseTimeOfDay(request.StartTime, out var startTime))
    {
        return Results.BadRequest(new { message = "StartTime invalido. Usa formato HH:mm." });
    }

    if (!TryParseTimeOfDay(request.EndTime, out var endTime))
    {
        return Results.BadRequest(new { message = "EndTime invalido. Usa formato HH:mm." });
    }

    if (request.StandardHours <= 0)
    {
        return Results.BadRequest(new { message = "StandardHours debe ser mayor que 0." });
    }

    try
    {
        await syncService.UpsertWorkScheduleAsync(
            code,
            request.Name,
            startTime,
            endTime,
            request.StandardHours,
            request.IsActive,
            request.LateToleranceMinutes,
            cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} scheduleCode={ScheduleCode} start={Start} end={End} standardHours={StandardHours} tolerance={Tolerance} active={IsActive}",
        "admin.schedules.upsert",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        code,
        startTime,
        endTime,
        request.StandardHours,
        request.LateToleranceMinutes,
        request.IsActive);

    return Results.Ok(new
    {
        code,
        name = request.Name.Trim(),
        startTime = startTime.ToString(@"hh\:mm"),
        endTime = endTime.ToString(@"hh\:mm"),
        request.StandardHours,
        request.IsActive,
        request.LateToleranceMinutes,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminSchedulesManage);

app.MapPut("/admin/employees/{userId:int}", async (
    int userId,
    AdminEmployeeUpsertRequest request,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (userId <= 0)
    {
        return Results.BadRequest(new { message = "userId debe ser mayor que 0." });
    }

    if (string.IsNullOrWhiteSpace(request.EmployeeName))
    {
        return Results.BadRequest(new { message = "EmployeeName es obligatorio." });
    }

    var result = await syncService.RenameEmployeeAsync(
        userId,
        request.EmployeeName,
        request.Department,
        request.Position,
        request.PervasiveDept,
        request.PervasiveShift,
        request.WorkScheduleCode,
        request.OvertimeAuthorized,
        cancellationToken);

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} targetUserId={TargetUserId} sqlUpdated={SqlUpdated} deviceUpdated={DeviceUpdated}",
        "admin.employees.update",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        userId,
        result.UpdatedInSql,
        result.UpdatedOnDevice);

    return Results.Ok(new
    {
        result.UserId,
        result.NewName,
        DeviceUpdated = result.UpdatedOnDevice,
        result.DeviceMessage,
        SqlUpdated = result.UpdatedInSql,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminEmployeesEdit);

app.MapDelete("/admin/employees/{userId:int}", async (
    int userId,
    bool? deleteHistoryRows,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (userId <= 0)
    {
        return Results.BadRequest(new { message = "userId debe ser mayor que 0." });
    }

    var result = await syncService.DeleteEmployeeAsync(userId, deleteHistoryRows ?? false, cancellationToken);

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} targetUserId={TargetUserId} sqlDeleted={SqlDeleted} deviceDeleted={DeviceDeleted} deletedHistory={DeletedHistoryRows}",
        "admin.employees.delete",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        userId,
        result.DeletedInSql,
        result.DeletedOnDevice,
        result.DeletedHistoryRows);

    return Results.Ok(new
    {
        result.UserId,
        DeviceDeleted = result.DeletedOnDevice,
        result.DeviceMessage,
        SqlDeleted = result.DeletedInSql,
        result.DeletedHistoryRows,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminEmployeesDelete);

app.MapPost("/admin/employees/reassign/preview", async (
    AdminEmployeeReassignPreviewRequest request,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.OldUserId <= 0 || request.NewUserId <= 0)
    {
        return Results.BadRequest(new { message = "OldUserId y NewUserId deben ser mayores que 0." });
    }

    if (request.From.Date > request.To.Date)
    {
        return Results.BadRequest(new { message = "Rango invÃ¡lido: from no puede ser mayor que to." });
    }

    var preview = await syncService.PreviewEmployeeReassignByDateRangeAsync(
        request.OldUserId,
        request.NewUserId,
        request.From.Date,
        request.To.Date,
        request.IncludePervasive,
        cancellationToken);

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} oldUserId={OldUserId} newUserId={NewUserId} from={From} to={To}",
        "admin.employees.reassign.preview",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        request.OldUserId,
        request.NewUserId,
        request.From,
        request.To);

    return Results.Ok(new
    {
        preview.OldUserId,
        preview.NewUserId,
        preview.FromDate,
        preview.ToDate,
        preview.SqlOldRows,
        preview.SqlTargetRows,
        preview.SqlConflictRows,
        preview.SqlDistinctDays,
        preview.SqlAffectedDates,
        preview.PervasiveEnabled,
        preview.PervasiveOldRows,
        preview.PervasiveTargetRows,
        preview.PervasiveConflictRows,
        preview.PervasiveDistinctDays,
        preview.PervasiveAffectedDates,
        preview.Message,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminEmployeesReassign);

app.MapPost("/admin/employees/reassign/apply", async (
    AdminEmployeeReassignApplyRequest request,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.OldUserId <= 0 || request.NewUserId <= 0)
    {
        return Results.BadRequest(new { message = "OldUserId y NewUserId deben ser mayores que 0." });
    }

    if (request.From.Date > request.To.Date)
    {
        return Results.BadRequest(new { message = "Rango invÃ¡lido: from no puede ser mayor que to." });
    }

    if (!request.ConfirmApply || !string.Equals(request.ConfirmText?.Trim(), "APLICAR CAMBIO", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { message = "ConfirmaciÃ³n requerida: marca ConfirmApply=true y escribe 'APLICAR CAMBIO'." });
    }

    EmployeeReassignApplyResult result;
    try
    {
        result = await syncService.ReassignEmployeeByDateRangeAsync(
            request.OldUserId,
            request.NewUserId,
            request.From.Date,
            request.To.Date,
            request.NewName,
            request.NewDepartment,
            request.NewPervasiveDept,
            request.NewPervasiveShift,
            request.AllowSkipConflicts,
            request.IncludePervasive,
            cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message, timestamp = DateTimeOffset.Now });
    }

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} oldUserId={OldUserId} newUserId={NewUserId} sqlUpdated={SqlUpdated} pervasiveUpdated={PervasiveUpdated}",
        "admin.employees.reassign.apply",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        request.OldUserId,
        request.NewUserId,
        result.SqlUpdatedRows,
        result.PervasiveUpdatedRows);

    return Results.Ok(new
    {
        result.OldUserId,
        result.NewUserId,
        result.FromDate,
        result.ToDate,
        result.SqlUpdatedRows,
        result.SqlSkippedConflicts,
        result.SqlEmployeeUpserted,
        result.PervasiveEnabled,
        result.PervasiveUpdatedRows,
        result.PervasiveSkippedConflicts,
        result.Message,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminEmployeesReassign);

app.MapGet("/admin/pervasive/preview", async (
    int? top,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var safeTop = top is > 0 ? Math.Min(top.Value, 5000) : 500;
    var allRows = await syncService.GetPervasivePayloadPreviewAsync(cancellationToken);
    var rows = allRows.Take(safeTop).ToList();

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} top={Top} totalRows={TotalRows} returnedRows={ReturnedRows}",
        "admin.pervasive.preview",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        safeTop,
        allRows.Count,
        rows.Count);

    return Results.Ok(new
    {
        totalRows = allRows.Count,
        returnedRows = rows.Count,
        top = safeTop,
        items = rows,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminPervasivePreview);

app.MapPost("/admin/pervasive/night-cleanup/preview", async (
    AdminPervasiveNightCleanupRequest request,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.UserId.HasValue && request.UserId.Value <= 0)
    {
        return Results.BadRequest(new { message = "UserId debe ser mayor que 0 cuando se especifica." });
    }

    if (request.From.Date > request.To.Date)
    {
        return Results.BadRequest(new { message = "Rango invÃ¡lido: from no puede ser mayor que to." });
    }

    PervasiveNightShiftCleanupPreviewResult result;
    try
    {
        result = await syncService.PreviewPervasiveNightShiftHistoricalCleanupAsync(
            request.From.Date,
            request.To.Date,
            request.UserId,
            cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message, timestamp = DateTimeOffset.Now });
    }

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} from={From} to={To} targetUserId={TargetUserId} candidate={Candidate} processable={Processable} affectedUsers={AffectedUsers} estimatedSplitRows={EstimatedSplitRows}",
        "admin.pervasive.cleanup.preview",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        request.From,
        request.To,
        request.UserId,
        result.CandidatePunches,
        result.ProcessablePunches,
        result.AffectedUsers,
        result.EstimatedSplitRows);

    return Results.Ok(new
    {
        result.Enabled,
        result.FromDate,
        result.ToDate,
        result.UserId,
        result.CandidatePunches,
        result.ProcessablePunches,
        result.TruncatedByTop,
        result.AffectedUsers,
        result.EstimatedSplitRows,
        result.Message,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminPervasiveCleanup);

app.MapPost("/admin/pervasive/night-cleanup/run", async (
    AdminPervasiveNightCleanupRunRequest request,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.UserId.HasValue && request.UserId.Value <= 0)
    {
        return Results.BadRequest(new { message = "UserId debe ser mayor que 0 cuando se especifica." });
    }

    if (request.From.Date > request.To.Date)
    {
        return Results.BadRequest(new { message = "Rango invÃ¡lido: from no puede ser mayor que to." });
    }

    if (!request.ConfirmApply || !string.Equals(request.ConfirmText?.Trim(), "EJECUTAR LIMPIEZA", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { message = "ConfirmaciÃ³n requerida: marca ConfirmApply=true y escribe 'EJECUTAR LIMPIEZA'." });
    }

    PervasiveNightShiftCleanupResult result;
    try
    {
        result = await syncService.RunPervasiveNightShiftHistoricalCleanupAsync(
            request.From.Date,
            request.To.Date,
            request.UserId,
            cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message, timestamp = DateTimeOffset.Now });
    }

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} from={From} to={To} targetUserId={TargetUserId} candidate={Candidate} processed={Processed} affectedUsers={AffectedUsers} deletedSplitRows={DeletedSplitRows}",
        "admin.pervasive.cleanup.run",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        request.From,
        request.To,
        request.UserId,
        result.CandidatePunches,
        result.ProcessedPunches,
        result.AffectedUsers,
        result.DeletedSplitRows);

    return Results.Ok(new
    {
        result.Enabled,
        result.FromDate,
        result.ToDate,
        result.UserId,
        result.CandidatePunches,
        result.ProcessedPunches,
        result.TruncatedByTop,
        result.AffectedUsers,
        result.DeletedSplitRows,
        result.MirrorMessage,
        result.Message,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminPervasiveCleanup);

app.MapPost("/admin/manual-punch", async (
    AdminManualPunchRequest request,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.UserId <= 0)
    {
        return Results.BadRequest(new { message = "UserId debe ser mayor que 0." });
    }

    if (string.IsNullOrWhiteSpace(request.EmployeeName))
    {
        return Results.BadRequest(new { message = "EmployeeName es obligatorio." });
    }

    if (request.BatchEntries is { Count: > 0 })
    {
        return Results.BadRequest(new { message = "Para lote usa /admin/manual-punch/batch." });
    }

    if (!request.CheckTime.HasValue)
    {
        return Results.BadRequest(new { message = "CheckTime es obligatorio para alta manual puntual." });
    }

    var result = await syncService.InsertManualPunchAsync(
        request.UserId,
        request.EmployeeName,
        request.CheckTime.Value,
        request.InOutMode,
        cancellationToken);

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} targetUserId={TargetUserId} inserted={Inserted} duplicated={Duplicated}",
        "admin.manual_punch.create",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        request.UserId,
        result.InsertedPunches,
        result.DuplicatePunches);

    return Results.Ok(new
    {
        result.DeviceEmployees,
        result.DevicePunches,
        result.InsertedPunches,
        result.DuplicatePunches,
        result.LastSyncTime,
        result.Message,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminManualPunchCreate);

app.MapPost("/admin/manual-punch/batch", async (
    AdminManualPunchBatchRequest request,
    SqlDeviceSyncService syncService,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.UserId <= 0)
    {
        return Results.BadRequest(new { message = "UserId debe ser mayor que 0." });
    }

    if (string.IsNullOrWhiteSpace(request.EmployeeName))
    {
        return Results.BadRequest(new { message = "EmployeeName es obligatorio." });
    }

    if (request.BatchEntries is null || request.BatchEntries.Count == 0)
    {
        return Results.BadRequest(new { message = "BatchEntries es obligatorio y debe tener al menos una marca." });
    }

    if (request.BatchEntries.Count > 300)
    {
        return Results.BadRequest(new { message = "BatchEntries supera el lÃ­mite de 300 marcas por solicitud." });
    }

    var batch = request.BatchEntries
        .Select(x => new ManualPunchEntry(x.CheckTime, x.InOutMode))
        .ToList();

    var result = await syncService.InsertManualPunchesAsync(
        request.UserId,
        request.EmployeeName,
        batch,
        cancellationToken);

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} targetUserId={TargetUserId} batchCount={BatchCount} inserted={Inserted} duplicated={Duplicated}",
        "admin.manual_punch.batch.create",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        request.UserId,
        request.BatchEntries.Count,
        result.InsertedPunches,
        result.DuplicatePunches);

    return Results.Ok(new
    {
        result.DeviceEmployees,
        result.DevicePunches,
        result.InsertedPunches,
        result.DuplicatePunches,
        result.LastSyncTime,
        result.Message,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyAdminManualPunchCreate);

app.MapPost("/reports/generate", async (
    ReportGenerateRequest request,
    AttendanceReportOrchestrator orchestrator,
    ApiReportOptions reportOptions,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    IWebHostEnvironment env,
    CancellationToken cancellationToken) =>
{
    if (request.From > request.To)
    {
        return Results.BadRequest(new { message = "Rango invÃ¡lido: from no puede ser mayor que to." });
    }

    var outputFormat = string.Equals(request.OutputFormat, "pdf", StringComparison.OrdinalIgnoreCase)
        ? ReportOutputFormat.Pdf
        : ReportOutputFormat.Excel;

    var outputDirectory = string.IsNullOrWhiteSpace(request.OutputDirectory)
        ? reportOptions.OutputDirectory
        : request.OutputDirectory.Trim();

    if (!Path.IsPathRooted(outputDirectory))
    {
        outputDirectory = Path.Combine(env.ContentRootPath, outputDirectory);
    }

    Directory.CreateDirectory(outputDirectory);

    var templatePath = ResolveTemplatePath(request.TemplatePath, reportOptions.TemplatePath, env.ContentRootPath);

    var reportRequest = new ReportRequest
    {
        UserId = request.UserId ?? 0,
        FechaInicio = request.From,
        FechaFin = request.To,
        OutputDirectory = outputDirectory,
        TemplatePath = templatePath,
        Company = request.Company,
        UseSimulatedDataWhenEmpty = request.UseSimulatedDataWhenEmpty
    };

    ReportRunSummary summary;
    try
    {
        if ((request.UserId ?? 0) <= 0)
        {
            summary = await orchestrator.GenerateForAllWithSummaryAsync(reportRequest, cancellationToken, outputFormat);
        }
        else
        {
            summary = await orchestrator.GenerateWithSummaryAsync(reportRequest, cancellationToken, outputFormat);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "No se pudo generar reporte general.");
        return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} targetUserId={TargetUserId} format={Format} processed={Processed} noRecords={NoRecords}",
        "reports.generate",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        request.UserId ?? 0,
        outputFormat,
        summary.EmployeesProcessed,
        summary.EmployeesWithoutRecords);

    return Results.Ok(new
    {
        summary.EmployeesProcessed,
        summary.EmployeesWithoutRecords,
        summary.TotalWorkedHours,
        summary.TotalExtraHours,
        summary.ReportPaths,
        outputFormat = outputFormat.ToString(),
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyReportsGenerate);

app.MapPost("/reports/overtime-format/generate", async (
    OvertimeFormatGenerateRequest request,
    ApiEmployeeAdminQueryService employeesQuery,
    AttendanceReportOrchestrator orchestrator,
    OvertimeWorkOrderFormatGenerator overtimeGenerator,
    ApiReportOptions reportOptions,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    IWebHostEnvironment env,
    CancellationToken cancellationToken) =>
{
    if (request.UserId <= 0)
    {
        return Results.BadRequest(new { message = "UserId debe ser mayor que 0." });
    }

    if (request.From.Date > request.To.Date)
    {
        return Results.BadRequest(new { message = "Rango invÃ¡lido: from no puede ser mayor que to." });
    }

    if (string.IsNullOrWhiteSpace(request.WorkOrderNumber))
    {
        return Results.BadRequest(new { message = "WorkOrderNumber es obligatorio." });
    }

    if (string.IsNullOrWhiteSpace(request.WorkDescription))
    {
        return Results.BadRequest(new { message = "WorkDescription es obligatorio." });
    }

    if (string.IsNullOrWhiteSpace(request.ImmediateBossName))
    {
        return Results.BadRequest(new { message = "ImmediateBossName es obligatorio." });
    }

    var employeeSnapshot = await employeesQuery.GetEmployeeAsync(request.UserId, cancellationToken);

    var employeeName = request.EmployeeName?.Trim();
    if (string.IsNullOrWhiteSpace(employeeName))
    {
        employeeName = string.IsNullOrWhiteSpace(employeeSnapshot?.EmployeeName)
            ? $"Empleado_{request.UserId}"
            : employeeSnapshot!.EmployeeName.Trim();
    }

    var outputDirectory = string.IsNullOrWhiteSpace(request.OutputDirectory)
        ? Path.Combine(reportOptions.OutputDirectory, "overtime-format")
        : request.OutputDirectory.Trim();

    if (!Path.IsPathRooted(outputDirectory))
    {
        outputDirectory = Path.Combine(env.ContentRootPath, outputDirectory);
    }

    Directory.CreateDirectory(outputDirectory);

    var requestedFileName = string.IsNullOrWhiteSpace(request.FileName)
        ? $"OT_{request.UserId}_{request.From:yyyyMMdd}_{request.To:yyyyMMdd}.xlsx"
        : request.FileName.Trim();

    var safeFileName = SanitizeFileName(requestedFileName);
    if (!safeFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
    {
        safeFileName = $"{Path.GetFileNameWithoutExtension(safeFileName)}.xlsx";
    }

    var fullPath = Path.Combine(outputDirectory, safeFileName);
    var workingDays = string.IsNullOrWhiteSpace(request.WorkingDays)
        ? BuildWorkingDaysText(request.From.Date, request.To.Date)
        : request.WorkingDays.Trim();

    var reportPreview = await orchestrator.BuildPreviewAsync(
        new ReportRequest
        {
            UserId = request.UserId,
            FechaInicio = request.From.Date,
            FechaFin = request.To.Date,
            OutputDirectory = "N/A",
            TemplatePath = null,
            EmployeeName = employeeName,
            Area = employeeSnapshot?.Department,
            Position = employeeSnapshot?.Position,
            WorkScheduleCode = employeeSnapshot?.WorkScheduleCode,
            UseSimulatedDataWhenEmpty = request.UseSimulatedDataWhenEmpty
        },
        cancellationToken);

    var data = new OvertimeWorkOrderFormatData(
        request.UserId,
        employeeName,
        request.WorkOrderNumber.Trim(),
        request.WorkDescription.Trim(),
        request.RequestDate == default ? DateTime.Today : request.RequestDate.Date,
        request.From.Date,
        request.To.Date,
        workingDays,
        string.IsNullOrWhiteSpace(request.WorkSchedule) ? "No especificado" : request.WorkSchedule.Trim(),
        request.ImmediateBossName.Trim(),
        string.IsNullOrWhiteSpace(request.ImmediateBossSignatureName)
            ? request.ImmediateBossName.Trim()
            : request.ImmediateBossSignatureName.Trim(),
        string.IsNullOrWhiteSpace(request.EmployeeSignatureName)
            ? employeeName
            : request.EmployeeSignatureName.Trim(),
        string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        employeeSnapshot?.Department,
        employeeSnapshot?.Position,
        string.IsNullOrWhiteSpace(reportPreview.WorkShiftType) ? "Diurna" : reportPreview.WorkShiftType,
        ResolveStandardShiftHoursForTemplate(employeeSnapshot?.WorkScheduleCode));

    var overtimeTemplatePath = ResolveTemplatePath(
        null,
        reportOptions.OvertimeTemplatePath,
        env.ContentRootPath);

    try
    {
        overtimeGenerator.Generate(fullPath, data, reportPreview.Days, overtimeTemplatePath);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "No se pudo generar formato de horas extras.");
        return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} targetUserId={TargetUserId} outputPath={OutputPath}",
        "reports.overtime_format.generate",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        request.UserId,
        fullPath);

    return Results.Ok(new
    {
        request.UserId,
        employeeName,
        outputPath = fullPath,
        fileName = safeFileName,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyReportsGenerate);

app.MapPost("/reports/lateness/generate", async (
    LatenessReportGenerateRequest request,
    SqlDeviceSyncService syncService,
    AttendanceReportOrchestrator orchestrator,
    ApiEmployeeAdminQueryService employeesQuery,
    LatenessReportTemplateGenerator latenessTemplateGenerator,
    ApiReportOptions reportOptions,
    ClaimsPrincipal principal,
    ILogger<Program> logger,
    IWebHostEnvironment env,
    CancellationToken cancellationToken) =>
{
    if (request.From.Date > request.To.Date)
    {
        return Results.BadRequest(new { message = "Rango invalido: from no puede ser mayor que to." });
    }

    var outputDirectory = string.IsNullOrWhiteSpace(request.OutputDirectory)
        ? Path.Combine(reportOptions.OutputDirectory, "retardos")
        : request.OutputDirectory.Trim();

    if (!Path.IsPathRooted(outputDirectory))
    {
        outputDirectory = Path.Combine(env.ContentRootPath, outputDirectory);
    }

    Directory.CreateDirectory(outputDirectory);

    var incidents = await syncService.GetLatenessIncidentsAsync(
        request.From.Date,
        request.To.Date,
        request.UserId is > 0 ? request.UserId : null,
        cancellationToken);

    if (request.UseSimulatedDataWhenEmpty && incidents.Count == 0)
    {
        incidents = await BuildSimulatedLatenessIncidentsAsync(
            request.UserId,
            request.From.Date,
            request.To.Date,
            employeesQuery,
            cancellationToken);
    }

    var requestedFileName = string.IsNullOrWhiteSpace(request.FileName)
        ? $"retardos_{request.From:yyyyMMdd}_{request.To:yyyyMMdd}{(request.UserId is > 0 ? $"_u{request.UserId}" : string.Empty)}.xlsx"
        : request.FileName.Trim();

    var safeFileName = SanitizeFileName(requestedFileName);
    if (!safeFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
    {
        safeFileName = $"{Path.GetFileNameWithoutExtension(safeFileName)}.xlsx";
    }

    var fullPath = Path.Combine(outputDirectory, safeFileName);
    var latenessTemplatePath = ResolveTemplatePath(
        null,
        reportOptions.LatenessTemplatePath,
        env.ContentRootPath);

    if (!string.IsNullOrWhiteSpace(latenessTemplatePath))
    {
        var templateReports = new List<LatenessTemplateEmployeeReport>();

        if (request.UserId is > 0)
        {
            var report = await BuildLatenessTemplateEmployeeReportAsync(
                request.UserId.Value,
                request.From.Date,
                request.To.Date,
                incidents,
                orchestrator,
                employeesQuery,
                cancellationToken);
            templateReports.Add(report);
        }
        else
        {
            var userIds = incidents
                .Select(x => x.UserId)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            foreach (var userId in userIds)
            {
                var report = await BuildLatenessTemplateEmployeeReportAsync(
                    userId,
                    request.From.Date,
                    request.To.Date,
                    incidents.Where(x => x.UserId == userId).ToList(),
                    orchestrator,
                    employeesQuery,
                    cancellationToken);
                templateReports.Add(report);
            }

            if (templateReports.Count == 0)
            {
                templateReports.Add(new LatenessTemplateEmployeeReport(
                    null,
                    "Sin incidencias",
                    null,
                    null,
                    "Diurna",
                    null,
                    null,
                    null,
                    Array.Empty<LatenessTemplateDayRow>(),
                    "Sin incidencias en el periodo seleccionado."));
            }
        }

        try
        {
            latenessTemplateGenerator.Generate(
                fullPath,
                request.From.Date,
                request.To.Date,
                templateReports,
                latenessTemplatePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo generar reporte de retardos con plantilla.");
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    else
    {
        try
        {
            WriteLatenessReportWorkbook(fullPath, request.From.Date, request.To.Date, incidents);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo generar reporte de retardos.");
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    var affectedEmployees = incidents
        .Select(x => x.UserId)
        .Distinct()
        .Count();

    logger.LogInformation(
        "AUDIT action={Action} user={User} userId={AuthUserId} from={From} to={To} targetUserId={TargetUserId} incidents={Incidents} affectedEmployees={AffectedEmployees} outputPath={OutputPath}",
        "reports.lateness.generate",
        principal.Identity?.Name ?? "unknown",
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "n/a",
        request.From.Date,
        request.To.Date,
        request.UserId,
        incidents.Count,
        affectedEmployees,
        fullPath);

    return Results.Ok(new
    {
        request.UserId,
        fromDate = request.From.Date,
        toDate = request.To.Date,
        incidentsCount = incidents.Count,
        affectedEmployees,
        outputPath = fullPath,
        fileName = safeFileName,
        timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization(ApiAuthConstants.PolicyReportsGenerate);

app.MapPost("/reports/files/download", async (
    ReportFilesDownloadRequest request,
    ApiReportOptions reportOptions,
    IWebHostEnvironment env,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.Paths is null || request.Paths.Count == 0)
    {
        return Results.BadRequest(new { message = "No hay archivos generados para descargar." });
    }

    var outputRoot = ResolveReportOutputRoot(reportOptions.OutputDirectory, env.ContentRootPath);
    var files = new List<string>();
    foreach (var path in request.Paths.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
    {
        var fullPath = Path.GetFullPath(path);
        if (!IsUnderDirectory(fullPath, outputRoot))
        {
            return Results.BadRequest(new { message = "Ruta de reporte fuera del directorio permitido." });
        }

        if (!File.Exists(fullPath))
        {
            return Results.NotFound(new { message = $"No se encontro el archivo generado: {Path.GetFileName(fullPath)}" });
        }

        files.Add(fullPath);
    }

    if (files.Count == 0)
    {
        return Results.BadRequest(new { message = "No hay archivos validos para descargar." });
    }

    var exportFormat = NormalizeDownloadExportFormat(request.ExportFormat);
    var preparedFiles = new List<PreparedDownloadFile>();
    try
    {
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            preparedFiles.Add(PrepareDownloadFile(file, exportFormat));
        }

        if (preparedFiles.Count == 1)
        {
            var prepared = preparedFiles[0];
            var bytes = await File.ReadAllBytesAsync(prepared.Path, cancellationToken);
            return Results.File(bytes, prepared.ContentType, prepared.FileName);
        }

        await using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prepared in preparedFiles)
            {
                var entryName = CreateUniqueZipEntryName(prepared.FileName, usedNames);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await using var fileStream = File.OpenRead(prepared.Path);
                await fileStream.CopyToAsync(entryStream, cancellationToken);
            }
        }

        return Results.File(
            zipStream.ToArray(),
            "application/zip",
            $"reportes_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "No se pudo preparar descarga de reportes.");
        return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
    finally
    {
        foreach (var prepared in preparedFiles.Where(x => x.IsTemporary))
        {
            TryDeleteFile(prepared.Path);
        }
    }
}).RequireAuthorization(ApiAuthConstants.PolicyReportsGenerate);

app.Run();

static bool TryGetDisabledModule(PathString path, ApiModuleFeatureFlags flags, out string moduleName)
{
    moduleName = string.Empty;
    var value = path.Value ?? string.Empty;

    if (value.StartsWith("/ops/telemetry", StringComparison.OrdinalIgnoreCase) && !flags.EnableOpsTelemetry)
    {
        moduleName = "ops.telemetry";
        return true;
    }

    if (value.StartsWith("/ops", StringComparison.OrdinalIgnoreCase) && !flags.EnableOps)
    {
        moduleName = "ops";
        return true;
    }

    if (value.StartsWith("/preview", StringComparison.OrdinalIgnoreCase) && !flags.EnablePreview)
    {
        moduleName = "preview";
        return true;
    }

    if (value.StartsWith("/kpi", StringComparison.OrdinalIgnoreCase) && !flags.EnableKpi)
    {
        moduleName = "kpi";
        return true;
    }

    if (value.StartsWith("/admin/users", StringComparison.OrdinalIgnoreCase) && !flags.EnableAdminUsers)
    {
        moduleName = "admin.users";
        return true;
    }

    if (value.StartsWith("/admin/employees", StringComparison.OrdinalIgnoreCase) && !flags.EnableAdminEmployees)
    {
        moduleName = "admin.employees";
        return true;
    }

    if (value.StartsWith("/admin/manual-punch", StringComparison.OrdinalIgnoreCase) && !flags.EnableAdminManualPunch)
    {
        moduleName = "admin.manual_punch";
        return true;
    }

    if (value.StartsWith("/admin/pervasive", StringComparison.OrdinalIgnoreCase) && !flags.EnableAdminPervasive)
    {
        moduleName = "admin.pervasive";
        return true;
    }

    if (value.StartsWith("/admin/schedules", StringComparison.OrdinalIgnoreCase) && !flags.EnableAdminSchedules)
    {
        moduleName = "admin.schedules";
        return true;
    }

    if (value.StartsWith("/reports", StringComparison.OrdinalIgnoreCase) && !flags.EnableReports)
    {
        moduleName = "reports";
        return true;
    }

    return false;
}

static string[] NormalizeAllowedOrigins(IEnumerable<string>? origins)
{
    if (origins is null)
    {
        return Array.Empty<string>();
    }

    return origins
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static bool IsOriginAllowed(string origin, IReadOnlyCollection<string> allowedOrigins)
{
    if (string.IsNullOrWhiteSpace(origin) || allowedOrigins.Count == 0)
    {
        return false;
    }

    foreach (var allowedOrigin in allowedOrigins)
    {
        if (string.Equals(origin, allowedOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsWildcardMatch(origin, allowedOrigin))
        {
            continue;
        }

        return true;
    }

    return false;
}

static bool IsWildcardMatch(string origin, string allowedOriginPattern)
{
    const string wildcardToken = "*.";
    if (!allowedOriginPattern.Contains(wildcardToken, StringComparison.Ordinal))
    {
        return false;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
    {
        return false;
    }

    if (!Uri.TryCreate(allowedOriginPattern.Replace(wildcardToken, string.Empty), UriKind.Absolute, out var baseUri))
    {
        return false;
    }

    if (!string.Equals(originUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    if (!string.Equals(originUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
        && !originUri.Host.EndsWith("." + baseUri.Host, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return originUri.Port == baseUri.Port;
}

static string ResolveTelemetryStatus(IReadOnlyCollection<OpsTelemetryAlert> alerts)
{
    if (alerts.Any(a => string.Equals(a.Severity, "critical", StringComparison.OrdinalIgnoreCase)))
    {
        return "critical";
    }

    if (alerts.Count > 0)
    {
        return "warning";
    }

    return "ok";
}

static string BuildWorkingDaysText(DateTime fromDate, DateTime toDate)
{
    var days = new List<string>();
    for (var cursor = fromDate.Date; cursor <= toDate.Date; cursor = cursor.AddDays(1))
    {
        if (cursor.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            continue;
        }

        days.Add(cursor.ToString("dd/MM/yyyy"));
    }

    if (days.Count == 0)
    {
        days.Add(fromDate.ToString("dd/MM/yyyy"));
    }

    if (days.Count <= 12)
    {
        return string.Join(", ", days);
    }

    return string.Join(", ", days.Take(12)) + $" ... (+{days.Count - 12} dia(s))";
}

static bool TryParseTimeOfDay(string? value, out TimeSpan parsed)
{
    parsed = default;
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    return TimeSpan.TryParseExact(
        value.Trim(),
        new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss", "c" },
        CultureInfo.InvariantCulture,
        out parsed);
}

static async Task<IReadOnlyList<LatenessIncidentItem>> BuildSimulatedLatenessIncidentsAsync(
    int? userId,
    DateTime fromDate,
    DateTime toDate,
    ApiEmployeeAdminQueryService employeesQuery,
    CancellationToken cancellationToken)
{
    var employees = new List<ApiEmployeeAdminItem>();
    if (userId is > 0)
    {
        var employee = await employeesQuery.GetEmployeeAsync(userId.Value, cancellationToken);
        employees.Add(employee ?? new ApiEmployeeAdminItem(
            userId.Value,
            $"Empleado_{userId.Value}",
            null,
            null,
            null,
            null,
            1,
            true,
            8m,
            null));
    }
    else
    {
        employees.AddRange(await employeesQuery.GetEmployeesAsync(cancellationToken));
    }

    var items = new List<LatenessIncidentItem>();
    foreach (var employee in employees.OrderBy(x => x.UserId).Take(userId is > 0 ? 1 : 25))
    {
        var scheduleCode = employee.WorkScheduleCode ?? 1;
        var scheduledStart = ResolveScheduledStartForTemplate(scheduleCode);
        var tolerance = 10;

        for (var day = fromDate.Date; day <= toDate.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var delay = 5 + ((day.Day + employee.UserId) % 4) * 5;
            var firstCheckIn = day.Add(scheduledStart).AddMinutes(delay);
            items.Add(new LatenessIncidentItem(
                employee.UserId,
                string.IsNullOrWhiteSpace(employee.EmployeeName) ? $"Empleado_{employee.UserId}" : employee.EmployeeName,
                DateOnly.FromDateTime(day),
                firstCheckIn,
                scheduleCode,
                $"Horario {scheduleCode}",
                scheduledStart,
                tolerance,
                delay));
        }
    }

    return items;
}

static async Task<LatenessTemplateEmployeeReport> BuildLatenessTemplateEmployeeReportAsync(
    int userId,
    DateTime fromDate,
    DateTime toDate,
    IReadOnlyList<LatenessIncidentItem> incidents,
    AttendanceReportOrchestrator orchestrator,
    ApiEmployeeAdminQueryService employeesQuery,
    CancellationToken cancellationToken)
{
    var employee = await employeesQuery.GetEmployeeAsync(userId, cancellationToken);
    var employeeName = string.IsNullOrWhiteSpace(employee?.EmployeeName)
        ? incidents.FirstOrDefault()?.EmployeeName ?? $"Empleado_{userId}"
        : employee.EmployeeName!;

    var preview = await orchestrator.BuildPreviewAsync(
        new ReportRequest
        {
            UserId = userId,
            FechaInicio = fromDate,
            FechaFin = toDate,
            OutputDirectory = "N/A",
            TemplatePath = null,
            EmployeeName = employeeName,
            Area = employee?.Department,
            Position = employee?.Position,
            WorkScheduleCode = employee?.WorkScheduleCode,
            UseSimulatedDataWhenEmpty = true
        },
        cancellationToken);

    var incidentByDate = incidents
        .GroupBy(x => x.WorkDate)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.DelayMinutes).First());

    var previewByDate = preview.Days
        .GroupBy(x => x.Date)
        .ToDictionary(g => g.Key, g => g.First());

    var startDate = DateOnly.FromDateTime(fromDate.Date);
    var endDate = DateOnly.FromDateTime(toDate.Date);
    var defaultTolerance = incidents.FirstOrDefault()?.LateToleranceMinutes ?? 10;
    var defaultScheduledStart = incidents.FirstOrDefault()?.ScheduledStartTime
        ?? ResolveScheduledStartForTemplate(employee?.WorkScheduleCode);

    var rows = new List<LatenessTemplateDayRow>();
    for (var date = startDate; date <= endDate; date = date.AddDays(1))
    {
        incidentByDate.TryGetValue(date, out var incident);
        previewByDate.TryGetValue(date, out var previewDay);

        var actualEntry = previewDay?.Entrada;
        var delayMinutes = incident?.DelayMinutes ?? 0;
        var tolerance = incident?.LateToleranceMinutes ?? defaultTolerance;
        var computable = Math.Max(0, delayMinutes - tolerance);
        var hasRecord = actualEntry.HasValue && !string.Equals(previewDay?.Estatus, "SIN_REGISTRO", StringComparison.OrdinalIgnoreCase);

        var justification = hasRecord
            ? (computable > 0 ? "Retardo sobre tolerancia" : "Sin retardo")
            : "Sin registro";

        var actionTaken = computable > 0
            ? "Aplicar politica de retardos"
            : string.Empty;

        rows.Add(new LatenessTemplateDayRow(
            date,
            hasRecord ? actualEntry : null,
            delayMinutes,
            tolerance,
            justification,
            "Supervisor",
            actionTaken,
            previewDay?.Observaciones));
    }

    return new LatenessTemplateEmployeeReport(
        userId,
        employeeName,
        employee?.Department,
        employee?.Position,
        string.IsNullOrWhiteSpace(preview.WorkShiftType) ? "Diurna" : preview.WorkShiftType,
        "Supervisor",
        defaultScheduledStart,
        defaultTolerance,
        rows,
        $"Periodo {fromDate:dd/MM/yyyy} al {toDate:dd/MM/yyyy}.");
}

static decimal ResolveStandardShiftHoursForTemplate(int? workScheduleCode)
{
    return workScheduleCode switch
    {
        3 => 9.6m,
        _ => 8m
    };
}

static TimeSpan ResolveScheduledStartForTemplate(int? workScheduleCode)
{
    return workScheduleCode switch
    {
        2 => new TimeSpan(8, 0, 0),
        3 => new TimeSpan(18, 0, 0),
        _ => new TimeSpan(7, 0, 0)
    };
}

static void WriteLatenessReportWorkbook(
    string outputPath,
    DateTime fromDate,
    DateTime toDate,
    IReadOnlyList<LatenessIncidentItem> incidents)
{
    using var workbook = new XLWorkbook();
    var ws = workbook.Worksheets.Add("Retardos");
    ws.Style.Font.FontName = "Segoe UI";
    ws.Style.Font.FontSize = 10;

    ws.Cell("A1").Value = "REPORTE DE RETARDOS";
    ws.Range("A1:I1").Merge();
    ws.Cell("A1").Style.Font.Bold = true;
    ws.Cell("A1").Style.Font.FontSize = 14;
    ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    ws.Cell("A2").Value = $"Periodo: {fromDate:dd/MM/yyyy} al {toDate:dd/MM/yyyy}";
    ws.Range("A2:I2").Merge();
    ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    ws.Cell("A3").Value = $"Total incidencias: {incidents.Count}";
    ws.Range("A3:I3").Merge();
    ws.Cell("A3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    var headerRow = 5;
    ws.Cell(headerRow, 1).Value = "Fecha";
    ws.Cell(headerRow, 2).Value = "UserId";
    ws.Cell(headerRow, 3).Value = "Empleado";
    ws.Cell(headerRow, 4).Value = "Horario";
    ws.Cell(headerRow, 5).Value = "Entrada programada";
    ws.Cell(headerRow, 6).Value = "Primera entrada";
    ws.Cell(headerRow, 7).Value = "Tolerancia (min)";
    ws.Cell(headerRow, 8).Value = "Retardo (min)";
    ws.Cell(headerRow, 9).Value = "Observacion";

    ws.Range(headerRow, 1, headerRow, 9).Style.Font.Bold = true;
    ws.Range(headerRow, 1, headerRow, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F6");

    var row = headerRow + 1;
    foreach (var item in incidents.OrderBy(x => x.WorkDate).ThenBy(x => x.UserId))
    {
        ws.Cell(row, 1).Value = item.WorkDate.ToDateTime(TimeOnly.MinValue);
        ws.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Cell(row, 2).Value = item.UserId;
        ws.Cell(row, 3).Value = item.EmployeeName;
        ws.Cell(row, 4).Value = item.WorkScheduleCode.HasValue
            ? $"{item.WorkScheduleCode.Value} - {item.WorkScheduleName ?? "Horario"}"
            : (item.WorkScheduleName ?? "Sin horario");
        ws.Cell(row, 5).Value = item.ScheduledStartTime.ToString(@"hh\:mm");
        ws.Cell(row, 6).Value = item.FirstCheckIn.ToString("HH:mm");
        ws.Cell(row, 7).Value = item.LateToleranceMinutes;
        ws.Cell(row, 8).Value = item.DelayMinutes;
        ws.Cell(row, 9).Value = item.DelayMinutes > item.LateToleranceMinutes
            ? "Retardo sobre tolerancia"
            : "Dentro de tolerancia";
        row++;
    }

    if (incidents.Count == 0)
    {
        ws.Cell(row, 1).Value = "Sin incidencias en el periodo seleccionado.";
        ws.Range(row, 1, row, 9).Merge();
        ws.Cell(row, 1).Style.Font.Italic = true;
    }

    ws.Columns(1, 9).AdjustToContents();
    var usedRange = ws.RangeUsed();
    if (usedRange is not null)
    {
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }
    workbook.SaveAs(outputPath);
}
static string SanitizeFileName(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    var safe = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
    return string.IsNullOrWhiteSpace(safe) ? $"ot_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx" : safe;
}

static string? ResolveTemplatePath(string? requestTemplatePath, string? defaultTemplatePath, string contentRoot)
{
    var candidate = string.IsNullOrWhiteSpace(requestTemplatePath)
        ? defaultTemplatePath
        : requestTemplatePath;

    if (string.IsNullOrWhiteSpace(candidate))
    {
        return null;
    }

    var resolved = candidate.Trim();
    if (Path.IsPathRooted(resolved))
    {
        return File.Exists(resolved) ? resolved : null;
    }

    var probes = new List<string>();
    void AddProbe(string? basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return;
        }

        probes.Add(Path.GetFullPath(Path.Combine(basePath, resolved)));
    }

    AddProbe(contentRoot);
    AddProbe(Directory.GetParent(contentRoot)?.FullName);
    AddProbe(Directory.GetParent(Directory.GetParent(contentRoot)?.FullName ?? string.Empty)?.FullName);
    AddProbe(AppContext.BaseDirectory);
    AddProbe(Directory.GetCurrentDirectory());

    return probes
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault(File.Exists);
}

static string ResolveReportOutputRoot(string? configuredOutputDirectory, string contentRoot)
{
    var outputDirectory = string.IsNullOrWhiteSpace(configuredOutputDirectory)
        ? "Reports"
        : configuredOutputDirectory.Trim();

    if (!Path.IsPathRooted(outputDirectory))
    {
        outputDirectory = Path.Combine(contentRoot, outputDirectory);
    }

    return Path.GetFullPath(outputDirectory);
}

static bool IsUnderDirectory(string filePath, string directoryPath)
{
    var fullFilePath = Path.GetFullPath(filePath);
    var fullDirectoryPath = Path.GetFullPath(directoryPath)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;

    return fullFilePath.StartsWith(fullDirectoryPath, StringComparison.OrdinalIgnoreCase);
}

static string NormalizeDownloadExportFormat(string? exportFormat)
{
    var normalized = exportFormat?.Trim().ToLowerInvariant();
    return normalized is "csv" or "pdf" ? normalized : "source";
}

static PreparedDownloadFile PrepareDownloadFile(string sourcePath, string exportFormat)
{
    var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
    if (exportFormat == "csv")
    {
        if (extension == ".csv")
        {
            return new PreparedDownloadFile(sourcePath, Path.GetFileName(sourcePath), "text/csv; charset=utf-8", false);
        }

        if (extension is ".xlsx" or ".xlsm")
        {
            var csvPath = ConvertWorkbookToCsvTemp(sourcePath);
            return new PreparedDownloadFile(csvPath, Path.ChangeExtension(Path.GetFileName(sourcePath), ".csv"), "text/csv; charset=utf-8", true);
        }
    }

    if (exportFormat == "pdf")
    {
        if (extension == ".pdf")
        {
            return new PreparedDownloadFile(sourcePath, Path.GetFileName(sourcePath), "application/pdf", false);
        }

        if (extension is ".xlsx" or ".xlsm")
        {
            var pdfPath = ConvertExcelToPdfTemp(sourcePath);
            return new PreparedDownloadFile(pdfPath, Path.ChangeExtension(Path.GetFileName(sourcePath), ".pdf"), "application/pdf", true);
        }
    }

    return new PreparedDownloadFile(sourcePath, Path.GetFileName(sourcePath), ResolveContentType(sourcePath), false);
}

static string ConvertWorkbookToCsvTemp(string workbookPath)
{
    var csvPath = Path.Combine(
        Path.GetTempPath(),
        $"{Path.GetFileNameWithoutExtension(workbookPath)}_{Guid.NewGuid():N}.csv");

    using var workbook = new XLWorkbook(workbookPath);
    var preferredSheet = ResolvePreferredReportSheetName(workbookPath);
    var worksheet = !string.IsNullOrWhiteSpace(preferredSheet) && workbook.TryGetWorksheet(preferredSheet, out var byName)
        ? byName
        : workbook.Worksheets.FirstOrDefault(ws => !string.Equals(ws.Name, "Listas", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.First();

    var used = worksheet.RangeUsed();
    if (used is null)
    {
        File.WriteAllText(csvPath, string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return csvPath;
    }

    var builder = new StringBuilder();
    var firstRow = used.RangeAddress.FirstAddress.RowNumber;
    var lastRow = used.RangeAddress.LastAddress.RowNumber;
    var firstColumn = used.RangeAddress.FirstAddress.ColumnNumber;
    var lastColumn = used.RangeAddress.LastAddress.ColumnNumber;

    for (var row = firstRow; row <= lastRow; row++)
    {
        if (row > firstRow)
        {
            builder.AppendLine();
        }

        for (var column = firstColumn; column <= lastColumn; column++)
        {
            if (column > firstColumn)
            {
                builder.Append(',');
            }

            builder.Append(EscapeCsvValue(worksheet.Cell(row, column).GetFormattedString()));
        }
    }

    File.WriteAllText(csvPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    return csvPath;
}

static string EscapeCsvValue(string? value)
{
    if (string.IsNullOrEmpty(value))
    {
        return string.Empty;
    }

    return value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n')
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;
}

static string ConvertExcelToPdfTemp(string excelPath)
{
    var pdfPath = Path.Combine(
        Path.GetTempPath(),
        $"{Path.GetFileNameWithoutExtension(excelPath)}_{Guid.NewGuid():N}.pdf");

    var excelType = Type.GetTypeFromProgID("Excel.Application");
    if (excelType is null)
    {
        throw new InvalidOperationException("No se puede exportar PDF porque Microsoft Excel no esta instalado en este equipo.");
    }

    object? excelAppObj = null;
    object? workbooksObj = null;
    object? workbookObj = null;
    object? worksheetObj = null;

    try
    {
        excelAppObj = Activator.CreateInstance(excelType)
            ?? throw new InvalidOperationException("No se pudo iniciar Microsoft Excel para exportar PDF.");

        dynamic excelApp = excelAppObj;
        excelApp.Visible = false;
        excelApp.DisplayAlerts = false;

        workbooksObj = excelApp.Workbooks;
        dynamic workbooks = workbooksObj;
        workbookObj = OpenExcelWorkbook(workbooksObj, excelPath);

        dynamic workbook = workbookObj;
        var preferredSheet = ResolvePreferredReportSheetName(excelPath);
        if (!string.IsNullOrWhiteSpace(preferredSheet))
        {
            try
            {
                worksheetObj = workbook.Worksheets[preferredSheet];
            }
            catch
            {
                worksheetObj = null;
            }
        }

        worksheetObj ??= workbook.Worksheets[1];
        try
        {
            dynamic worksheet = worksheetObj;
            worksheet.Activate();
            ConfigureComWorksheetForDownloadPdf(worksheet);
        }
        catch
        {
        }

        const int xlTypePdf = 0;
        const int xlQualityStandard = 0;
        dynamic exportSheet = worksheetObj;
        exportSheet.ExportAsFixedFormat(xlTypePdf, pdfPath, xlQualityStandard, true, false);
        workbook.Close(false);
        excelApp.Quit();

        if (!File.Exists(pdfPath))
        {
            throw new InvalidOperationException("Excel no genero el archivo PDF esperado.");
        }

        return pdfPath;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            "No se pudo exportar el PDF. Verifica que Excel este instalado y sin ventanas de bloqueo. " +
            $"Detalle: {ex.Message}",
            ex);
    }
    finally
    {
        TryCloseComWorkbook(workbookObj);
        TryQuitExcel(excelAppObj);
        ReleaseComObject(worksheetObj);
        ReleaseComObject(workbookObj);
        ReleaseComObject(workbooksObj);
        ReleaseComObject(excelAppObj);
    }
}

static string? ResolvePreferredReportSheetName(string path)
{
    var fileName = Path.GetFileName(path).ToLowerInvariant();
    var directory = Path.GetDirectoryName(path)?.ToLowerInvariant() ?? string.Empty;

    if (fileName.Contains("retardo") || directory.Contains("retardo"))
    {
        return "Retardos";
    }

    if (fileName.StartsWith("ot_", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("horas")
        || directory.Contains("overtime")
        || directory.Contains("horas"))
    {
        return "Horas Extra";
    }

    return null;
}

static object OpenExcelWorkbook(object workbooksObj, string excelPath)
{
    try
    {
        return InvokeExcelOpen(workbooksObj, new object[] { excelPath });
    }
    catch
    {
        return InvokeExcelOpen(workbooksObj, new object[]
        {
            excelPath, 0, false, 5, string.Empty, string.Empty, true, 1,
            string.Empty, false, false, 0, false, true, 1
        });
    }
}

static object InvokeExcelOpen(object workbooksObj, object[] args)
{
    return workbooksObj.GetType().InvokeMember(
        "Open",
        System.Reflection.BindingFlags.InvokeMethod,
        binder: null,
        target: workbooksObj,
        args: args)
        ?? throw new InvalidOperationException("Excel no pudo abrir el archivo para exportar PDF.");
}

static void ConfigureComWorksheetForDownloadPdf(dynamic worksheet)
{
    try
    {
        worksheet.ResetAllPageBreaks();
        dynamic pageSetup = worksheet.PageSetup;
        pageSetup.Zoom = false;
        pageSetup.FitToPagesWide = 1;
        pageSetup.FitToPagesTall = 1;
        pageSetup.Orientation = 2;
        pageSetup.CenterHorizontally = true;
        pageSetup.TopMargin = 18;
        pageSetup.BottomMargin = 18;
        pageSetup.LeftMargin = 14;
        pageSetup.RightMargin = 14;
    }
    catch
    {
    }
}

static string ResolveContentType(string path)
{
    return Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xlsm" => "application/vnd.ms-excel.sheet.macroEnabled.12",
        ".pdf" => "application/pdf",
        ".csv" => "text/csv; charset=utf-8",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}

static string CreateUniqueZipEntryName(string fileName, HashSet<string> usedNames)
{
    var safeName = SanitizeFileName(fileName);
    var extension = Path.GetExtension(safeName);
    var baseName = Path.GetFileNameWithoutExtension(safeName);
    var candidate = safeName;
    var suffix = 1;

    while (usedNames.Contains(candidate))
    {
        candidate = $"{baseName}_{suffix}{extension}";
        suffix++;
    }

    usedNames.Add(candidate);
    return candidate;
}

static void TryDeleteFile(string path)
{
    try
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    catch
    {
    }
}

static void TryCloseComWorkbook(object? workbookObj)
{
    if (workbookObj is null)
    {
        return;
    }

    try
    {
        dynamic workbook = workbookObj;
        workbook.Close(false);
    }
    catch
    {
    }
}

static void TryQuitExcel(object? excelAppObj)
{
    if (excelAppObj is null)
    {
        return;
    }

    try
    {
        dynamic excelApp = excelAppObj;
        excelApp.Quit();
    }
    catch
    {
    }
}

static void ReleaseComObject(object? value)
{
    if (value is null)
    {
        return;
    }

    try
    {
        if (Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
    catch
    {
    }
}

internal sealed record AuthLoginRequest(string Username, string Password);
internal sealed record AuthChangePasswordRequest(string CurrentPassword, string NewPassword);
internal sealed record OpsAutoSyncIntervalRequest(int IntervalMinutes);
internal sealed record AdminEmployeeReassignPreviewRequest(
    int OldUserId,
    int NewUserId,
    DateTime From,
    DateTime To,
    bool IncludePervasive = false);
internal sealed record AdminEmployeeReassignApplyRequest(
    int OldUserId,
    int NewUserId,
    DateTime From,
    DateTime To,
    string? NewName,
    string? NewDepartment,
    string? NewPervasiveDept,
    string? NewPervasiveShift,
    bool AllowSkipConflicts,
    bool IncludePervasive,
    bool ConfirmApply,
    string? ConfirmText);
internal sealed record AdminEmployeeUpsertRequest(
    string EmployeeName,
    string? Department,
    string? Position,
    string? PervasiveDept,
    string? PervasiveShift,
    int? WorkScheduleCode,
    bool OvertimeAuthorized);
internal sealed record AdminWorkScheduleUpsertRequest(
    string Name,
    string StartTime,
    string EndTime,
    decimal StandardHours,
    bool IsActive,
    int LateToleranceMinutes = 10);
internal sealed record AdminManualPunchRequest(
    int UserId,
    string EmployeeName,
    DateTime? CheckTime,
    int? InOutMode,
    List<AdminManualPunchEntryRequest>? BatchEntries);
internal sealed record AdminManualPunchBatchRequest(
    int UserId,
    string EmployeeName,
    List<AdminManualPunchEntryRequest> BatchEntries);
internal sealed record AdminManualPunchEntryRequest(DateTime CheckTime, int? InOutMode);
internal sealed record AdminPervasiveNightCleanupRequest(
    DateTime From,
    DateTime To,
    int? UserId);
internal sealed record AdminPervasiveNightCleanupRunRequest(
    DateTime From,
    DateTime To,
    int? UserId,
    bool ConfirmApply,
    string? ConfirmText);
internal sealed record ReportGenerateRequest(
    int? UserId,
    DateTime From,
    DateTime To,
    string? OutputFormat,
    string? OutputDirectory,
    string? TemplatePath,
    string? Company,
    bool UseSimulatedDataWhenEmpty = false);
internal sealed record OvertimeFormatGenerateRequest(
    int UserId,
    string? EmployeeName,
    string WorkOrderNumber,
    string WorkDescription,
    DateTime RequestDate,
    DateTime From,
    DateTime To,
    string? WorkingDays,
    string? WorkSchedule,
    string ImmediateBossName,
    string? ImmediateBossSignatureName,
    string? EmployeeSignatureName,
    string? Notes,
    string? OutputDirectory,
    string? FileName,
    bool UseSimulatedDataWhenEmpty = false);
internal sealed record LatenessReportGenerateRequest(
    int? UserId,
    DateTime From,
    DateTime To,
    string? OutputDirectory,
    string? FileName,
    bool UseSimulatedDataWhenEmpty = false);
internal sealed record ReportFilesDownloadRequest(
    List<string> Paths,
    string? ExportFormat);
internal sealed record PreparedDownloadFile(
    string Path,
    string FileName,
    string ContentType,
    bool IsTemporary);
internal sealed record OpsTelemetryAlert(
    string Code,
    string Severity,
    string Message,
    DateTimeOffset? LastSeenAt = null);

