using Checador.Domain.Interfaces;
using Checador.Domain.Models;

namespace Checador.Api;

public sealed class NoOpReportGenerator : IReportGenerator
{
    public Task<string> GenerateWeeklyReportAsync(
        IReadOnlyList<AttendanceDayResult> days,
        string outputDirectory,
        string? templatePath,
        ReportTemplateContext? templateContext = null,
        CancellationToken cancellationToken = default,
        ReportOutputFormat outputFormat = ReportOutputFormat.Excel)
    {
        throw new NotSupportedException("Checador.Api expone solo endpoints read-only. Generacion de reportes no habilitada.");
    }
}
