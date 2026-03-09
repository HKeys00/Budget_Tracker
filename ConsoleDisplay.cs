// =============================================================================
// UI/ConsoleDisplay.cs
// Static helpers for consistent, coloured console output.
// All raw Console.WriteLine calls from services go through here.
// =============================================================================

using BudgetTracker.Models;

namespace BudgetTracker.UI;

public static class ConsoleDisplay
{
    // -------------------------------------------------------------------------
    // Layout helpers
    // -------------------------------------------------------------------------

    public static void AppHeader()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║        Budget Tracker  v1.0          ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void SectionHeader(string title)
    {
        int max = 34 - title.Length;
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"\n  ┌─ {title} '─'{Math.Max(0, max)}┐");
        Console.ResetColor();
    }

    /// <summary>Prints the transaction counter and key details before prompting.</summary>
    public static void TransactionHeader(int current, int total, ImportRow row)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  ── Transaction {current}/{total} ──────────────────────────");
        Console.ResetColor();
        Console.WriteLine($"  Date:        {row.Date:dd MMM yyyy}");
        Console.WriteLine($"  Description: {row.Description}");
        Console.WriteLine($"  Cost:        {row.Cost:C2}");
        Console.WriteLine($"  Type:        {row.TypeRaw}");
        Console.WriteLine($"  Sheet cat.:  {row.CategoryRaw}");
    }

    // -------------------------------------------------------------------------
    // Status messages
    // -------------------------------------------------------------------------

    public static void Success(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {message}");
        Console.ResetColor();
    }

    public static void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠ {message}");
        Console.ResetColor();
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {message}");
        Console.ResetColor();
    }

    // -------------------------------------------------------------------------
    // Main menu
    // -------------------------------------------------------------------------

    public static void MainMenu()
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("\n  ┌─ Main Menu ────────────────────────────┐");
        Console.ResetColor();
        Console.WriteLine("    [1] Import Excel spreadsheet");
        Console.WriteLine("    [2] View spending summary");
        Console.WriteLine("    [3] View recent transactions");
        Console.WriteLine("    [4] Exit");
        Console.Write("\n  Enter number (1–4): ");
    }
}
