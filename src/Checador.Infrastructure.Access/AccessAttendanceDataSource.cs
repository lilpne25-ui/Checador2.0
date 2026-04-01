using System.Data;
using System.Data.OleDb;
using Checador.Domain.Interfaces;
using Checador.Domain.Models;

namespace Checador.Infrastructure.Access;

public sealed class AccessAttendanceDataSource : IAttendanceDataSource
{
    private readonly AccessConnectionOptions _options;

    public AccessAttendanceDataSource(AccessConnectionOptions options)
    {
        _options = options;
    }

    public async Task<IReadOnlyList<PunchRecord>> GetPunchesAsync(
        int? userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PunchRecord>();

        var query = @"
SELECT
    U.USERID,
    U.[NAME],
    C.CHECKTIME
FROM
    USERINFO U
INNER JOIN
    CHECKINOUT C ON U.USERID = C.USERID
WHERE
    C.CHECKTIME BETWEEN ? AND ?";

        if (userId.HasValue)
        {
            query += @"
    AND U.USERID = ?";
        }

        query += @"
ORDER BY
    U.USERID, C.CHECKTIME;";

        try
        {
            await using var connection = new OleDbConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(query, connection)
            {
                CommandType = CommandType.Text,
                CommandTimeout = _options.CommandTimeoutSeconds
            };

            command.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = from });
            command.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = to });

            if (userId.HasValue)
            {
                command.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = userId.Value });
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (reader is null)
            {
                return results;
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var id = Convert.ToInt32(reader[0]);
                var name = Convert.ToString(reader[1]) ?? string.Empty;
                var checkTime = Convert.ToDateTime(reader[2]);
                results.Add(new PunchRecord(id, name, checkTime));
            }

            return results;
        }
        catch (OleDbException ex)
        {
            throw new InvalidOperationException(
                "Error al leer Access (revisa permisos UNC, driver ACE y bloqueo del archivo .ldb).",
                ex);
        }
    }

    public async Task<IReadOnlyList<EmployeeInfo>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        var employees = new List<EmployeeInfo>();

        const string query = @"
SELECT
    U.USERID,
    U.[NAME]
FROM
    USERINFO U
ORDER BY
    U.USERID;";

        try
        {
            await using var connection = new OleDbConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(query, connection)
            {
                CommandType = CommandType.Text,
                CommandTimeout = _options.CommandTimeoutSeconds
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (reader is null)
            {
                return employees;
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var id = Convert.ToInt32(reader[0]);
                var name = Convert.ToString(reader[1]) ?? string.Empty;
                employees.Add(new EmployeeInfo(id, name));
            }

            return employees;
        }
        catch (OleDbException ex)
        {
            throw new InvalidOperationException(
                "Error al leer empleados desde Access (tabla USERINFO).",
                ex);
        }
    }
}
