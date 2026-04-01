using Checador.Domain.Models;

namespace Checador.Infrastructure.Access;

public sealed class AccessToSqlMigrationService
{
    private readonly AccessAttendanceDataSource _access;
    private readonly ZkTecoDirectAttendanceDataSource _direct;
    private readonly SqlDeviceSyncService _sql;

    public AccessToSqlMigrationService(
        AccessAttendanceDataSource access,
        ZkTecoDirectAttendanceDataSource direct,
        SqlDeviceSyncService sql)
    {
        _access = access;
        _direct = direct;
        _sql = sql;
    }

    public async Task<SqlSyncRunResult> MigrateHistoricalAsync(CancellationToken cancellationToken = default)
    {
        var from = new DateTime(2000, 1, 1);
        var to = DateTime.Now;

        try
        {
            var employees = await _access.GetEmployeesAsync(cancellationToken);
            var punches = await _access.GetPunchesAsync(null, from, to, cancellationToken);

            return await _sql.ImportSnapshotAsync(
                employees,
                punches,
                source: "AccessHistorical",
                runLabel: "Migración histórica Access→SQL",
                overrideLastSync: to,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (IsAceProviderMissing(ex))
        {
            var employees = await _direct.GetEmployeesAsync(cancellationToken);
            var punches = await _direct.GetPunchesAsync(null, from, to, cancellationToken);

            var fallbackResult = await _sql.ImportSnapshotAsync(
                employees,
                punches,
                source: "DirectHistoricalFallback",
                runLabel: "Migración histórica fallback (Reloj→SQL)",
                overrideLastSync: punches.Count > 0 ? punches.Max(p => p.CheckTime) : to,
                cancellationToken: cancellationToken);

            return fallbackResult with
            {
                Message = fallbackResult.Message +
                          " | Access no disponible (ACE no instalado). Se aplicó fallback desde reloj directo."
            };
        }
    }

    private static bool IsAceProviderMissing(Exception ex)
    {
        var full = ex.ToString();
        return full.Contains("Microsoft.ACE.OLEDB", StringComparison.OrdinalIgnoreCase)
            || full.Contains("ACE OLEDB", StringComparison.OrdinalIgnoreCase)
            || full.Contains("provider is not registered", StringComparison.OrdinalIgnoreCase);
    }
}
