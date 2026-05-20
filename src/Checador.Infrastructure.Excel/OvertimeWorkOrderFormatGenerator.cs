using Checador.Domain.Models;
using ClosedXML.Excel;
using System.Globalization;
using System.Text;

namespace Checador.Infrastructure.Excel;

public sealed class OvertimeWorkOrderFormatGenerator
{
    private const int FirstTemplateDataRow = 10;
    private const int LastTemplateDataRow = 26;

    public void Generate(
        string outputPath,
        OvertimeWorkOrderFormatData data,
        IReadOnlyList<AttendanceDayResult>? attendanceDays = null,
        string? templatePath = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("La ruta de salida del formato de horas extras es obligatoria.");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (!string.IsNullOrWhiteSpace(templatePath))
        {
            if (!File.Exists(templatePath))
            {
                throw new InvalidOperationException($"No se encontro la plantilla de horas extra: '{templatePath}'.");
            }

            GenerateFromTemplate(outputPath, data, attendanceDays ?? Array.Empty<AttendanceDayResult>(), templatePath);
            return;
        }

        GenerateLegacyFallback(outputPath, data);
    }

    private static void GenerateFromTemplate(
        string outputPath,
        OvertimeWorkOrderFormatData data,
        IReadOnlyList<AttendanceDayResult> attendanceDays,
        string templatePath)
    {
        using var workbookLease = TemplateWorkbookLoader.Open(templatePath);
        var workbook = workbookLease.Workbook;
        var ws = ResolveTemplateSheet(workbook);
        if (ws is null)
        {
            throw new InvalidOperationException("La plantilla configurada no contiene la hoja 'Horas Extra'.");
        }

        PopulateHeader(ws, data);
        PopulateDailyRows(ws, data, attendanceDays);

        workbook.SaveAs(outputPath);
    }

    private static IXLWorksheet? ResolveTemplateSheet(XLWorkbook workbook)
    {
        if (workbook.TryGetWorksheet("Horas Extra", out var byName))
        {
            return byName;
        }

        return workbook.Worksheets
            .FirstOrDefault(ws => Normalize(ws.Name).Contains("horasextra", StringComparison.Ordinal));
    }

    private static void PopulateHeader(IXLWorksheet ws, OvertimeWorkOrderFormatData data)
    {
        WriteText(ws, "B4", data.EmployeeName);
        WriteNumber(ws, "D4", data.UserId);
        WriteText(ws, "F4", data.Department ?? string.Empty);
        WriteText(ws, "H4", data.Position ?? string.Empty);

        WriteDate(ws, "B5", data.FromDate.Date);
        WriteDate(ws, "D5", data.ToDate.Date);
        WriteText(ws, "F5", string.IsNullOrWhiteSpace(data.WorkShiftType) ? "Diurna" : data.WorkShiftType);
        WriteText(ws, "H5", data.ImmediateBossName);

        var standardHours = data.StandardShiftHours is > 0m
            ? data.StandardShiftHours.Value
            : 8m;
        WriteNumber(ws, "B6", standardHours);
        WriteText(ws, "H6", data.WorkOrderNumber);

        var summaryText = string.IsNullOrWhiteSpace(data.Notes)
            ? data.WorkDescription
            : $"{data.WorkDescription} | {data.Notes}";
        WriteText(ws, "A28", summaryText);
    }

    private static void PopulateDailyRows(
        IXLWorksheet ws,
        OvertimeWorkOrderFormatData data,
        IReadOnlyList<AttendanceDayResult> attendanceDays)
    {
        var byDate = attendanceDays
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.First());

        var start = DateOnly.FromDateTime(data.FromDate.Date);
        var end = DateOnly.FromDateTime(data.ToDate.Date);
        var current = start;

        for (var row = FirstTemplateDataRow; row <= LastTemplateDataRow; row++)
        {
            if (current > end)
            {
                ClearTextIfNoFormula(ws.Cell(row, "A"));
                ClearTextIfNoFormula(ws.Cell(row, "B"));
                ClearTextIfNoFormula(ws.Cell(row, "C"));
                ClearTextIfNoFormula(ws.Cell(row, "D"));
                ClearTextIfNoFormula(ws.Cell(row, "E"));
                ClearTextIfNoFormula(ws.Cell(row, "F"));
                ClearTextIfNoFormula(ws.Cell(row, "L"));
                ClearTextIfNoFormula(ws.Cell(row, "M"));
                ClearTextIfNoFormula(ws.Cell(row, "N"));
                continue;
            }

            WriteDateIfNoFormula(ws.Cell(row, "A"), current.ToDateTime(TimeOnly.MinValue));
            WriteTextIfNoFormula(ws.Cell(row, "B"), CultureInfo.GetCultureInfo("es-MX").TextInfo.ToTitleCase(GetSpanishDayName(current.DayOfWeek)));

            if (byDate.TryGetValue(current, out var day) && !string.Equals(day.Estatus, "SIN_REGISTRO", StringComparison.OrdinalIgnoreCase))
            {
                WriteTimeIfNoFormula(ws.Cell(row, "C"), day.Entrada);
                WriteTimeIfNoFormula(ws.Cell(row, "D"), day.SalidaComida);
                WriteTimeIfNoFormula(ws.Cell(row, "E"), day.EntradaComida);
                WriteTimeIfNoFormula(ws.Cell(row, "F"), day.Salida);
                WriteTextIfNoFormula(ws.Cell(row, "L"), data.WorkDescription);
                WriteTextIfNoFormula(ws.Cell(row, "M"), data.ImmediateBossName);
                WriteTextIfNoFormula(ws.Cell(row, "N"), data.EmployeeSignatureName);
            }
            else
            {
                ClearTextIfNoFormula(ws.Cell(row, "C"));
                ClearTextIfNoFormula(ws.Cell(row, "D"));
                ClearTextIfNoFormula(ws.Cell(row, "E"));
                ClearTextIfNoFormula(ws.Cell(row, "F"));
                WriteTextIfNoFormula(ws.Cell(row, "L"), "SIN_REGISTRO");
                ClearTextIfNoFormula(ws.Cell(row, "M"));
                ClearTextIfNoFormula(ws.Cell(row, "N"));
            }

            current = current.AddDays(1);
        }
    }

    private static void WriteDate(IXLWorksheet ws, string address, DateTime value)
    {
        var cell = ws.Cell(address);
        if (!HasFormula(cell))
        {
            cell.Value = value;
            cell.Style.DateFormat.Format = "dd/MM/yyyy";
        }
    }

    private static void WriteNumber(IXLWorksheet ws, string address, decimal value)
    {
        var cell = ws.Cell(address);
        if (!HasFormula(cell))
        {
            cell.Value = value;
        }
    }

    private static void WriteText(IXLWorksheet ws, string address, string value)
    {
        var cell = ws.Cell(address);
        if (!HasFormula(cell))
        {
            cell.Value = value;
        }
    }

    private static void WriteDateIfNoFormula(IXLCell cell, DateTime value)
    {
        if (HasFormula(cell))
        {
            return;
        }

        cell.Value = value;
        cell.Style.DateFormat.Format = "dd/MM/yyyy";
    }

    private static void WriteTimeIfNoFormula(IXLCell cell, DateTime? value)
    {
        if (HasFormula(cell))
        {
            return;
        }

        if (!value.HasValue)
        {
            cell.Value = string.Empty;
            return;
        }

        cell.Value = value.Value.TimeOfDay;
        cell.Style.DateFormat.Format = "hh:mm";
    }

    private static void WriteTextIfNoFormula(IXLCell cell, string value)
    {
        if (!HasFormula(cell))
        {
            cell.Value = value;
        }
    }

    private static void ClearTextIfNoFormula(IXLCell cell)
    {
        if (!HasFormula(cell))
        {
            cell.Value = string.Empty;
        }
    }

    private static bool HasFormula(IXLCell cell)
    {
        return !string.IsNullOrWhiteSpace(cell.FormulaA1);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        Span<char> buffer = stackalloc char[normalized.Length];
        var index = 0;
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                buffer[index++] = ch;
            }
        }

        return new string(buffer[..index]);
    }

    private static string GetSpanishDayName(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "lunes",
            DayOfWeek.Tuesday => "martes",
            DayOfWeek.Wednesday => "miercoles",
            DayOfWeek.Thursday => "jueves",
            DayOfWeek.Friday => "viernes",
            DayOfWeek.Saturday => "sabado",
            DayOfWeek.Sunday => "domingo",
            _ => string.Empty
        };
    }

    private static void GenerateLegacyFallback(string outputPath, OvertimeWorkOrderFormatData data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Formato Horas Extras");

        ws.Style.Font.FontName = "Segoe UI";
        ws.Style.Font.FontSize = 10.5;

        ConfigureColumns(ws);
        ConfigurePage(ws);
        FillLegacyContent(ws, data);

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

    private static void FillLegacyContent(IXLWorksheet ws, OvertimeWorkOrderFormatData data)
    {
        ws.Range("A1:H1").Merge().Value = "SOLICITUD Y APROBACION DE HORAS EXTRA";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range("A2:H2").Merge().Value =
            "Formato de horas extras para validacion previa por jefe inmediato y empleado";
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
        ws.Cell("E12").Value = "Dias:";
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
            "Declaro que la OT indicada requiere labor extraordinaria en los dias y horario senalados.";
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

public sealed record OvertimeWorkOrderFormatData(
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
    string? Notes,
    string? Department = null,
    string? Position = null,
    string? WorkShiftType = null,
    decimal? StandardShiftHours = null);
