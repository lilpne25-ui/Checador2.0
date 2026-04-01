namespace Checador.Domain.Models;

public sealed class DailyPunches
{
    public int UserId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public IReadOnlyList<PunchRecord> Punches { get; init; } = Array.Empty<PunchRecord>();
}
