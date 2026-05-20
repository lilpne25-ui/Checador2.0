using Checador.Infrastructure.Access;

namespace Checador.Api.Ops;

public sealed class OpsAutoSyncController : IDisposable
{
    private readonly SqlDeviceSyncService _syncService;
    private readonly ILogger<OpsAutoSyncController> _logger;
    private readonly SemaphoreSlim _cycleLock = new(1, 1);
    private readonly object _stateLock = new();

    private Timer? _timer;
    private bool _enabled;
    private bool _busy;
    private int _intervalMinutes;

    private DateTimeOffset? _lastRunStartedAt;
    private DateTimeOffset? _lastRunFinishedAt;
    private DateTimeOffset? _lastSuccessAt;
    private DateTimeOffset? _lastFailureAt;
    private int _successCount;
    private int _failureCount;
    private string _lastMessage = "AutoSync no iniciado.";

    public OpsAutoSyncController(SqlDeviceSyncService syncService, SqlSyncOptions sqlOptions, ILogger<OpsAutoSyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
        _intervalMinutes = ClampInterval(sqlOptions.AutoSyncMinutes);
    }

    public OpsAutoSyncStatus GetStatus()
    {
        lock (_stateLock)
        {
            return new OpsAutoSyncStatus(
                _enabled,
                _busy,
                _intervalMinutes,
                _lastRunStartedAt,
                _lastRunFinishedAt,
                _lastSuccessAt,
                _lastFailureAt,
                _successCount,
                _failureCount,
                _lastMessage);
        }
    }

    public OpsAutoSyncStatus Start()
    {
        lock (_stateLock)
        {
            if (_enabled)
            {
                _lastMessage = "AutoSync ya estaba activo.";
                return BuildStatusUnsafe();
            }

            _timer?.Dispose();
            _timer = new Timer(OnTimerTick, null, TimeSpan.FromMinutes(_intervalMinutes), TimeSpan.FromMinutes(_intervalMinutes));
            _enabled = true;
            _lastMessage = $"AutoSync activado cada {_intervalMinutes} minuto(s).";
            _logger.LogInformation("ops.autosync.start intervalMinutes={IntervalMinutes}", _intervalMinutes);
            return BuildStatusUnsafe();
        }
    }

    public OpsAutoSyncStatus Stop()
    {
        lock (_stateLock)
        {
            if (!_enabled)
            {
                _lastMessage = "AutoSync ya estaba detenido.";
                return BuildStatusUnsafe();
            }

            _timer?.Dispose();
            _timer = null;
            _enabled = false;
            _busy = false;
            _lastMessage = "AutoSync detenido.";
            _logger.LogInformation("ops.autosync.stop");
            return BuildStatusUnsafe();
        }
    }

    public OpsAutoSyncStatus SetIntervalMinutes(int intervalMinutes)
    {
        var safeInterval = ClampInterval(intervalMinutes);
        lock (_stateLock)
        {
            _intervalMinutes = safeInterval;
            if (_enabled && _timer is not null)
            {
                _timer.Change(TimeSpan.FromMinutes(_intervalMinutes), TimeSpan.FromMinutes(_intervalMinutes));
            }

            _lastMessage = $"Intervalo AutoSync configurado a {_intervalMinutes} minuto(s).";
            _logger.LogInformation("ops.autosync.interval intervalMinutes={IntervalMinutes}", _intervalMinutes);
            return BuildStatusUnsafe();
        }
    }

    private static int ClampInterval(int minutes) => Math.Clamp(minutes <= 0 ? 15 : minutes, 1, 240);

    private OpsAutoSyncStatus BuildStatusUnsafe()
        => new(
            _enabled,
            _busy,
            _intervalMinutes,
            _lastRunStartedAt,
            _lastRunFinishedAt,
            _lastSuccessAt,
            _lastFailureAt,
            _successCount,
            _failureCount,
            _lastMessage);

    private void OnTimerTick(object? _)
    {
        _ = RunScheduledCycleAsync();
    }

    private async Task RunScheduledCycleAsync()
    {
        if (!await _cycleLock.WaitAsync(0))
        {
            lock (_stateLock)
            {
                _lastMessage = "Ciclo AutoSync omitido: ejecución previa en curso.";
            }
            return;
        }

        try
        {
            lock (_stateLock)
            {
                _busy = true;
                _lastRunStartedAt = DateTimeOffset.Now;
                _lastMessage = "AutoSync en ejecución...";
            }

            var result = await _syncService.DownloadAttendanceLogsAsync();
            lock (_stateLock)
            {
                _successCount++;
                _lastSuccessAt = DateTimeOffset.Now;
                _lastMessage = result.Message;
            }

            _logger.LogInformation(
                "ops.autosync.cycle.success inserted={Inserted} duplicated={Duplicated}",
                result.InsertedPunches,
                result.DuplicatePunches);
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                _failureCount++;
                _lastFailureAt = DateTimeOffset.Now;
                _lastMessage = ex.Message;
            }

            _logger.LogWarning(ex, "ops.autosync.cycle.failed");
        }
        finally
        {
            lock (_stateLock)
            {
                _busy = false;
                _lastRunFinishedAt = DateTimeOffset.Now;
            }

            _cycleLock.Release();
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cycleLock.Dispose();
    }
}

public sealed record OpsAutoSyncStatus(
    bool Enabled,
    bool Busy,
    int IntervalMinutes,
    DateTimeOffset? LastRunStartedAt,
    DateTimeOffset? LastRunFinishedAt,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    int SuccessCount,
    int FailureCount,
    string LastMessage);
