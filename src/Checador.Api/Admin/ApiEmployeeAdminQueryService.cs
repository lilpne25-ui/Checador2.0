using Microsoft.Data.SqlClient;

namespace Checador.Api.Admin;

public sealed class ApiEmployeeAdminQueryService
{
    private readonly string _connectionString;

    public ApiEmployeeAdminQueryService(IConfiguration configuration)
    {
        _connectionString = configuration["Sql:ConnectionString"]
            ?? throw new InvalidOperationException("No se encontró Sql:ConnectionString para consulta de empleados admin.");
    }

    public async Task<IReadOnlyList<ApiEmployeeAdminItem>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

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
    UpdatedAt
FROM dbo.Employees
ORDER BY UserId;";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var list = new List<ApiEmployeeAdminItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new ApiEmployeeAdminItem(
                reader.GetInt32(0),
                await reader.IsDBNullAsync(1, cancellationToken) ? string.Empty : reader.GetString(1),
                await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetString(2),
                await reader.IsDBNullAsync(3, cancellationToken) ? null : reader.GetString(3),
                await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetString(4),
                await reader.IsDBNullAsync(5, cancellationToken) ? null : reader.GetString(5),
                await reader.IsDBNullAsync(6, cancellationToken) ? (int?)null : reader.GetInt32(6),
                !await reader.IsDBNullAsync(7, cancellationToken) && reader.GetBoolean(7),
                await reader.IsDBNullAsync(8, cancellationToken) ? 9.60m : reader.GetDecimal(8),
                await reader.IsDBNullAsync(9, cancellationToken) ? (DateTime?)null : reader.GetDateTime(9)));
        }

        return list;
    }

    public async Task<ApiEmployeeAdminItem?> GetEmployeeAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
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
    OvertimeAuthorized,
    StandardShiftHours,
    UpdatedAt
FROM dbo.Employees
WHERE UserId = @UserId;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ApiEmployeeAdminItem(
            reader.GetInt32(0),
            await reader.IsDBNullAsync(1, cancellationToken) ? string.Empty : reader.GetString(1),
            await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetString(2),
            await reader.IsDBNullAsync(3, cancellationToken) ? null : reader.GetString(3),
            await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetString(4),
            await reader.IsDBNullAsync(5, cancellationToken) ? null : reader.GetString(5),
            await reader.IsDBNullAsync(6, cancellationToken) ? (int?)null : reader.GetInt32(6),
            !await reader.IsDBNullAsync(7, cancellationToken) && reader.GetBoolean(7),
            await reader.IsDBNullAsync(8, cancellationToken) ? 9.60m : reader.GetDecimal(8),
            await reader.IsDBNullAsync(9, cancellationToken) ? (DateTime?)null : reader.GetDateTime(9));
    }
}

public sealed record ApiEmployeeAdminItem(
    int UserId,
    string EmployeeName,
    string? Department,
    string? Position,
    string? PervasiveDept,
    string? PervasiveShift,
    int? WorkScheduleCode,
    bool OvertimeAuthorized,
    decimal StandardShiftHours,
    DateTime? UpdatedAt);
