namespace Checador.Domain.Models;

public sealed class ReportTemplateContext
{
    public string? EmployeeName { get; init; }
    public string? Company { get; init; }
    public string? Area { get; init; }
    public string? Position { get; init; }
    public int? WorkScheduleCode { get; init; }
    public string? WorkScheduleDisplay { get; init; }
    public string? WorkShiftType { get; init; }
}
