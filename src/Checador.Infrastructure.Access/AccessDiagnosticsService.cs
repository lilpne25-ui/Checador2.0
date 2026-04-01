using System.Data;
using System.Data.OleDb;
using Checador.Domain.Interfaces;
using Checador.Domain.Models;

namespace Checador.Infrastructure.Access;

public sealed class AccessDiagnosticsService : IAttendanceDiagnostics
{
    private readonly AccessConnectionOptions _options;

    public AccessDiagnosticsService(AccessConnectionOptions options)
    {
        _options = options;
    }

    public async Task<AccessDiagnosticsResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new OleDbConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var employeeCount = await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM USERINFO", cancellationToken);
            var lastCheckTime = await ExecuteScalarDateTimeAsync(connection, "SELECT MAX(CHECKTIME) FROM CHECKINOUT", cancellationToken);

            return new AccessDiagnosticsResult
            {
                IsConnected = true,
                EmployeeCount = employeeCount,
                LastCheckTime = lastCheckTime,
                Message = "Conexión exitosa al archivo att2000.mdb"
            };
        }
        catch (Exception ex)
        {
            return new AccessDiagnosticsResult
            {
                IsConnected = false,
                EmployeeCount = 0,
                LastCheckTime = null,
                Message = $"No se pudo conectar a Access: {ex.Message}"
            };
        }
    }

    private static async Task<int> ExecuteScalarIntAsync(OleDbConnection connection, string query, CancellationToken cancellationToken)
    {
        await using var command = new OleDbCommand(query, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 120
        };

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    private static async Task<DateTime?> ExecuteScalarDateTimeAsync(OleDbConnection connection, string query, CancellationToken cancellationToken)
    {
        await using var command = new OleDbCommand(query, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 120
        };

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value ? null : Convert.ToDateTime(result);
    }
}
