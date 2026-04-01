using Checador.Domain.Interfaces;
using Checador.Domain.Models;
using ClosedXML.Excel;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace Checador.Infrastructure.Excel;

public sealed class ClosedXmlReportGenerator : IReportGenerator
{
    private readonly ExcelReportOptions _options;

    public ClosedXmlReportGenerator(ExcelReportOptions options)
    {
        _options = options;
    }

    public Task<string> GenerateWeeklyReportAsync(
        IReadOnlyList<AttendanceDayResult> days,
        string outputDirectory,
        string? templatePath,
        ReportTemplateContext? templateContext = null,
        CancellationToken cancellationToken = default,
        ReportOutputFormat outputFormat = ReportOutputFormat.Excel)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (days.Count == 0)
        {
            throw new InvalidOperationException("No hay registros procesados para generar el reporte.");
        }

        var first = days[0];
        var effectiveEmployeeName = string.IsNullOrWhiteSpace(templateContext?.EmployeeName)
            ? first.EmployeeName
            : templateContext!.EmployeeName!;
        var rangeStart = days.Min(d => d.Date).ToDateTime(TimeOnly.MinValue);
        var rangeEnd = days.Max(d => d.Date).ToDateTime(TimeOnly.MinValue);

        var rangeFolderName = $"{rangeStart:dd-MM-yyyy}_al_{rangeEnd:dd-MM-yyyy}";
        var targetDirectory = Path.Combine(outputDirectory, rangeFolderName);
        Directory.CreateDirectory(targetDirectory);

        var employeeSafe = string.Concat(effectiveEmployeeName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        if (string.IsNullOrWhiteSpace(employeeSafe))
        {
            employeeSafe = $"Empleado_{first.UserId}";
        }

        var excelExtension = ResolveOutputExtension(templatePath);
        var outputExtension = outputFormat == ReportOutputFormat.Pdf
            ? ".pdf"
            : excelExtension;
        var fileName = $"{first.UserId}_{employeeSafe}_{rangeStart:dd-MM-yyyy}_al_{rangeEnd:dd-MM-yyyy}{outputExtension}";

        var fullPath = Path.Combine(targetDirectory, fileName);

        if (outputFormat == ReportOutputFormat.Pdf)
        {
            var tempExcelPath = Path.Combine(
                targetDirectory,
                $"{Path.GetFileNameWithoutExtension(fileName)}__tmp__{Guid.NewGuid():N}{excelExtension}");

            GenerateExcelReportFile(
                days,
                templatePath,
                templateContext,
                effectiveEmployeeName,
                tempExcelPath,
                excelExtension,
                cancellationToken);

            try
            {
                ConvertExcelToPdfViaCom(tempExcelPath, fullPath);
            }
            finally
            {
                TryDeleteFile(tempExcelPath);
            }

            return Task.FromResult(fullPath);
        }

        GenerateExcelReportFile(
            days,
            templatePath,
            templateContext,
            effectiveEmployeeName,
            fullPath,
            outputExtension,
            cancellationToken);

        return Task.FromResult(fullPath);
    }

    private void GenerateExcelReportFile(
        IReadOnlyList<AttendanceDayResult> days,
        string? templatePath,
        ReportTemplateContext? templateContext,
        string effectiveEmployeeName,
        string fullPath,
        string outputExtension,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hasTemplate = !string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath);
        using var workbook = LoadWorkbook(templatePath);
        var ws = workbook.Worksheets.FirstOrDefault() ?? workbook.Worksheets.Add("Asistencia");

        var weekStart = days.Min(d => d.Date);
        var weekEnd = days.Max(d => d.Date);

        var templateMapped = hasTemplate && !_options.ForceWriteHeaders &&
                             TryWriteTemplateLayout(ws, days, effectiveEmployeeName, weekStart, weekEnd, templateContext);

        if (!templateMapped)
        {
            if (!hasTemplate || _options.ForceWriteHeaders)
            {
                WriteHeaders(ws);
            }

            WriteRows(ws, days, _options.DataStartRow);
            WriteTotals(ws, days, _options.DataStartRow);
        }

        workbook.SaveAs(fullPath);

        if (hasTemplate && string.Equals(outputExtension, ".xlsm", StringComparison.OrdinalIgnoreCase))
        {
            PreserveMacroProject(templatePath!, fullPath);
        }
    }

    private static bool TryWriteTemplateLayout(
        IXLWorksheet ws,
        IReadOnlyList<AttendanceDayResult> days,
        string employeeName,
        DateOnly weekStart,
        DateOnly weekEnd,
        ReportTemplateContext? templateContext)
    {
        var used = ws.RangeUsed();
        if (used is null)
        {
            return false;
        }

        WriteEmployeeName(ws, used, employeeName);
        WriteHeaderMetadata(ws, used, templateContext);
        WriteWeekPeriod(ws, used, weekStart, weekEnd);
        WriteDeclarationMetadata(ws, used, employeeName, weekEnd);

        var columns = FindTemplateColumns(used);
        var dayRows = FindDayRows(used, columns);

        if (dayRows.Count == 0 || columns.Count == 0)
        {
            return false;
        }

        var singleByDay = days
            .OrderBy(d => d.Date)
            .GroupBy(d => Normalize(GetSpanishDayName(d.Date.DayOfWeek)))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var dayEntry in singleByDay)
        {
            var dayKey = dayEntry.Key;
            var day = dayEntry.Value;

            if (!dayRows.TryGetValue(dayKey, out var row))
            {
                continue;
            }

            WriteDateIfMapped(ws, row, columns, "fecha", day.Date.ToDateTime(TimeOnly.MinValue), "dd-MM-yyyy");
            var sinReg = day.Estatus == "SIN_REGISTRO";

            WriteTextIfMapped(ws, row, columns, "horaentrada", sinReg ? "SIN REG" : day.Entrada?.ToString("HH:mm") ?? "SIN REG");
            WriteTextIfMapped(ws, row, columns, "salidacomida", sinReg ? "SIN REG" : day.SalidaComida?.ToString("HH:mm") ?? "SIN REG");
            WriteTextIfMapped(ws, row, columns, "entradacomida", sinReg ? "SIN REG" : day.EntradaComida?.ToString("HH:mm") ?? "SIN REG");
            WriteTextIfMapped(ws, row, columns, "horasalida", sinReg ? "SIN REG" : day.Salida?.ToString("HH:mm") ?? "SIN REG");
            WriteTextIfMapped(ws, row, columns, "totalhoras", sinReg ? "SIN REG" : day.HorasTrabajadas?.ToString("0.00") ?? "SIN REG");
            WriteTextIfMapped(ws, row, columns, "horasextra", sinReg ? "SIN REG" : day.HorasExtra?.ToString("0.00") ?? "SIN REG");
        }

        WriteWeeklyTotalsInTemplate(ws, used, days);

        return true;
    }

    private static void WriteWeeklyTotalsInTemplate(IXLWorksheet ws, IXLRange used, IReadOnlyList<AttendanceDayResult> days)
    {
        var totalHorasSemana = days.Where(d => d.HorasTrabajadas.HasValue).Sum(d => d.HorasTrabajadas!.Value);
        var totalExtraSemana = days.Where(d => d.HorasExtra.HasValue).Sum(d => d.HorasExtra!.Value);

        foreach (var cell in used.CellsUsed())
        {
            var text = Normalize(cell.GetString());

            if (text.Contains("totaldehoraslaboradasporsemana") ||
                text.Contains("totaldehoraslaboradasenlasemana") ||
                text.Contains("totalhoraslaboradasenlasemana"))
            {
                var target = GetValueCellToRight(ws, used, cell);
                target.Value = totalHorasSemana.ToString("0.00");
            }
            else if (text.Contains("totaldehorasextra") || text.Contains("totaldehorasextras"))
            {
                var target = GetValueCellToRight(ws, used, cell);
                target.Value = totalExtraSemana.ToString("0.00");
            }
        }
    }

    private static void WriteEmployeeName(IXLWorksheet ws, IXLRange used, string employeeName)
    {
        foreach (var cell in used.CellsUsed())
        {
            var norm = Normalize(cell.GetString());
            if (!norm.Contains("nombredeltrabajador"))
            {
                continue;
            }

            var target = ws.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber + 1);
            target.Value = employeeName;
            return;
        }
    }

    private static void WriteHeaderMetadata(IXLWorksheet ws, IXLRange used, ReportTemplateContext? templateContext)
    {
        if (templateContext is null)
        {
            return;
        }

        WriteValueNextToLabel(ws, used, new[] { "empresa", "razonsocial" }, templateContext.Company);
        WriteValueNextToLabel(ws, used, new[] { "area", "departamento" }, templateContext.Area);
        WriteValueNextToLabel(ws, used, new[] { "puesto", "cargo" }, templateContext.Position);
        WriteValueNextToLabel(ws, used, new[] { "horario" }, templateContext.WorkScheduleDisplay);
        WriteValueNextToLabel(ws, used, new[] { "tipodejornada" }, templateContext.WorkShiftType);
        MarkShiftTypeBestEffort(ws, used, templateContext.WorkShiftType);
    }

    private static void WriteDeclarationMetadata(IXLWorksheet ws, IXLRange used, string employeeName, DateOnly reportDate)
    {
        var signatureNameCell = used.CellsUsed()
            .Where(c =>
            {
                var normalized = Normalize(c.GetString());
                return normalized.Contains("nombreyfirmadeltrabajador") ||
                       normalized.Contains("nombrefirmadeltrabajador");
            })
            .OrderByDescending(c => c.Address.RowNumber)
            .ThenBy(c => c.Address.ColumnNumber)
            .FirstOrDefault();

        if (signatureNameCell is null)
        {
            signatureNameCell = used.CellsUsed()
                .Where(c => Normalize(c.GetString()).Contains("nombredeltrabajador"))
                .OrderByDescending(c => c.Address.RowNumber)
                .ThenBy(c => c.Address.ColumnNumber)
                .FirstOrDefault();
        }

        if (signatureNameCell is not null)
        {
            var raw = signatureNameCell.GetString();
            var nameWritten = TryWriteNameInsideDeclarationLabel(signatureNameCell, raw, employeeName);
            if (!nameWritten)
            {
                nameWritten = TryWriteValueNearDeclarationLabel(ws, used, signatureNameCell, employeeName);
            }

            if (!nameWritten)
            {
                signatureNameCell.Value = BuildLabeledValue(raw, "Nombre y firma del trabajador", employeeName);
                signatureNameCell.Style.Alignment.WrapText = false;
                signatureNameCell.Style.Alignment.ShrinkToFit = true;
            }
        }

        var signatureRow = signatureNameCell?.Address.RowNumber ?? used.RangeAddress.FirstAddress.RowNumber;
        var signatureDateCell = used.CellsUsed()
            .Where(c => c.Address.RowNumber >= signatureRow)
            .FirstOrDefault(c =>
            {
                var raw = c.GetString();
                var norm = Normalize(raw);
                return norm.Contains("fecha") && (raw.Contains("_") || raw.Contains("/") || raw.Contains(":"));
            })
            ?? used.CellsUsed()
                .Where(c => c.Address.RowNumber >= signatureRow)
                .OrderByDescending(c => c.Address.RowNumber)
                .ThenBy(c => c.Address.ColumnNumber)
                .FirstOrDefault(c => Normalize(c.GetString()).Contains("fecha"));

        if (signatureDateCell is not null)
        {
            var rawDateLabel = signatureDateCell.GetString();
            signatureDateCell.Value = BuildLabeledValue(rawDateLabel, "Fecha", reportDate.ToDateTime(TimeOnly.MinValue).ToString("dd-MM-yyyy"));
        }
    }

    private static bool TryWriteValueNearDeclarationLabel(IXLWorksheet ws, IXLRange used, IXLCell labelCell, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var row = labelCell.Address.RowNumber;
        var firstCol = labelCell.Address.ColumnNumber + 1;
        var lastCol = used.RangeAddress.LastAddress.ColumnNumber;

        var merged = ws.MergedRanges.FirstOrDefault(r => r.Contains(labelCell));
        if (merged is not null)
        {
            firstCol = merged.RangeAddress.LastAddress.ColumnNumber + 1;
        }

        if (firstCol <= lastCol)
        {
            for (var col = firstCol; col <= lastCol; col++)
            {
                var cell = ws.Cell(row, col);
                if (!string.IsNullOrWhiteSpace(cell.GetString()))
                {
                    continue;
                }

                cell.Value = value;
                return true;
            }

            // Si no hay espacios vacíos, usamos la última celda de la fila para no modificar la etiqueta.
            ws.Cell(row, lastCol).Value = value;
            return true;
        }

        return false;
    }

    private static bool TryWriteNameInsideDeclarationLabel(IXLCell labelCell, string rawLabel, string employeeName)
    {
        var cleanName = PrepareSignatureName(employeeName, 34);
        if (string.IsNullOrWhiteSpace(cleanName))
        {
            return false;
        }

        var label = ExtractLabelPrefix(rawLabel, "Nombre y firma del trabajador:");
        labelCell.Value = $"{label} {cleanName}";
        labelCell.Style.Alignment.WrapText = false;
        labelCell.Style.Alignment.ShrinkToFit = false;
        return true;
    }

    private static string ExtractLabelPrefix(string raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        var idx = raw.IndexOf(':');
        if (idx < 0)
        {
            return raw.Trim();
        }

        return raw[..(idx + 1)].TrimEnd();
    }

    private static string CollapseWhitespace(string value)
    {
        var sb = new StringBuilder(value.Length);
        var prevSpace = false;

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (prevSpace)
                {
                    continue;
                }

                sb.Append(' ');
                prevSpace = true;
                continue;
            }

            sb.Append(ch);
            prevSpace = false;
        }

        return sb.ToString();
    }

    private static string PrepareSignatureName(string employeeName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(employeeName))
        {
            return string.Empty;
        }

        var cleanName = CollapseWhitespace(employeeName).Trim();
        if (cleanName.Length > maxLength && maxLength > 4)
        {
            cleanName = cleanName[..(maxLength - 3)] + "...";
        }

        return cleanName;
    }

    private static void WriteValueNextToLabel(IXLWorksheet ws, IXLRange used, IReadOnlyList<string> labelTokens, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var cell in used.CellsUsed())
        {
            var norm = Normalize(cell.GetString());
            if (labelTokens.All(token => !norm.Contains(Normalize(token))))
            {
                continue;
            }

            var target = FindBestValueCellOnRow(ws, used, cell, value);
            target.Value = value;
            return;
        }
    }

    private static void MarkShiftTypeBestEffort(IXLWorksheet ws, IXLRange used, string? workShiftType)
    {
        if (string.IsNullOrWhiteSpace(workShiftType))
        {
            return;
        }

        var targetToken = workShiftType.Equals("Nocturna", StringComparison.OrdinalIgnoreCase)
            ? "nocturna"
            : "diurna";

        foreach (var cell in used.CellsUsed())
        {
            var text = cell.GetString();
            var norm = Normalize(text);
            if (!norm.Contains(targetToken))
            {
                continue;
            }

            if (text.Contains("(") && text.Contains(")"))
            {
                ws.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Value = ReplaceMarkerWithX(text);
                return;
            }

            var rightCell = ws.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber + 1);
            if (string.IsNullOrWhiteSpace(rightCell.GetString()))
            {
                rightCell.Value = "X";
                return;
            }
        }
    }

    private static void WriteWeekPeriod(IXLWorksheet ws, IXLRange used, DateOnly weekStart, DateOnly weekEnd)
    {
        foreach (var row in used.Rows())
        {
            var hasPeriodLabel = row.Cells().Any(c => Normalize(c.GetString()).Contains("periododelasemana"));
            if (!hasPeriodLabel)
            {
                continue;
            }

            var placeholders = row.Cells()
                .Where(c => Normalize(c.GetString()).Contains("ddmmaa"))
                .ToList();

            if (placeholders.Count >= 2)
            {
                placeholders[0].Value = weekStart.ToDateTime(TimeOnly.MinValue).ToString("dd-MM-yyyy");
                placeholders[1].Value = weekEnd.ToDateTime(TimeOnly.MinValue).ToString("dd-MM-yyyy");
                return;
            }

            var delCell = row.Cells().FirstOrDefault(c => Normalize(c.GetString()) == "del");
            var alCell = row.Cells().FirstOrDefault(c => Normalize(c.GetString()) == "al");

            if (delCell is not null)
            {
                ws.Cell(delCell.Address.RowNumber, delCell.Address.ColumnNumber + 1).Value =
                    weekStart.ToDateTime(TimeOnly.MinValue).ToString("dd-MM-yyyy");
            }

            if (alCell is not null)
            {
                ws.Cell(alCell.Address.RowNumber, alCell.Address.ColumnNumber + 1).Value =
                    weekEnd.ToDateTime(TimeOnly.MinValue).ToString("dd-MM-yyyy");
            }

            return;
        }
    }

    private static Dictionary<string, int> FindTemplateColumns(IXLRange used)
    {
        var columns = new Dictionary<string, int>();

        var dayHeaderCell = used.CellsUsed().FirstOrDefault(c => Normalize(c.GetString()) == "dia");
        if (dayHeaderCell is null)
        {
            return columns;
        }

        var headerRow = dayHeaderCell.Address.RowNumber;
        var rowCells = dayHeaderCell.Worksheet
            .Row(headerRow)
            .Cells(used.RangeAddress.FirstAddress.ColumnNumber, used.RangeAddress.LastAddress.ColumnNumber);

        foreach (var cell in rowCells)
        {
            var header = Normalize(cell.GetString());
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            if (header == "fecha") columns["fecha"] = cell.Address.ColumnNumber;
            else if (header == "dia") columns["dia"] = cell.Address.ColumnNumber;
            else if (header.Contains("horadeentrada")) columns["horaentrada"] = cell.Address.ColumnNumber;
            else if (header.Contains("salidaacomida")) columns["salidacomida"] = cell.Address.ColumnNumber;
            else if (header.Contains("entradadecomida")) columns["entradacomida"] = cell.Address.ColumnNumber;
            else if (header.Contains("horadesalida")) columns["horasalida"] = cell.Address.ColumnNumber;
            else if (header.Contains("totaldehoras")) columns["totalhoras"] = cell.Address.ColumnNumber;
            else if (header.Contains("horasextra")) columns["horasextra"] = cell.Address.ColumnNumber;
        }

        return columns;
    }

    private static Dictionary<string, int> FindDayRows(IXLRange used, Dictionary<string, int> columns)
    {
        var dayRows = new Dictionary<string, int>();
        if (!columns.TryGetValue("dia", out var dayColumn))
        {
            return dayRows;
        }

        for (var row = used.RangeAddress.FirstAddress.RowNumber; row <= used.RangeAddress.LastAddress.RowNumber; row++)
        {
            var text = Normalize(used.Worksheet.Cell(row, dayColumn).GetString());
            if (!TryResolveTemplateDayKey(text, out var dayKey))
            {
                continue;
            }

            if (!dayRows.ContainsKey(dayKey))
            {
                dayRows[dayKey] = row;
            }
        }

        return dayRows;
    }

    private static bool TryResolveTemplateDayKey(string normalizedText, out string dayKey)
    {
        dayKey = string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        static bool Matches(string value, string token)
            => value.Equals(token, StringComparison.Ordinal)
               || value.StartsWith(token, StringComparison.Ordinal);

        if (Matches(normalizedText, "lunes") || Matches(normalizedText, "lun"))
        {
            dayKey = "lunes";
            return true;
        }

        if (Matches(normalizedText, "martes") || Matches(normalizedText, "mar"))
        {
            dayKey = "martes";
            return true;
        }

        if (Matches(normalizedText, "miercoles") || Matches(normalizedText, "mie"))
        {
            dayKey = "miercoles";
            return true;
        }

        if (Matches(normalizedText, "jueves") || Matches(normalizedText, "jue"))
        {
            dayKey = "jueves";
            return true;
        }

        if (Matches(normalizedText, "viernes") || Matches(normalizedText, "vie"))
        {
            dayKey = "viernes";
            return true;
        }

        if (Matches(normalizedText, "sabado") || Matches(normalizedText, "sab"))
        {
            dayKey = "sabado";
            return true;
        }

        if (Matches(normalizedText, "domingo") || Matches(normalizedText, "dom"))
        {
            dayKey = "domingo";
            return true;
        }

        return false;
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

    private static void WriteDateIfMapped(IXLWorksheet ws, int row, Dictionary<string, int> cols, string key, DateTime value, string format)
    {
        if (!cols.TryGetValue(key, out var col)) return;
        ws.Cell(row, col).Value = value;
        ws.Cell(row, col).Style.DateFormat.Format = format;
    }

    private static void WriteTextIfMapped(IXLWorksheet ws, int row, Dictionary<string, int> cols, string key, string value)
    {
        if (!cols.TryGetValue(key, out var col)) return;
        ws.Cell(row, col).Value = value;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string ResolveOutputExtension(string? templatePath)
    {
        var extension = Path.GetExtension(templatePath ?? string.Empty);
        return extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase)
            ? ".xlsm"
            : ".xlsx";
    }

    private static void PreserveMacroProject(string templatePath, string outputPath)
    {
        using var templateArchive = ZipFile.OpenRead(templatePath);
        using var outputArchive = ZipFile.Open(outputPath, ZipArchiveMode.Update);

        var copiedMacroProject = CopyZipEntry(templateArchive, outputArchive, "xl/vbaProject.bin");
        var copiedMacroSignature = CopyZipEntry(templateArchive, outputArchive, "xl/vbaProjectSignature.bin");

        EnsureContentTypeOverride(
            outputArchive,
            "/xl/workbook.xml",
            "application/vnd.ms-excel.sheet.macroEnabled.main+xml");

        if (!copiedMacroProject)
        {
            return;
        }

        EnsureWorkbookMacroRelationship(templateArchive, outputArchive, "vbaProject");
        EnsureContentTypeOverride(
            outputArchive,
            "/xl/vbaProject.bin",
            "application/vnd.ms-office.vbaProject");

        if (copiedMacroSignature)
        {
            EnsureWorkbookMacroRelationship(templateArchive, outputArchive, "vbaProjectSignature");
            EnsureContentTypeOverride(
                outputArchive,
                "/xl/vbaProjectSignature.bin",
                "application/vnd.ms-office.vbaProjectSignature");
        }
    }

    private static bool CopyZipEntry(ZipArchive sourceArchive, ZipArchive targetArchive, string entryPath)
    {
        var sourceEntry = sourceArchive.GetEntry(entryPath);
        if (sourceEntry is null)
        {
            return false;
        }

        targetArchive.GetEntry(entryPath)?.Delete();
        var targetEntry = targetArchive.CreateEntry(entryPath, CompressionLevel.Optimal);

        using var sourceStream = sourceEntry.Open();
        using var targetStream = targetEntry.Open();
        sourceStream.CopyTo(targetStream);
        return true;
    }

    private static void EnsureWorkbookMacroRelationship(
        ZipArchive templateArchive,
        ZipArchive outputArchive,
        string relationshipTypeName)
    {
        const string workbookRelsPath = "xl/_rels/workbook.xml.rels";
        var outputRels = ReadXmlDocument(outputArchive, workbookRelsPath);
        if (outputRels?.Root is null)
        {
            return;
        }

        var suffix = "/" + relationshipTypeName;
        var relNs = outputRels.Root.Name.Namespace;

        var alreadyExists = outputRels.Root
            .Elements(relNs + "Relationship")
            .Any(r => (string?)r.Attribute("Type") is string type &&
                      type.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            return;
        }

        var templateRels = ReadXmlDocument(templateArchive, workbookRelsPath);
        var templateRel = templateRels?.Root?
            .Elements(templateRels.Root.Name.Namespace + "Relationship")
            .FirstOrDefault(r => ((string?)r.Attribute("Type"))?.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) == true);

        var type = (string?)templateRel?.Attribute("Type") ?? relationshipTypeName switch
        {
            "vbaProject" => "http://schemas.microsoft.com/office/2006/relationships/vbaProject",
            "vbaProjectSignature" => "http://schemas.microsoft.com/office/2006/relationships/vbaProjectSignature",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(type))
        {
            return;
        }

        var target = (string?)templateRel?.Attribute("Target") ?? relationshipTypeName switch
        {
            "vbaProject" => "vbaProject.bin",
            "vbaProjectSignature" => "vbaProjectSignature.bin",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var nextId = GetNextRelationshipId(outputRels.Root, relNs);
        outputRels.Root.Add(new XElement(
            relNs + "Relationship",
            new XAttribute("Id", nextId),
            new XAttribute("Type", type),
            new XAttribute("Target", target)));

        WriteXmlDocument(outputArchive, workbookRelsPath, outputRels);
    }

    private static string GetNextRelationshipId(XElement root, XNamespace relNs)
    {
        var used = new HashSet<int>();
        foreach (var rel in root.Elements(relNs + "Relationship"))
        {
            var id = (string?)rel.Attribute("Id");
            if (id is null || !id.StartsWith("rId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(id[3..], out var n))
            {
                used.Add(n);
            }
        }

        var next = 1;
        while (used.Contains(next))
        {
            next++;
        }

        return $"rId{next}";
    }

    private static void EnsureContentTypeOverride(ZipArchive archive, string partName, string contentType)
    {
        const string contentTypesPath = "[Content_Types].xml";
        var contentTypes = ReadXmlDocument(archive, contentTypesPath);
        if (contentTypes?.Root is null)
        {
            return;
        }

        var ctNs = contentTypes.Root.Name.Namespace;
        var overrideNode = contentTypes.Root
            .Elements(ctNs + "Override")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("PartName"), partName, StringComparison.OrdinalIgnoreCase));

        if (overrideNode is null)
        {
            contentTypes.Root.Add(new XElement(
                ctNs + "Override",
                new XAttribute("PartName", partName),
                new XAttribute("ContentType", contentType)));
        }
        else
        {
            overrideNode.SetAttributeValue("ContentType", contentType);
        }

        WriteXmlDocument(archive, contentTypesPath, contentTypes);
    }

    private static XDocument? ReadXmlDocument(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static void WriteXmlDocument(ZipArchive archive, string entryPath, XDocument document)
    {
        archive.GetEntry(entryPath)?.Delete();
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var stream = entry.Open();
        document.Save(stream);
    }

    private static XLWorkbook LoadWorkbook(string? templatePath)
    {
        if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
        {
            try
            {
                using var fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var temp = new MemoryStream();
                fs.CopyTo(temp);
                temp.Position = 0;

                var workbookStream = new MemoryStream(temp.ToArray());
                return new XLWorkbook(workbookStream);
            }
            catch (IOException ex)
            {
                throw new IOException(
                    $"No se pudo leer la plantilla '{templatePath}'. Cierra el archivo en Excel u otro proceso e inténtalo de nuevo.",
                    ex);
            }
        }

        return new XLWorkbook();
    }

    private static void WriteHeaders(IXLWorksheet ws)
    {
        ws.Cell("A1").Value = "Fecha";
        ws.Cell("B1").Value = "Entrada";
        ws.Cell("C1").Value = "Salida comida";
        ws.Cell("D1").Value = "Entrada comida";
        ws.Cell("E1").Value = "Salida";
        ws.Cell("F1").Value = "Horas trabajadas";
        ws.Cell("G1").Value = "Horas extra";
        ws.Cell("H1").Value = "Estatus";
        ws.Cell("I1").Value = "Observaciones";

        ws.Range("A1:I1").Style.Font.Bold = true;
        ws.Columns("A:I").AdjustToContents();
    }

    private static void WriteRows(IXLWorksheet ws, IReadOnlyList<AttendanceDayResult> days, int startRow)
    {
        var row = Math.Max(1, startRow);
        foreach (var day in days.OrderBy(d => d.Date))
        {
            ws.Cell(row, 1).Value = day.Date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, 1).Style.DateFormat.Format = "dd-MM-yyyy";

            ws.Cell(row, 2).Value = day.Entrada?.ToString("HH:mm") ?? "";
            ws.Cell(row, 3).Value = day.SalidaComida?.ToString("HH:mm") ?? "";
            ws.Cell(row, 4).Value = day.EntradaComida?.ToString("HH:mm") ?? "";
            ws.Cell(row, 5).Value = day.Salida?.ToString("HH:mm") ?? "";
            ws.Cell(row, 6).Value = day.HorasTrabajadas;
            ws.Cell(row, 7).Value = day.HorasExtra;
            ws.Cell(row, 8).Value = day.Estatus;
            ws.Cell(row, 9).Value = day.Observaciones ?? "";

            row++;
        }
    }

    private static void WriteTotals(IXLWorksheet ws, IReadOnlyList<AttendanceDayResult> days, int dataStartRow)
    {
        var startRow = Math.Max(1, dataStartRow);
        var totalRow = startRow + days.Count + 1;
        var totalHoras = days.Where(d => d.HorasTrabajadas.HasValue).Sum(d => d.HorasTrabajadas!.Value);
        var totalExtra = days.Where(d => d.HorasExtra.HasValue).Sum(d => d.HorasExtra!.Value);

        ws.Cell(totalRow, 5).Value = "Totales";
        ws.Cell(totalRow, 6).Value = totalHoras;
        ws.Cell(totalRow, 7).Value = totalExtra;

        ws.Columns("A:I").AdjustToContents();
    }

    private static IXLCell GetValueCellToRight(IXLWorksheet ws, IXLRange used, IXLCell labelCell)
    {
        var row = labelCell.Address.RowNumber;
        var col = labelCell.Address.ColumnNumber;

        var merged = ws.MergedRanges.FirstOrDefault(r => r.Contains(labelCell));
        if (merged is not null)
        {
            col = merged.RangeAddress.LastAddress.ColumnNumber;
        }

        var targetCol = Math.Min(col + 1, used.RangeAddress.LastAddress.ColumnNumber);
        var target = ws.Cell(row, targetCol);

        if (!string.IsNullOrWhiteSpace(target.GetString()) && targetCol < used.RangeAddress.LastAddress.ColumnNumber)
        {
            target = ws.Cell(row, targetCol + 1);
        }

        return target;
    }

    private static IXLCell FindBestValueCellOnRow(IXLWorksheet ws, IXLRange used, IXLCell labelCell, string value)
    {
        var row = labelCell.Address.RowNumber;
        var firstCol = labelCell.Address.ColumnNumber + 1;
        var lastCol = used.RangeAddress.LastAddress.ColumnNumber;

        if (firstCol > lastCol)
        {
            return labelCell;
        }

        var valueNorm = Normalize(value);
        var prefersHorario = valueNorm.Contains("hrs") || valueNorm.Contains("a");

        if (prefersHorario)
        {
            for (var col = firstCol; col <= lastCol; col++)
            {
                var c = ws.Cell(row, col);
                var t = Normalize(c.GetString());
                if (t.Contains("hrs") || t.Contains("0000a0000hrs") || t.Contains("0000"))
                {
                    return c;
                }
            }
        }

        for (var col = firstCol; col <= lastCol; col++)
        {
            var c = ws.Cell(row, col);
            if (string.IsNullOrWhiteSpace(c.GetString()))
            {
                return c;
            }
        }

        return ws.Cell(row, firstCol);
    }

    private static string ReplaceMarkerWithX(string text)
    {
        var start = text.IndexOf('(');
        var end = text.IndexOf(')', start + 1);
        if (start < 0 || end <= start)
        {
            return text;
        }

        return text[..(start + 1)] + "X" + text[end..];
    }

    private static string BuildLabeledValue(string sourceText, string fallbackLabel, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return sourceText;
        }

        var colonIndex = sourceText.IndexOf(':');
        if (colonIndex >= 0)
        {
            var label = sourceText[..(colonIndex + 1)].TrimEnd();
            return $"{label} {value}";
        }

        return $"{fallbackLabel}: {value}";
    }

    private static void ConvertExcelToPdfViaCom(string excelPath, string pdfPath)
    {
        var excelType = Type.GetTypeFromProgID("Excel.Application");
        if (excelType is null)
        {
            throw new InvalidOperationException(
                "No se pudo generar PDF con formato identico al Excel porque Microsoft Excel no esta instalado en este equipo.");
        }

        object? excelAppObj = null;
        object? workbooksObj = null;
        object? workbookObj = null;
        object? worksheetObj = null;

        try
        {
            excelAppObj = Activator.CreateInstance(excelType)
                ?? throw new InvalidOperationException("No se pudo iniciar Microsoft Excel para exportar el PDF.");

            dynamic excelApp = excelAppObj;
            excelApp.Visible = false;
            excelApp.DisplayAlerts = false;

            workbooksObj = excelApp.Workbooks;
            dynamic workbooks = workbooksObj;
            workbookObj = workbooks.Open(excelPath, 0, true);

            dynamic workbook = workbookObj;
            try
            {
                worksheetObj = workbook.ActiveSheet;
            }
            catch
            {
                worksheetObj = null;
            }

            if (worksheetObj is null)
            {
                try
                {
                    worksheetObj = workbook.Worksheets[1];
                }
                catch
                {
                    worksheetObj = null;
                }
            }

            if (worksheetObj is not null)
            {
                try
                {
                    ConfigureWorksheetForSinglePagePdf(worksheetObj);
                }
                catch
                {
                    // Si falla la configuración en esta versión de Excel,
                    // exportamos con la configuración actual para no bloquear al usuario.
                }
            }

            const int xlTypePdf = 0;
            const int xlQualityStandard = 0;
            var pdfExported = false;

            if (worksheetObj is not null)
            {
                try
                {
                    dynamic worksheet = worksheetObj;
                    worksheet.ExportAsFixedFormat(xlTypePdf, pdfPath, xlQualityStandard, true, false);
                    pdfExported = true;
                }
                catch
                {
                    pdfExported = false;
                }
            }

            if (!pdfExported)
            {
                workbook.ExportAsFixedFormat(xlTypePdf, pdfPath, xlQualityStandard, true, false);
                pdfExported = true;
            }

            if (!pdfExported || !File.Exists(pdfPath))
            {
                throw new InvalidOperationException("Excel no generó el archivo PDF esperado.");
            }

            workbook.Close(false);
            excelApp.Quit();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "No se pudo exportar el PDF con formato identico al Excel. Verifica que Excel este instalado y sin ventanas de bloqueo. " +
                $"Detalle: {ex.Message}",
                ex);
        }
        finally
        {
            TryCloseWorkbook(workbookObj);
            TryQuitExcel(excelAppObj);
            ReleaseComObject(worksheetObj);
            ReleaseComObject(workbookObj);
            ReleaseComObject(workbooksObj);
            ReleaseComObject(excelAppObj);
        }
    }

    private static void ConfigureWorksheetForSinglePagePdf(object worksheetObj)
    {
        const int excelMaxRows = 1_048_576;
        const int excelMaxColumns = 16_384;
        const int bottomPaddingRows = 2;

        object? usedRangeObj = null;
        object? pageSetupObj = null;

        try
        {
            dynamic worksheet = worksheetObj;

            try
            {
                worksheet.ResetAllPageBreaks();
            }
            catch
            {
            }

            pageSetupObj = worksheet.PageSetup;
            dynamic pageSetup = pageSetupObj;

            // Evita recorte superior por áreas de impresión que inician debajo de la fila 1
            // y evita hojas extra al incluir solo contenido real hasta la última celda usada.
            var tightBounds = TryGetTightPrintBounds(worksheetObj);
            if (tightBounds is not null)
            {
                try
                {
                    var paddedLastRow = Math.Min(excelMaxRows, tightBounds.LastRow + bottomPaddingRows);
                    var paddedLastCol = Math.Min(excelMaxColumns, tightBounds.LastCol);
                    pageSetup.PrintArea = $"A1:{ToExcelCellRef(paddedLastRow, paddedLastCol)}";
                }
                catch
                {
                }
            }
            else
            {
                try
                {
                    usedRangeObj = worksheet.UsedRange;
                    dynamic usedRange = usedRangeObj;
                    var usedLastRow = Convert.ToInt32(usedRange?.Row) + Convert.ToInt32(usedRange?.Rows?.Count) - 1;
                    var usedLastCol = Convert.ToInt32(usedRange?.Column) + Convert.ToInt32(usedRange?.Columns?.Count) - 1;

                    var lastRow = Math.Min(excelMaxRows, usedLastRow + bottomPaddingRows);
                    var lastCol = Math.Min(excelMaxColumns, usedLastCol);

                    if (lastRow > 0 && lastCol > 0)
                    {
                        pageSetup.PrintArea = $"A1:{ToExcelCellRef(lastRow, lastCol)}";
                    }
                    else
                    {
                        pageSetup.PrintArea = string.Empty;
                    }
                }
                catch
                {
                    try
                    {
                        pageSetup.PrintArea = string.Empty;
                    }
                    catch
                    {
                    }
                }
            }

            try
            {
                pageSetup.Zoom = false;
            }
            catch
            {
            }

            try
            {
                pageSetup.FitToPagesWide = 1;
                pageSetup.FitToPagesTall = 1;
            }
            catch
            {
            }

            try
            {
                pageSetup.CenterHorizontally = true;
            }
            catch
            {
            }
        }
        finally
        {
            ReleaseComObject(usedRangeObj);
            ReleaseComObject(pageSetupObj);
        }
    }

    private static PdfPrintBounds? TryGetTightPrintBounds(object worksheetObj)
    {
        const int xlFormulas = -4123;
        const int xlByRows = 1;
        const int xlByColumns = 2;
        const int xlNext = 1;
        const int xlPrevious = 2;

        object? cellsObj = null;
        object? anchorObj = null;
        object? firstCellObj = null;
        object? lastRowCellObj = null;
        object? lastColCellObj = null;

        try
        {
            dynamic worksheet = worksheetObj;
            cellsObj = worksheet.Cells;
            dynamic cells = cellsObj;

            anchorObj = cells[1, 1];
            firstCellObj = cells.Find("*", anchorObj, xlFormulas, Type.Missing, xlByRows, xlNext, false, Type.Missing, Type.Missing);
            if (firstCellObj is null)
            {
                return null;
            }

            lastRowCellObj = cells.Find("*", anchorObj, xlFormulas, Type.Missing, xlByRows, xlPrevious, false, Type.Missing, Type.Missing);
            lastColCellObj = cells.Find("*", anchorObj, xlFormulas, Type.Missing, xlByColumns, xlPrevious, false, Type.Missing, Type.Missing);
            if (lastRowCellObj is null || lastColCellObj is null)
            {
                return null;
            }

            var first = (dynamic)firstCellObj;
            var lastRow = (dynamic)lastRowCellObj;
            var lastCol = (dynamic)lastColCellObj;

            var firstRow = Convert.ToInt32(first.Row);
            var firstColumn = Convert.ToInt32(first.Column);
            var finalRow = Convert.ToInt32(lastRow.Row);
            var finalColumn = Convert.ToInt32(lastCol.Column);

            if (firstRow <= 0 || firstColumn <= 0 || finalRow < firstRow || finalColumn < firstColumn)
            {
                return null;
            }

            var startRef = ToExcelCellRef(firstRow, firstColumn);
            var endRef = ToExcelCellRef(finalRow, finalColumn);
            return new PdfPrintBounds($"{startRef}:{endRef}", firstRow, firstColumn, finalRow, finalColumn);
        }
        catch
        {
            return null;
        }
        finally
        {
            ReleaseComObject(lastColCellObj);
            ReleaseComObject(lastRowCellObj);
            ReleaseComObject(firstCellObj);
            ReleaseComObject(anchorObj);
            ReleaseComObject(cellsObj);
        }
    }

    private static string ToExcelCellRef(int row, int column)
        => $"{ToExcelColumnName(column)}{row}";

    private static string ToExcelColumnName(int column)
    {
        if (column <= 0)
        {
            return "A";
        }

        var col = column;
        var chars = new Stack<char>();
        while (col > 0)
        {
            col--;
            chars.Push((char)('A' + (col % 26)));
            col /= 26;
        }

        return new string(chars.ToArray());
    }

    private sealed record PdfPrintBounds(string Address, int FirstRow, int FirstCol, int LastRow, int LastCol);

    private static void TryCloseWorkbook(object? workbookObj)
    {
        if (workbookObj is null)
        {
            return;
        }

        try
        {
            dynamic workbook = workbookObj;
            workbook.Close(false);
        }
        catch
        {
        }
    }

    private static void TryQuitExcel(object? excelAppObj)
    {
        if (excelAppObj is null)
        {
            return;
        }

        try
        {
            dynamic app = excelAppObj;
            app.Quit();
        }
        catch
        {
        }
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is null || !Marshal.IsComObject(comObject))
        {
            return;
        }

        try
        {
            Marshal.FinalReleaseComObject(comObject);
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
