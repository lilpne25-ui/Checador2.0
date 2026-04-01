namespace Checador.Infrastructure.Excel;

public sealed class ExcelReportOptions
{
    public string DefaultFileNamePattern { get; set; } = "{Empleado}_{Semana}.xlsx";
    public int DataStartRow { get; set; } = 2;
    public bool ForceWriteHeaders { get; set; } = false;
}
