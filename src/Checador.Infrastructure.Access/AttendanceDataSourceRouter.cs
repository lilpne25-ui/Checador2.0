using Checador.Domain.Interfaces;
using Checador.Domain.Models;

namespace Checador.Infrastructure.Access;

public sealed class AttendanceDataSourceRouter : IAttendanceDataSource
{
    private readonly AccessAttendanceDataSource _access;
    private readonly ZkTecoDirectAttendanceDataSource _direct;
    private readonly SqlAttendanceDataSource _sql;
    private readonly DirectDeviceOptions _options;
    private readonly DataSourceOptions _dataSourceOptions;

    public AttendanceDataSourceRouter(
        AccessAttendanceDataSource access,
        ZkTecoDirectAttendanceDataSource direct,
        SqlAttendanceDataSource sql,
        DirectDeviceOptions options,
        DataSourceOptions dataSourceOptions)
    {
        _access = access;
        _direct = direct;
        _sql = sql;
        _options = options;
        _dataSourceOptions = dataSourceOptions;
    }

    public async Task<IReadOnlyList<PunchRecord>> GetPunchesAsync(int? userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var primary = (_dataSourceOptions.Primary ?? string.Empty).Trim();
        if (primary.Equals("Sql", StringComparison.OrdinalIgnoreCase))
        {
            return await _sql.GetPunchesAsync(userId, from, to, cancellationToken);
        }

        if (primary.Equals("Access", StringComparison.OrdinalIgnoreCase))
        {
            return await _access.GetPunchesAsync(userId, from, to, cancellationToken);
        }

        if (_options.Enabled && _options.PreferDirect)
        {
            try
            {
                return await _direct.GetPunchesAsync(userId, from, to, cancellationToken);
            }
            catch when (_options.FallbackToAccess)
            {
                return await _access.GetPunchesAsync(userId, from, to, cancellationToken);
            }
        }

        return await _access.GetPunchesAsync(userId, from, to, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeInfo>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        var primary = (_dataSourceOptions.Primary ?? string.Empty).Trim();
        if (primary.Equals("Sql", StringComparison.OrdinalIgnoreCase))
        {
            return await _sql.GetEmployeesAsync(cancellationToken);
        }

        if (primary.Equals("Access", StringComparison.OrdinalIgnoreCase))
        {
            return await _access.GetEmployeesAsync(cancellationToken);
        }

        if (_options.Enabled && _options.PreferDirect)
        {
            try
            {
                var directEmployees = await _direct.GetEmployeesAsync(cancellationToken);
                if (directEmployees.Count > 0 || !_options.FallbackToAccess)
                {
                    return directEmployees;
                }
            }
            catch when (_options.FallbackToAccess)
            {
            }
        }

        return await _access.GetEmployeesAsync(cancellationToken);
    }
}
