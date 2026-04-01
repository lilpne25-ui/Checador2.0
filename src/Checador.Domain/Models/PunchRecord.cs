namespace Checador.Domain.Models;

public sealed record PunchRecord(
    int UserId,
    string EmployeeName,
    DateTime CheckTime,
    int? InOutMode = null,
    int? WorkScheduleCode = null,
    bool? OvertimeAuthorized = null,
    decimal? StandardShiftHours = null,
    bool? IsNightShift = null);
