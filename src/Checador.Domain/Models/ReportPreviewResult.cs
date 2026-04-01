namespace Checador.Domain.Models;

public sealed class ReportPreviewResult
{
    public int UserId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? Company { get; init; }
    public string? Area { get; init; }
    public string? Position { get; init; }
    public int? WorkScheduleCode { get; init; }
    public string? WorkScheduleDisplay { get; init; }
    public string? WorkShiftType { get; init; }
    public DateTime FechaInicio { get; init; }
    public DateTime FechaFin { get; init; }
    public decimal TotalWorkedHours { get; init; }
    public decimal TotalExtraHours { get; init; }
    public IReadOnlyList<AttendanceDayResult> Days { get; init; } = Array.Empty<AttendanceDayResult>();
}
