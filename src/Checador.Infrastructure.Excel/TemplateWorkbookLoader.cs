using ClosedXML.Excel;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Checador.Infrastructure.Excel;

internal static class TemplateWorkbookLoader
{
    private const string PictureNameLengthError = "Picture names cannot be more than 31 characters";
    private const long EmusPerPixel = 9525;
    private const long MaxLogoWidthEmus = 82 * EmusPerPixel;
    private const long MaxLogoHeightEmus = 82 * EmusPerPixel;

    public static TemplateWorkbookLease Open(string templatePath)
    {
        var tempPath = CreateSanitizedTemplateCopy(templatePath);
        try
        {
            var workbook = new XLWorkbook(tempPath);
            NormalizePictures(workbook);
            return new TemplateWorkbookLease(workbook, tempPath);
        }
        catch (ArgumentException ex) when (ex.Message.Contains(PictureNameLengthError, StringComparison.OrdinalIgnoreCase))
        {
            SanitizeDrawingObjectNames(tempPath);

            var workbook = new XLWorkbook(tempPath);
            NormalizePictures(workbook);
            return new TemplateWorkbookLease(workbook, tempPath);
        }
    }

    private static string CreateSanitizedTemplateCopy(string templatePath)
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"checador_tpl_{Guid.NewGuid():N}{Path.GetExtension(templatePath)}");

        File.Copy(templatePath, tempPath, overwrite: true);
        SanitizeDrawingObjectNames(tempPath);
        return tempPath;
    }

    public static void NormalizePictures(XLWorkbook workbook)
    {
        const int maxLogoWidth = 82;
        const int maxLogoHeight = 82;

        foreach (var worksheet in workbook.Worksheets)
        {
            foreach (var picture in worksheet.Pictures)
            {
                if (picture.Width <= maxLogoWidth && picture.Height <= maxLogoHeight)
                {
                    continue;
                }

                var ratio = Math.Min(
                    maxLogoWidth / (double)Math.Max(1, picture.Width),
                    maxLogoHeight / (double)Math.Max(1, picture.Height));

                picture.WithSize(
                    Math.Max(1, (int)Math.Round(picture.Width * ratio)),
                    Math.Max(1, (int)Math.Round(picture.Height * ratio)));
            }
        }
    }

    private static void SanitizeDrawingObjectNames(string workbookPath)
    {
        using var archive = ZipFile.Open(workbookPath, ZipArchiveMode.Update);
        var entries = archive.Entries
            .Where(entry =>
                entry.FullName.StartsWith("xl/drawings/", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in entries)
        {
            string? updatedXml = null;
            var entryName = entry.FullName;
            using (var stream = entry.Open())
            {
                XDocument? document;
                try
                {
                    document = XDocument.Load(stream);
                }
                catch
                {
                    continue;
                }

                updatedXml = SanitizeDrawingXml(document);

                if (string.IsNullOrWhiteSpace(updatedXml))
                {
                    continue;
                }
            }

            if (string.IsNullOrWhiteSpace(updatedXml))
            {
                continue;
            }

            entry.Delete();
            var replacement = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var write = replacement.Open();
            using var writer = new StreamWriter(write);
            writer.Write(updatedXml);
        }
    }

    public static string SanitizeDrawingXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return xml;
        }

        try
        {
            return SanitizeDrawingXml(XDocument.Parse(xml, System.Xml.Linq.LoadOptions.PreserveWhitespace)) ?? xml;
        }
        catch
        {
            return xml;
        }
    }

    private static string? SanitizeDrawingXml(XDocument document)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var element in document.Descendants().Where(x => x.Name.LocalName == "cNvPr"))
        {
            var nameAttribute = element.Attribute("name");
            if (nameAttribute is null || string.IsNullOrWhiteSpace(nameAttribute.Value))
            {
                continue;
            }

            var fixedName = EnsureNameWithinLimit(nameAttribute.Value, usedNames);
            if (!string.Equals(fixedName, nameAttribute.Value, StringComparison.Ordinal))
            {
                nameAttribute.Value = fixedName;
                changed = true;
            }
        }

        foreach (var anchor in document.Descendants()
            .Where(x => x.Name.LocalName is "oneCellAnchor" or "absoluteAnchor"))
        {
            var ext = anchor.Elements().FirstOrDefault(x => x.Name.LocalName == "ext");
            if (ext is null)
            {
                continue;
            }

            var cxAttr = ext.Attribute("cx");
            var cyAttr = ext.Attribute("cy");
            if (cxAttr is null || cyAttr is null
                || !long.TryParse(cxAttr.Value, out var cx)
                || !long.TryParse(cyAttr.Value, out var cy)
                || (cx <= MaxLogoWidthEmus && cy <= MaxLogoHeightEmus))
            {
                continue;
            }

            var ratio = Math.Min(
                MaxLogoWidthEmus / (double)Math.Max(1, cx),
                MaxLogoHeightEmus / (double)Math.Max(1, cy));

            cxAttr.Value = Math.Max(1, (long)Math.Round(cx * ratio)).ToString(CultureInfo.InvariantCulture);
            cyAttr.Value = Math.Max(1, (long)Math.Round(cy * ratio)).ToString(CultureInfo.InvariantCulture);
            changed = true;
        }

        if (!changed)
        {
            return null;
        }

        return document.Declaration is null
            ? document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)
            : $"{document.Declaration}{Environment.NewLine}{document}";
    }

    private static string EnsureNameWithinLimit(string rawName, HashSet<string> usedNames)
    {
        var trimmed = rawName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            trimmed = "Imagen";
        }

        var baseName = trimmed.Length <= 31 ? trimmed : trimmed[..31];
        var candidate = baseName;
        var suffix = 1;
        while (usedNames.Contains(candidate))
        {
            var suffixText = $"_{suffix}";
            var maxBaseLength = Math.Max(1, 31 - suffixText.Length);
            var shortBase = baseName.Length > maxBaseLength
                ? baseName[..maxBaseLength]
                : baseName;
            candidate = shortBase + suffixText;
            suffix++;
        }

        usedNames.Add(candidate);
        return candidate;
    }
}

internal sealed class TemplateWorkbookLease : IDisposable
{
    private readonly string? _temporaryPath;
    private bool _disposed;

    public TemplateWorkbookLease(XLWorkbook workbook, string? temporaryPath)
    {
        Workbook = workbook;
        _temporaryPath = temporaryPath;
    }

    public XLWorkbook Workbook { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Workbook.Dispose();

        if (string.IsNullOrWhiteSpace(_temporaryPath))
        {
            return;
        }

        try
        {
            if (File.Exists(_temporaryPath))
            {
                File.Delete(_temporaryPath);
            }
        }
        catch
        {
        }
    }
}
