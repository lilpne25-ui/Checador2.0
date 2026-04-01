namespace Checador.Domain.Models;

public sealed class ReportRequest
{
    public int UserId { get; init; }
    public DateTime FechaInicio { get; init; }
    public DateTime FechaFin { get; init; }
    public string OutputDirectory { get; init; } = string.Empty;
    public string? TemplatePath { get; init; }
    public string? EmployeeName { get; init; }
    public string? Company { get; init; }
    public string? Area { get; init; }
    public string? Position { get; init; }
    public int? WorkScheduleCode { get; init; }
    public string? WorkScheduleDisplay { get; init; }
    public string? WorkShiftType { get; init; }
}
