// =============================================================================
// Models/ImportRow.cs
// Holds a raw row parsed directly from the Excel spreadsheet before any
// database lookups or user interaction takes place.
//
// Type and Category are NOT read from the sheet — they are resolved at runtime
// by looking up the Description in the database and/or prompting the user.
// =============================================================================

namespace BudgetTracker.Models;

public class ImportRow
{
    public DateTime Date { get; set; }
    public decimal Cost { get; set; }

    /// <summary>
    /// Merchant / location name, cleaned of bank noise (e.g. trailing AUD000…).
    /// Used as the key for description → category/type memory lookups.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
