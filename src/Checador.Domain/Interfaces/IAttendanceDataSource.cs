using Checador.Domain.Models;

namespace Checador.Domain.Interfaces;

public interface IAttendanceDataSource
{
    Task<IReadOnlyList<PunchRecord>> GetPunchesAsync(int? userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeInfo>> GetEmployeesAsync(CancellationToken cancellationToken = default);
}
