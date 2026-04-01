namespace Checador.Domain.Models;

public sealed class ReportRunSummary
{
    public int EmployeesProcessed { get; init; }
    public int EmployeesWithoutRecords { get; init; }
    public decimal TotalWorkedHours { get; init; }
    public decimal TotalExtraHours { get; init; }
    public IReadOnlyList<string> ReportPaths { get; init; } = Array.Empty<string>();
}
