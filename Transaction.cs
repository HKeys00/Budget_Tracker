// =============================================================================
// Models/Transaction.cs
// Represents a single financial transaction row from the Excel spreadsheet.
// =============================================================================

namespace BudgetTracker.Models;

public class Transaction
{
    public int Id { get; set; }

    /// <summary>The date the transaction occurred.</summary>
    public DateTime Date { get; set; }

    /// <summary>The cost/amount of the transaction.</summary>
    public decimal Cost { get; set; }

    /// <summary>Description or merchant name (e.g. "Woolworths", "Netflix").</summary>
    public string Description { get; set; } = string.Empty;

    public bool IsExpense { get; set; }

    /// <summary>FK to the TransactionType table (Need / Want).</summary>
    public int TypeId { get; set; }
    public string? TypeName { get; set; }   // populated via JOIN when reading back

    /// <summary>FK to the Category table.</summary>
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; } // populated via JOIN when reading back
}
