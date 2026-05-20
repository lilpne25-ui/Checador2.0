namespace Checador.Api.Reports;

public sealed class ApiReportOptions
{
    public string OutputDirectory { get; set; } = "reports";
    public string? TemplatePath { get; set; }
    public string? OvertimeTemplatePath { get; set; }
    public string? LatenessTemplatePath { get; set; }
    public string DefaultFileNamePattern { get; set; } = "{Empleado}_{Semana}.xlsx";
    public int DataStartRow { get; set; } = 2;
    public bool ForceWriteHeaders { get; set; } = false;
}
