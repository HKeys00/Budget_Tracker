// =============================================================================
// Services/ExcelImportService.cs
// Reads an Excel (.xlsx) file and returns a list of raw ImportRow objects.
//
// Expected column layout (header row 1):
//   A: Date  |  B: Cost  |  C: Description  |  D: Type  |  E: Category
//
// Column names are matched case-insensitively, so minor header variations are
// handled gracefully.
// =============================================================================

using BudgetTracker.Models;
using OfficeOpenXml;

namespace BudgetTracker.Services;

public class ExcelImportService
{
    // Map header names to their 1-based column index after scanning row 1.
    private readonly record struct ColumnMap(int Date, int Cost, int Description, int Type, int Category);

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
    /// Throws <see cref="InvalidOperationException"/> if required columns are
    /// missing or the file cannot be read.
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

        for (int row = 2; row <= lastRow; row++)
        {
            // Skip completely blank rows
            if (IsRowEmpty(sheet, row, colMap)) continue;

            var importRow = new ImportRow
            {
                Date        = ParseDate(sheet.Cells[row, colMap.Date].Text, row),
                Cost        = ParseCost(sheet.Cells[row, colMap.Cost].Text, row),
                Description = sheet.Cells[row, colMap.Description].Text.Trim(),
                TypeRaw     = sheet.Cells[row, colMap.Type].Text.Trim(),
                CategoryRaw = sheet.Cells[row, colMap.Category].Text.Trim()
            };

            rows.Add(importRow);
        }

        return rows;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans row 1 to build a mapping of expected column names → column indices.
    /// </summary>
    private static ColumnMap BuildColumnMap(ExcelWorksheet sheet)
    {
        int lastCol = sheet.Dimension?.End.Column ?? 0;
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int col = 1; col <= lastCol; col++)
        {
            var header = sheet.Cells[1, col].Text.Trim();
            if (!string.IsNullOrWhiteSpace(header))
                map[header] = col;
        }

        return new ColumnMap(
            Date:        RequireColumn(map, "Date"),
            Cost:        RequireColumn(map, "Cost"),
            Description: RequireColumn(map, "Description"),
            Type:        RequireColumn(map, "Type"),
            Category:    RequireColumn(map, "Category")
        );
    }

    private static int RequireColumn(Dictionary<string, int> map, string name)
    {
        if (map.TryGetValue(name, out int idx)) return idx;
        throw new InvalidOperationException(
            $"Required column '{name}' not found in the spreadsheet header row. " +
            $"Found columns: {string.Join(", ", map.Keys)}");
    }

    private static bool IsRowEmpty(ExcelWorksheet sheet, int row, ColumnMap map)
    {
        return string.IsNullOrWhiteSpace(sheet.Cells[row, map.Date].Text)
            && string.IsNullOrWhiteSpace(sheet.Cells[row, map.Description].Text)
            && string.IsNullOrWhiteSpace(sheet.Cells[row, map.Cost].Text);
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
