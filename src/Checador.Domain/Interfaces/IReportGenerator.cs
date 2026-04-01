using Checador.Domain.Models;

namespace Checador.Domain.Interfaces;

public interface IReportGenerator
{
    Task<string> GenerateWeeklyReportAsync(
        IReadOnlyList<AttendanceDayResult> days,
        string outputDirectory,
        string? templatePath,
        ReportTemplateContext? templateContext = null,
    CancellationToken cancellationToken = default,
    ReportOutputFormat outputFormat = ReportOutputFormat.Excel);
}
