// =============================================================================
// Services/SummaryService.cs
// Queries the database and renders a spending summary to the console.
// =============================================================================

using BudgetTracker.Data;
using BudgetTracker.UI;

namespace BudgetTracker.Services;

public class SummaryService
{
    private readonly TransactionRepository _transactions;

    public SummaryService(TransactionRepository transactions)
    {
        _transactions = transactions;
    }

    // -------------------------------------------------------------------------
    // Display full spending summary
    // -------------------------------------------------------------------------

    /// <summary>Prints a breakdown of all spending grouped by Category and Type.</summary>
    public void ShowSummary()
    {
        ConsoleDisplay.SectionHeader("Spending Summary");

        var summary = _transactions.GetSummary();

        if (summary.Count == 0)
        {
            ConsoleDisplay.Warning("No transactions found in the database yet.");
            return;
        }

        // Overall total
        decimal grandTotal = summary.Sum(s => s.Total);
        int     grandCount = summary.Sum(s => s.Count);

        // Column widths for alignment
        const int colCat  = 22;
        const int colType = 10;
        const int colAmt  = 12;
        const int colCnt  =  6;

        string separator = new string('─', colCat + colType + colAmt + colCnt + 9);

        Console.WriteLine();
        Console.WriteLine($"  Category, {colCat}, Type-{colType} Total-{colAmt} Txns-{colCnt}");
        Console.WriteLine($"  {separator}");

        // Group by Type for sub-totals
        var byType = summary.GroupBy(s => s.Type).OrderBy(g => g.Key);

        foreach (var typeGroup in byType)
        {
            decimal typeTotal = typeGroup.Sum(s => s.Total);
            int     typeCount = typeGroup.Sum(s => s.Count);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  ── {typeGroup.Key} ──");
            Console.ResetColor();

            foreach (var (cat, type, total, count) in typeGroup.OrderByDescending(s => s.Total))
            {
                Console.WriteLine(
                    $"  cat-{colCat} type-{colType} total,{colAmt}:C2 count,{colCnt}");
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine(
            //    $"  Subtotal-{colCat}} {colType}typeTotal,{colAmt}:C2} {typeCount,{colCnt}}");
            Console.ResetColor();
        }

        Console.WriteLine($"\n  {separator}");
        Console.ForegroundColor = ConsoleColor.Green;
        //Console.WriteLine(
        //    $"  {"GRAND TOTAL",-{colCat}} {string.Empty,-{colType}} {grandTotal,{colAmt}:C2} {grandCount,{colCnt}}");
        Console.ResetColor();
        Console.WriteLine();
    }

    // -------------------------------------------------------------------------
    // Recent transactions list
    // -------------------------------------------------------------------------

    /// <summary>Prints the most recent N transactions.</summary>
    public void ShowRecentTransactions(int count = 10)
    {
        ConsoleDisplay.SectionHeader($"Last {count} Transactions");

        var all = _transactions.GetAll().Take(count).ToList();

        if (all.Count == 0)
        {
            ConsoleDisplay.Warning("No transactions found.");
            return;
        }

        Console.WriteLine();
        foreach (var t in all)
        {
            Console.WriteLine(
                $"  {t.Date:dd MMM yyyy}  {t.Cost,10:C2}  {t.TypeName,-8}  " +
                $"{t.CategoryName,-20}  {t.Description}");
        }

        Console.WriteLine();
    }
}
