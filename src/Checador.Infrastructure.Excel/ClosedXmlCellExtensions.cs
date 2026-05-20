using ClosedXML.Excel;

namespace Checador.Infrastructure.Excel;

internal static class ClosedXmlCellExtensions
{
    public static string SafeGetString(this IXLCell cell)
    {
        if (!string.IsNullOrWhiteSpace(cell.FormulaA1))
        {
            return string.Empty;
        }

        try
        {
            return cell.GetString();
        }
        catch
        {
            return string.Empty;
        }
    }
}
