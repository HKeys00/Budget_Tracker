// =============================================================================
// Models/ImportRow.cs
// Holds a raw row parsed directly from the Excel spreadsheet before any
// database lookups or user interaction takes place.
// =============================================================================

namespace BudgetTracker.Models;

public class ImportRow
{
    public DateTime Date { get; set; }
    public decimal Cost { get; set; }

    /// <summary>Raw description / merchant name from the spreadsheet cell.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Raw type string from the spreadsheet (e.g. "Need", "Want").</summary>
    public string TypeRaw { get; set; } = string.Empty;

    /// <summary>Raw category string from the spreadsheet (e.g. "Groceries").</summary>
    public string CategoryRaw { get; set; } = string.Empty;
}
