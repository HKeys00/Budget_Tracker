// =============================================================================
// Services/SummaryService.cs
// Queries the database and renders spending summaries to the console.
//
// Views available:
//   ShowSummary()              - All-time totals grouped by Type -> Category
//   ShowAllMonthsOverview()    - One-line-per-month table (Income / Needs / Wants / Net)
//   ShowMonthDetail(y, m)      - Full drill-down for a single month
//   ShowRecentTransactions()   - Raw list of the most recent N transactions
//
// NOTE: C# does not allow variables inside interpolation format specifiers,
// e.g. {value,-{width}} is illegal. All column alignment is done by calling
// .PadRight() / .PadLeft() on the value before it enters the interpolated
// string, so the format specifier is always a plain literal like {:C2}.
// =============================================================================

using BudgetTracker.Data;
using BudgetTracker.Models;
using BudgetTracker.UI;

namespace BudgetTracker.Services;

public class SummaryService
{
    private readonly TransactionRepository _transactions;

    // Shared column widths — used by PadRight / PadLeft helpers below.
    private const int ColLabel = 24;
    private const int ColAmt = 12;
    private const int ColCount = 6;

    // Monthly overview table has wider money columns to fit larger totals.
    private const int ColMonth = 12;
    private const int ColMoney = 13;

    public SummaryService(TransactionRepository transactions)
    {
        _transactions = transactions;
    }

    // =========================================================================
    // 1. All-time summary
    // =========================================================================

    /// <summary>Prints a breakdown of all spending grouped by Type -> Category.</summary>
    public void ShowSummary()
    {
        ConsoleDisplay.SectionHeader("All-Time Spending Summary");

        var summary = _transactions.GetSummary();

        if (summary.Count == 0)
        {
            ConsoleDisplay.Warning("No transactions found in the database yet.");
            return;
        }

        decimal grandTotal = summary.Sum(s => s.Total);
        int grandCount = summary.Sum(s => s.Count);

        string sep = Separator(ColLabel + ColAmt + ColCount + 6);

        Console.WriteLine();
        PrintHeaderRow("Category", "Total", "Txns");
        Console.WriteLine("  " + sep);

        foreach (var typeGroup in summary.GroupBy(s => s.Type).OrderBy(g => g.Key))
        {
            decimal typeTotal = typeGroup.Sum(s => s.Total);
            int typeCount = typeGroup.Sum(s => s.Count);

            PrintTypeHeading(typeGroup.Key);

            foreach (var (cat, _, total, count) in typeGroup.OrderByDescending(s => s.Total))
                PrintDataRow("  " + cat, total, count);

            PrintSubtotal(typeTotal, typeCount);
        }

        Console.WriteLine("  " + sep);
        PrintGrandTotal("TOTAL EXPENSES", grandTotal, grandCount);

        decimal totalIncome = _transactions.GetTotalIncome();
        if (totalIncome > 0)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("  " + "TOTAL INCOME".PadRight(ColLabel) + FormatMoney(totalIncome));
            Console.ResetColor();
            PrintNetLine(totalIncome - grandTotal);
        }

        Console.WriteLine();
    }

    // =========================================================================
    // 2. Monthly overview - one row per calendar month
    // =========================================================================

    /// <summary>
    /// Prints a compact table showing Income / Needs / Wants / Net for every
    /// calendar month that contains at least one transaction.
    /// </summary>
    public void ShowAllMonthsOverview()
    {
        ConsoleDisplay.SectionHeader("Monthly Overview");

        var rows = _transactions.GetMonthlyOverview();

        if (rows.Count == 0)
        {
            ConsoleDisplay.Warning("No transactions found in the database yet.");
            return;
        }

        string sep = Separator(ColMonth + ColMoney * 4 + 6);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(
            "  " +
            "Month".PadRight(ColMonth) +
            "Income".PadLeft(ColMoney) +
            "Needs".PadLeft(ColMoney) +
            "Wants".PadLeft(ColMoney) +
            "Net".PadLeft(ColMoney));
        Console.ResetColor();
        Console.WriteLine("  " + sep);

        decimal totalIncome = 0, totalNeeds = 0, totalWants = 0;

        foreach (var (year, month, income, needs, wants) in rows)
        {
            decimal net = income + needs + wants;
            string label = new DateTime(year, month, 1).ToString("MMM yyyy");

            // Write all columns except Net in default colour, then colour Net separately.
            Console.Write(
                "  " +
                label.PadRight(ColMonth) +
                FormatMoneyWide(income) +
                FormatMoneyWide(needs) +
                FormatMoneyWide(wants));

            Console.ForegroundColor = net >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(FormatMoneyWide(net));
            Console.ResetColor();

            totalIncome += income;
            totalNeeds += needs;
            totalWants += wants;
        }

        decimal grandNet = totalIncome + totalNeeds + totalWants;

        Console.WriteLine("  " + sep);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(
            "  " +
            "TOTAL".PadRight(ColMonth) +
            FormatMoneyWide(totalIncome) +
            FormatMoneyWide(totalNeeds) +
            FormatMoneyWide(totalWants));

        Console.ForegroundColor = grandNet >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(FormatMoneyWide(grandNet));
        Console.ResetColor();
        Console.WriteLine();
    }

    // =========================================================================
    // 3. Single-month drill-down
    // =========================================================================

    /// <summary>
    /// Prints the full breakdown for one calendar month:
    ///   Income by category -> Needs by category -> Wants by category -> net position.
    /// </summary>
    public void ShowMonthDetail(int year, int month)
    {
        string monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy");
        ConsoleDisplay.SectionHeader("Monthly Detail - " + monthLabel);

        var rows = _transactions.GetMonthlySummary(year, month);

        if (rows.Count == 0)
        {
            ConsoleDisplay.Warning("No transactions found for " + monthLabel + ".");
            return;
        }

        string sep = Separator(ColLabel + ColAmt + ColCount + 6);

        Console.WriteLine();
        PrintHeaderRow("Category", "Total", "Txns");
        Console.WriteLine("  " + sep);

        // ---- Income section ----
        var incomeRows = rows.Where(r => !r.IsExpense).ToList();
        if (incomeRows.Any())
        {
            PrintTypeHeading("INCOME");
            foreach (var r in incomeRows)
                PrintDataRow("  " + r.CategoryName, r.Total, r.Count);
            PrintSubtotal(incomeRows.Sum(r => r.Total), incomeRows.Sum(r => r.Count));
            Console.WriteLine();
        }

        // ---- Expense sections - one heading per Type (Need, Want, etc.) ----
        var expenseRows = rows.Where(r => r.IsExpense).ToList();
        decimal totalExp = 0;
        int countExp = 0;

        foreach (var typeGroup in expenseRows.GroupBy(r => r.TypeName).OrderBy(g => g.Key))
        {
            decimal typeTotal = typeGroup.Sum(r => r.Total);
            int typeCount = typeGroup.Sum(r => r.Count);
            totalExp += typeTotal > 0 ? 0 : typeTotal;
            countExp += typeCount;

            PrintTypeHeading(typeGroup.Key.ToUpper());

            foreach (var r in typeGroup.OrderByDescending(r => r.Total))
                PrintDataRow("  " + r.CategoryName, r.Total, r.Count);

            PrintSubtotal(typeTotal, typeCount);
            Console.WriteLine();
        }

        // ---- Footer ----
        Console.WriteLine("  " + sep);
        PrintGrandTotal("TOTAL EXPENSES", totalExp, countExp);

        decimal monthIncome = incomeRows.Sum(r => r.Total);
        if (monthIncome > 0)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("  " + "TOTAL INCOME".PadRight(ColLabel) + FormatMoney(monthIncome));
            Console.ResetColor();
            PrintNetLine(monthIncome + totalExp);
        }

        Console.WriteLine();
    }

    // =========================================================================
    // 4. Recent transactions
    // =========================================================================

    /// <summary>Prints the most recent N transactions.</summary>
    public void ShowRecentTransactions(int count = 15)
    {
        ConsoleDisplay.SectionHeader("Last " + count + " Transactions");

        var all = _transactions.GetAll().Take(count).ToList();

        if (all.Count == 0)
        {
            ConsoleDisplay.Warning("No transactions found.");
            return;
        }

        Console.WriteLine();
        foreach (var t in all)
        {
            string typeLabel = t.IsExpense ? (t.TypeName ?? "Expense") : "Income";
            Console.WriteLine(
                "  " +
                t.Date.ToString("dd MMM yyyy") + "  " +
                t.Cost.ToString("C2").PadLeft(10) + "  " +
                typeLabel.PadRight(8) + "  " +
                (t.CategoryName ?? "").PadRight(20) + "  " +
                t.Description);
        }

        Console.WriteLine();
    }

    // =========================================================================
    // Private rendering helpers
    // =========================================================================

    private static string Separator(int length) => new('─', length);

    /// <summary>Right-aligns a money value into ColAmt characters.</summary>
    private static string FormatMoney(decimal value) =>
        value.ToString("C2").PadLeft(ColAmt);

    /// <summary>Right-aligns a money value into ColMoney characters (overview table).</summary>
    private static string FormatMoneyWide(decimal value) =>
        value.ToString("C2").PadLeft(ColMoney);

    /// <summary>Prints the column header row in white.</summary>
    private static void PrintHeaderRow(string labelHeader, string amtHeader, string countHeader)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(
            "  " +
            labelHeader.PadRight(ColLabel) +
            amtHeader.PadLeft(ColAmt) +
            countHeader.PadLeft(ColCount));
        Console.ResetColor();
    }

    /// <summary>Prints a single data row: label left-aligned, amount and count right-aligned.</summary>
    private static void PrintDataRow(string label, decimal amount, int count)
    {
        Console.WriteLine(
            "  " +
            label.PadRight(ColLabel) +
            FormatMoney(amount) +
            count.ToString().PadLeft(ColCount));
    }

    private static void PrintTypeHeading(string typeName)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n  -- " + typeName);
        Console.ResetColor();
    }

    private static void PrintSubtotal(decimal total, int count)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(
            "  " +
            "  Subtotal".PadRight(ColLabel) +
            FormatMoney(total) +
            count.ToString().PadLeft(ColCount));
        Console.ResetColor();
    }

    private static void PrintGrandTotal(string label, decimal total, int count)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(
            "  " +
            label.PadRight(ColLabel) +
            FormatMoney(total) +
            count.ToString().PadLeft(ColCount));
        Console.ResetColor();
    }

    private static void PrintNetLine(decimal net)
    {
        string netLabel = net >= 0 ? "NET SURPLUS" : "NET DEFICIT";
        Console.ForegroundColor = net >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(
            "  " +
            netLabel.PadRight(ColLabel) +
            FormatMoney(Math.Abs(net)));
        Console.ResetColor();
    }
}