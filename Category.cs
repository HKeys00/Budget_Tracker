// =============================================================================
// Models/Category.cs
// Represents a spending category stored in the Categories table.
// Categories are discovered dynamically from the spreadsheet, never hardcoded.
// =============================================================================

namespace BudgetTracker.Models;

public class Category
{
    public int Id { get; set; }

    /// <summary>Category name as read from the spreadsheet (e.g. "Groceries").</summary>
    public string Name { get; set; } = string.Empty;
}

// =============================================================================
// Models/TransactionType.cs
// Represents the "Need" vs "Want" classification for a transaction.
// =============================================================================

public class TransactionType
{
    public int Id { get; set; }

    /// <summary>Type label, typically "Need" or "Want".</summary>
    public string Name { get; set; } = string.Empty;
}
