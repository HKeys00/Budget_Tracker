// =============================================================================
// Services/ExcelImportService.cs
// Reads an Excel (.xlsx) file and returns a list of raw ImportRow objects.
//
// Required columns (matched case-insensitively by header name):
//   Date  |  Cost  |  Description
//
// Type and Category are intentionally NOT read from the spreadsheet.
// They are resolved at runtime via description lookups and user prompts.
// =============================================================================

using BudgetTracker.Models;
using OfficeOpenXml;

namespace BudgetTracker.Services;

public class ExcelImportService
{
    // Only the three columns the app actually needs from the sheet.
    private readonly record struct ColumnMap(int Date, int Cost, int Description);

    public ExcelImportService()
    {
        // EPPlus 5+ requires a licence context; NonCommercial is free.
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses the spreadsheet at <paramref name="filePath"/> and returns one
    /// <see cref="ImportRow"/> per data row (skipping the header).
    /// Throws if required columns are missing or the file cannot be read.
    /// </summary>
    public List<ImportRow> Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var rows = new List<ImportRow>();

        using var package = new ExcelPackage(new FileInfo(filePath));
        var sheet = package.Workbook.Worksheets[0]
            ?? throw new InvalidOperationException("The workbook contains no worksheets.");

        var colMap = BuildColumnMap(sheet);
        int lastRow = sheet.Dimension?.End.Row ?? 1;

        for (int row = 1; row <= lastRow; row++)
        {
            if (IsRowEmpty(sheet, row, colMap)) continue;

            rows.Add(new ImportRow
            {
                Date        = ParseDate(sheet.Cells[row, colMap.Date].Text, row),
                Cost        = ParseCost(sheet.Cells[row, colMap.Cost].Text, row),
                Description = CleanDescription(sheet.Cells[row, colMap.Description].Text)
            });
        }

        return rows;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans row 1 to find the Date, Cost, and Description column indices.
    /// Any other columns present in the sheet (Type, Category, etc.) are ignored.
    /// </summary>
    private static ColumnMap BuildColumnMap(ExcelWorksheet sheet)
    {
        return new ColumnMap(
            Date:        1,
            Cost:        2,
            Description: 3
        );
    }

    private static bool IsRowEmpty(ExcelWorksheet sheet, int row, ColumnMap map)
    {
        return string.IsNullOrWhiteSpace(sheet.Cells[row, map.Date].Text)
            && string.IsNullOrWhiteSpace(sheet.Cells[row, map.Description].Text)
            && string.IsNullOrWhiteSpace(sheet.Cells[row, map.Cost].Text);
    }

    /// <summary>
    /// Strips bank-appended noise from transaction descriptions.
    ///
    /// Banks often append a currency code followed by a zero-padded amount, e.g.:
    ///   "RETAIL PURCHASE SQ *AROMAS ON BRIDG,Abbotsford 1402 AUD000000005000"
    ///
    /// The trailing "AUD0000..." token is removed so the merchant name is clean
    /// and consistent across imports — which matters for the description→category
    /// memory to work reliably.
    /// </summary>
    private static string CleanDescription(string raw)
    {
        raw = raw.Trim();

        // Match a 3-letter currency code followed by all-digit zero-padded amount at end of string.
        // e.g. " AUD000000005000" or " USD000000012500"
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            raw,
            @"\s+[A-Z]{3}0+\d+\s*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return cleaned.Trim();
    }

    private static DateTime ParseDate(string raw, int row)
    {
        if (DateTime.TryParse(raw, out var dt)) return dt;
        // EPPlus sometimes gives OA date as a number string
        if (double.TryParse(raw, out double oaDate))
            return DateTime.FromOADate(oaDate);
        throw new InvalidOperationException(
            $"Row {row}: Cannot parse date value '{raw}'.");
    }

    private static decimal ParseCost(string raw, int row)
    {
        // Strip common currency symbols before parsing
        raw = raw.Replace("$", "").Replace("£", "").Replace("€", "").Trim();
        if (decimal.TryParse(raw, out decimal value)) return value;
        throw new InvalidOperationException(
            $"Row {row}: Cannot parse cost value '{raw}'.");
    }
}
