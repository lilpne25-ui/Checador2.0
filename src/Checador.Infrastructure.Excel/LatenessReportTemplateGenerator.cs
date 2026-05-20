using ClosedXML.Excel;
using System.Globalization;
using System.Text;

namespace Checador.Infrastructure.Excel;

public sealed class LatenessReportTemplateGenerator
{
    private const int FirstTemplateDataRow = 10;
    private const int LastTemplateDataRow = 26;

    public void Generate(
        string outputPath,
        DateTime fromDate,
        DateTime toDate,
        IReadOnlyList<LatenessTemplateEmployeeReport> employeeReports,
        string templatePath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("La ruta de salida del reporte de retardos es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
        {
            throw new InvalidOperationException($"No se encontro la plantilla de retardos: '{templatePath}'.");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var workbookLease = TemplateWorkbookLoader.Open(templatePath);
        var workbook = workbookLease.Workbook;
        var templateSheet = ResolveTemplateSheet(workbook);
        if (templateSheet is null)
        {
            throw new InvalidOperationException("La plantilla configurada no contiene la hoja 'Retardos'.");
        }

        var reports = employeeReports.Count == 0
            ? new[]
            {
                new LatenessTemplateEmployeeReport(
                    null,
                    "Sin empleado",
                    null,
                    null,
                    "Diurna",
                    null,
                    null,
                    null,
                    Array.Empty<LatenessTemplateDayRow>(),
                    "Sin incidencias en el periodo seleccionado.")
            }
            : employeeReports;

        for (var i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            var sheet = i == 0
                ? templateSheet
                : templateSheet.CopyTo(CreateUniqueSheetName(workbook, report.UserId, report.EmployeeName));

            PopulateSheet(sheet, fromDate.Date, toDate.Date, report);
        }

        workbook.SaveAs(outputPath);
    }

    private static IXLWorksheet? ResolveTemplateSheet(XLWorkbook workbook)
    {
        if (workbook.TryGetWorksheet("Retardos", out var byName))
        {
            return byName;
        }

        return workbook.Worksheets.FirstOrDefault(ws => Normalize(ws.Name).Contains("retardos", StringComparison.Ordinal));
    }

    private static void PopulateSheet(
        IXLWorksheet ws,
        DateTime fromDate,
        DateTime toDate,
        LatenessTemplateEmployeeReport report)
    {
        WriteText(ws, "B4", report.EmployeeName);
        WriteText(ws, "D4", report.UserId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        WriteText(ws, "F4", report.Department ?? string.Empty);
        WriteText(ws, "H4", report.Position ?? string.Empty);

        WriteDate(ws, "B5", fromDate);
        WriteDate(ws, "D5", toDate);
        WriteText(ws, "F5", string.IsNullOrWhiteSpace(report.WorkShiftType) ? "Diurna" : report.WorkShiftType);
        WriteText(ws, "H5", report.SupervisorName ?? string.Empty);

        if (report.ScheduledStartTime.HasValue)
        {
            WriteTime(ws, "B6", report.ScheduledStartTime.Value);
        }

        if (report.LateToleranceMinutes.HasValue && report.LateToleranceMinutes.Value >= 0)
        {
            WriteText(ws, "D6", report.LateToleranceMinutes.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(report.Notes))
        {
            WriteText(ws, "A28", report.Notes);
        }

        var dayMap = report.Days
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.First());

        var current = DateOnly.FromDateTime(fromDate.Date);
        var end = DateOnly.FromDateTime(toDate.Date);

        for (var row = FirstTemplateDataRow; row <= LastTemplateDataRow; row++)
        {
            if (current > end)
            {
                ClearIfNoFormula(ws.Cell(row, "A"));
                ClearIfNoFormula(ws.Cell(row, "B"));
                ClearIfNoFormula(ws.Cell(row, "D"));
                ClearIfNoFormula(ws.Cell(row, "I"));
                ClearIfNoFormula(ws.Cell(row, "J"));
                ClearIfNoFormula(ws.Cell(row, "K"));
                ClearIfNoFormula(ws.Cell(row, "L"));
                ClearIfNoFormula(ws.Cell(row, "M"));
                ClearIfNoFormula(ws.Cell(row, "N"));
                continue;
            }

            WriteDateIfNoFormula(ws.Cell(row, "A"), current.ToDateTime(TimeOnly.MinValue));
            WriteTextIfNoFormula(ws.Cell(row, "B"), CultureInfo.GetCultureInfo("es-MX").TextInfo.ToTitleCase(GetSpanishDayName(current.DayOfWeek)));

            if (dayMap.TryGetValue(current, out var day))
            {
                if (day.ActualCheckIn.HasValue)
                {
                    WriteTimeIfNoFormula(ws.Cell(row, "D"), day.ActualCheckIn.Value.TimeOfDay);
                }
                else
                {
                    ClearIfNoFormula(ws.Cell(row, "D"));
                }

                WriteTextIfNoFormula(ws.Cell(row, "I"), day.Justification ?? string.Empty);
                WriteTextIfNoFormula(ws.Cell(row, "J"), day.AuthorizedBy ?? string.Empty);
                WriteTextIfNoFormula(ws.Cell(row, "K"), day.ActionTaken ?? string.Empty);
                WriteTextIfNoFormula(ws.Cell(row, "L"), day.Observations ?? string.Empty);
                WriteTextIfNoFormula(ws.Cell(row, "M"), report.EmployeeName);
                WriteTextIfNoFormula(ws.Cell(row, "N"), report.SupervisorName ?? string.Empty);
            }
            else
            {
                ClearIfNoFormula(ws.Cell(row, "D"));
                ClearIfNoFormula(ws.Cell(row, "I"));
                ClearIfNoFormula(ws.Cell(row, "J"));
                ClearIfNoFormula(ws.Cell(row, "K"));
                ClearIfNoFormula(ws.Cell(row, "L"));
                ClearIfNoFormula(ws.Cell(row, "M"));
                ClearIfNoFormula(ws.Cell(row, "N"));
            }

            current = current.AddDays(1);
        }
    }

    private static void WriteDate(IXLWorksheet ws, string address, DateTime value)
    {
        var cell = ws.Cell(address);
        if (HasFormula(cell))
        {
            return;
        }

        cell.Value = value;
        cell.Style.DateFormat.Format = "dd/MM/yyyy";
    }

    private static void WriteTime(IXLWorksheet ws, string address, TimeSpan value)
    {
        var cell = ws.Cell(address);
        if (HasFormula(cell))
        {
            return;
        }

        cell.Value = value;
        cell.Style.DateFormat.Format = "hh:mm";
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

    private static void WriteTimeIfNoFormula(IXLCell cell, TimeSpan value)
    {
        if (HasFormula(cell))
        {
            return;
        }

        cell.Value = value;
        cell.Style.DateFormat.Format = "hh:mm";
    }

    private static void WriteTextIfNoFormula(IXLCell cell, string value)
    {
        if (!HasFormula(cell))
        {
            cell.Value = value;
        }
    }

    private static void ClearIfNoFormula(IXLCell cell)
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

    private static string CreateUniqueSheetName(XLWorkbook workbook, int? userId, string employeeName)
    {
        var baseName = userId.HasValue && userId.Value > 0
            ? $"Retardos_{userId.Value}"
            : $"Retardos_{employeeName}";

        var normalized = new string(baseName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Retardos";
        }

        if (normalized.Length > 31)
        {
            normalized = normalized[..31];
        }

        var candidate = normalized;
        var suffix = 1;
        while (workbook.Worksheets.Any(ws => string.Equals(ws.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            var suffixText = $"_{suffix}";
            var maxBaseLength = Math.Max(1, 31 - suffixText.Length);
            candidate = normalized.Length > maxBaseLength
                ? normalized[..maxBaseLength] + suffixText
                : normalized + suffixText;
            suffix++;
        }

        return candidate;
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
}

public sealed record LatenessTemplateEmployeeReport(
    int? UserId,
    string EmployeeName,
    string? Department,
    string? Position,
    string? WorkShiftType,
    string? SupervisorName,
    TimeSpan? ScheduledStartTime,
    int? LateToleranceMinutes,
    IReadOnlyList<LatenessTemplateDayRow> Days,
    string? Notes);

public sealed record LatenessTemplateDayRow(
    DateOnly Date,
    DateTime? ActualCheckIn,
    int DelayMinutes,
    int ToleranceMinutes,
    string? Justification,
    string? AuthorizedBy,
    string? ActionTaken,
    string? Observations);
