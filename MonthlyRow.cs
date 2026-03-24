// =============================================================================
// Models/MonthlyRow.cs
// Represents one row of the monthly summary query result.
// Each row is a unique combination of (Year, Month, TypeName, CategoryName)
// with aggregated totals for that grouping.
// =============================================================================

namespace BudgetTracker.Models;

public class MonthlyRow
{
    public int     Year         { get; set; }
    public int     Month        { get; set; }

    /// <summary>e.g. "Need", "Want", "Income"</summary>
    public string  TypeName     { get; set; } = string.Empty;

    /// <summary>e.g. "Groceries", "Entertainment"</summary>
    public string  CategoryName { get; set; } = string.Empty;

    public decimal Total        { get; set; }
    public int     Count        { get; set; }

    /// <summary>True when this row represents an income transaction.</summary>
    public bool    IsExpense    { get; set; }

    /// <summary>Convenience: "Jan 2024" formatted label for display.</summary>
    public string MonthLabel =>
        new DateTime(Year, Month, 1).ToString("MMM yyyy");
}
