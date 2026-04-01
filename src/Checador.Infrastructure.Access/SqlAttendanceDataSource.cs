using Checador.Domain.Interfaces;
using Checador.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Checador.Infrastructure.Access;

public sealed class SqlAttendanceDataSource : IAttendanceDataSource
{
    private readonly SqlSyncOptions _options;
    private readonly SqlDeviceSyncService _syncService;

    public SqlAttendanceDataSource(SqlSyncOptions options, SqlDeviceSyncService syncService)
    {
        _options = options;
        _syncService = syncService;
    }

    public async Task<IReadOnlyList<PunchRecord>> GetPunchesAsync(int? userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        await _syncService.EnsureReadyAsync(cancellationToken);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT
                p.UserId,
                COALESCE(NULLIF(e.EmployeeName, ''), p.EmployeeName) AS EmployeeName,
                p.CheckTime,
                p.InOutMode,
                e.WorkScheduleCode,
                e.OvertimeAuthorized,
        e.StandardShiftHours,
        CASE
            WHEN ws.StartTime IS NOT NULL AND ws.EndTime IS NOT NULL AND ws.StartTime > ws.EndTime THEN CAST(1 AS bit)
            ELSE CAST(0 AS bit)
        END AS IsNightShift
        FROM dbo.PunchLogs p
        LEFT JOIN dbo.Employees e ON e.UserId = p.UserId
    LEFT JOIN dbo.WorkSchedules ws ON ws.Code = e.WorkScheduleCode
WHERE CheckTime >= @FromDate
  AND CheckTime <= @ToDate
            AND (@UserId IS NULL OR p.UserId = @UserId)
        ORDER BY p.UserId, p.CheckTime;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FromDate", from);
        cmd.Parameters.AddWithValue("@ToDate", to);
        cmd.Parameters.AddWithValue("@UserId", userId.HasValue ? userId.Value : DBNull.Value);

        var rows = new List<PunchRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PunchRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                await reader.IsDBNullAsync(3, cancellationToken) ? null : reader.GetInt32(3),
                await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetInt32(4),
                await reader.IsDBNullAsync(5, cancellationToken) ? null : reader.GetBoolean(5),
                await reader.IsDBNullAsync(6, cancellationToken) ? null : reader.GetDecimal(6),
                await reader.IsDBNullAsync(7, cancellationToken) ? null : reader.GetBoolean(7)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<EmployeeInfo>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        await _syncService.EnsureReadyAsync(cancellationToken);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sqlEmployees = @"
SELECT UserId, EmployeeName
FROM dbo.Employees
ORDER BY UserId;";

        await using var cmd = new SqlCommand(sqlEmployees, connection);
        var result = new List<EmployeeInfo>();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new EmployeeInfo(reader.GetInt32(0), reader.GetString(1)));
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        const string sqlFallback = @"
SELECT UserId, MAX(EmployeeName) AS EmployeeName
FROM dbo.PunchLogs
GROUP BY UserId
ORDER BY UserId;";

        await using var cmdFallback = new SqlCommand(sqlFallback, connection);
        await using var readerFallback = await cmdFallback.ExecuteReaderAsync(cancellationToken);
        while (await readerFallback.ReadAsync(cancellationToken))
        {
            result.Add(new EmployeeInfo(readerFallback.GetInt32(0), readerFallback.GetString(1)));
        }

        return result;
    }
}
