using ClosedXML.Excel;

namespace Checador.Desktop;

internal sealed class OvertimeWorkOrderFormatGenerator
{
    public void Generate(string outputPath, OvertimeWorkOrderFormatData data)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("La ruta de salida del formato OT es obligatoria.");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Formato OT");

        ws.Style.Font.FontName = "Segoe UI";
        ws.Style.Font.FontSize = 10.5;

        ConfigureColumns(ws);
        ConfigurePage(ws);
        FillContent(ws, data);

        workbook.SaveAs(outputPath);
    }

    private static void ConfigureColumns(IXLWorksheet ws)
    {
        ws.Column("A").Width = 20;
        ws.Column("B").Width = 14;
        ws.Column("C").Width = 14;
        ws.Column("D").Width = 14;
        ws.Column("E").Width = 14;
        ws.Column("F").Width = 14;
        ws.Column("G").Width = 14;
        ws.Column("H").Width = 18;
    }

    private static void ConfigurePage(IXLWorksheet ws)
    {
        ws.PageSetup.PagesWide = 1;
        ws.PageSetup.PagesTall = 1;
        ws.PageSetup.CenterHorizontally = true;
        ws.PageSetup.Margins.Top = 0.35;
        ws.PageSetup.Margins.Bottom = 0.35;
        ws.PageSetup.Margins.Left = 0.3;
        ws.PageSetup.Margins.Right = 0.3;
    }

    private static void FillContent(IXLWorksheet ws, OvertimeWorkOrderFormatData data)
    {
        ws.Range("A1:H1").Merge().Value = "SOLICITUD Y APROBACION DE HORAS EXTRA";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range("A2:H2").Merge().Value =
            "Formato OT para validación previa por jefe inmediato y empleado";
        ws.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#4B5563");
        ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell("A4").Value = "Fecha de solicitud:";
        ws.Range("B4:C4").Merge().Value = data.RequestDate.ToString("dd/MM/yyyy");
        ws.Cell("E4").Value = "Orden de trabajo (OT):";
        ws.Range("F4:H4").Merge().Value = data.WorkOrderNumber;

        ws.Cell("A6").Value = "No. empleado:";
        ws.Range("B6:C6").Merge().Value = data.UserId;
        ws.Cell("E6").Value = "Empleado:";
        ws.Range("F6:H6").Merge().Value = data.EmployeeName;

        ws.Cell("A8").Value = "Trabajo a realizar:";
        ws.Range("B8:H10").Merge().Value = data.WorkDescription;
        ws.Range("B8:H10").Style.Alignment.WrapText = true;
        ws.Range("B8:H10").Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        ws.Cell("A12").Value = "Periodo solicitado:";
        ws.Range("B12:D12").Merge().Value = $"{data.FromDate:dd/MM/yyyy} al {data.ToDate:dd/MM/yyyy}";
        ws.Cell("E12").Value = "Días:";
        ws.Range("F12:H12").Merge().Value = data.WorkingDays;

        ws.Cell("A14").Value = "Horario planificado:";
        ws.Range("B14:H14").Merge().Value = data.WorkSchedule;

        ws.Cell("A16").Value = "Jefe inmediato:";
        ws.Range("B16:D16").Merge().Value = data.ImmediateBossName;
        ws.Cell("E16").Value = "Firma jefe inmediato (texto):";
        ws.Range("F16:H16").Merge().Value = data.ImmediateBossSignatureName;

        ws.Cell("A18").Value = "Firma empleado (texto):";
        ws.Range("B18:D18").Merge().Value = data.EmployeeSignatureName;
        ws.Cell("E18").Value = "Observaciones:";
        ws.Range("F18:H20").Merge().Value = data.Notes ?? string.Empty;
        ws.Range("F18:H20").Style.Alignment.WrapText = true;
        ws.Range("F18:H20").Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        ws.Range("A22:H22").Merge().Value =
            "Declaro que la OT indicada requiere labor extraordinaria en los días y horario señalados.";
        ws.Cell("A22").Style.Font.Italic = true;
        ws.Cell("A22").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range("B24:D24").Merge().Style.Border.TopBorder = XLBorderStyleValues.Thin;
        ws.Range("F24:H24").Merge().Style.Border.TopBorder = XLBorderStyleValues.Thin;
        ws.Range("B25:D25").Merge().Value = "Nombre y firma del jefe inmediato";
        ws.Range("F25:H25").Merge().Value = "Nombre y firma del empleado";
        ws.Range("B25:D25").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range("F25:H25").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range("A4:H20").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range("A4:H20").Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        foreach (var label in new[] { "A4", "E4", "A6", "E6", "A8", "A12", "E12", "A14", "A16", "E16", "A18", "E18" })
        {
            ws.Cell(label).Style.Font.Bold = true;
        }
    }
}

internal sealed record OvertimeWorkOrderFormatData(
    int UserId,
    string EmployeeName,
    string WorkOrderNumber,
    string WorkDescription,
    DateTime RequestDate,
    DateTime FromDate,
    DateTime ToDate,
    string WorkingDays,
    string WorkSchedule,
    string ImmediateBossName,
    string ImmediateBossSignatureName,
    string EmployeeSignatureName,
    string? Notes);
