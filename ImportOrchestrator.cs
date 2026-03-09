// =============================================================================
// Services/ImportOrchestrator.cs
// Coordinates the full import pipeline:
//   1. Parse Excel rows
//   2. For each row, resolve Type and Category via DB (with user interaction)
//   3. Persist finalised transactions and update description mappings
// =============================================================================

using BudgetTracker.Data;
using BudgetTracker.Models;
using BudgetTracker.UI;

namespace BudgetTracker.Services;

public class ImportOrchestrator
{
    private readonly ExcelImportService    _excel;
    private readonly CategoryRepository   _categories;
    private readonly MappingRepository    _mappings;
    private readonly TransactionRepository _transactions;
    private readonly ConsolePrompt         _prompt;

    public ImportOrchestrator(
        ExcelImportService    excel,
        CategoryRepository    categories,
        MappingRepository     mappings,
        TransactionRepository transactions,
        ConsolePrompt         prompt)
    {
        _excel        = excel;
        _categories   = categories;
        _mappings     = mappings;
        _transactions = transactions;
        _prompt       = prompt;
    }

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs the full import for the given file path.
    /// Returns the number of transactions saved.
    /// </summary>
    public int Import(string filePath)
    {
        ConsoleDisplay.SectionHeader($"Importing: {Path.GetFileName(filePath)}");

        List<ImportRow> rows;
        try
        {
            rows = _excel.Parse(filePath);
        }
        catch (Exception ex)
        {
            ConsoleDisplay.Error($"Could not read spreadsheet: {ex.Message}");
            return 0;
        }

        if (rows.Count == 0)
        {
            ConsoleDisplay.Warning("No data rows found in the spreadsheet.");
            return 0;
        }

        Console.WriteLine($"  Found {rows.Count} transaction(s) to process.\n");

        int saved = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            ConsoleDisplay.TransactionHeader(i + 1, rows.Count, row);

            // ---- Resolve TransactionType ----
            var type = _categories.GetOrCreateType(
                string.IsNullOrWhiteSpace(row.TypeRaw) ? "Unknown" : row.TypeRaw);

            // ---- Resolve Category ----
            // Ensure the category from the sheet exists in the DB first.
            Category sheetCategory = _categories.GetOrCreate(row.CategoryRaw);

            // Check if we've seen this description before.
            Category? previousCategory = _mappings.GetCategoryForDescription(row.Description);

            Category chosenCategory;

            if (previousCategory != null)
            {
                // Known merchant — offer to reuse or override.
                chosenCategory = ResolveKnownDescription(row.Description, previousCategory, sheetCategory);
            }
            else
            {
                // Brand new description — show all available categories.
                chosenCategory = ResolveNewDescription(row.Description, sheetCategory);
            }

            // ---- Persist ----
            var transaction = new Transaction
            {
                Date        = row.Date,
                Cost        = row.Cost,
                Description = row.Description,
                TypeId      = type.Id,
                CategoryId  = chosenCategory.Id
            };

            _transactions.Insert(transaction);
            _mappings.Upsert(row.Description, chosenCategory.Id);

            ConsoleDisplay.Success($"  Saved → Category: {chosenCategory.Name} | Type: {type.Name}");
            Console.WriteLine();
            saved++;
        }

        return saved;
    }

    // -------------------------------------------------------------------------
    // Category resolution helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// For a previously seen description, ask the user whether to reuse the
    /// remembered category, use the one from the spreadsheet, or pick another.
    /// </summary>
    private Category ResolveKnownDescription(
        string description, Category previous, Category fromSheet)
    {
        Console.WriteLine($"  \u2139  '{description}' was previously categorised as: [{previous.Name}]");

        // Build options list
        var options = new List<(string Label, Func<Category> Resolve)>();

        // Option 1: reuse previous
        options.Add(($"Keep previous category: {previous.Name}", () => previous));

        // Option 2: use sheet value (only if different from previous)
        if (!string.Equals(fromSheet.Name, previous.Name, StringComparison.OrdinalIgnoreCase))
            options.Add(($"Use spreadsheet category: {fromSheet.Name}", () => fromSheet));

        // Option 3+: any other category already in the DB
        var allCategories = _categories.GetAll();
        foreach (var cat in allCategories)
        {
            if (cat.Id != previous.Id && cat.Id != fromSheet.Id)
                options.Add(($"Choose: {cat.Name}", () => cat));
        }

        int choice = _prompt.ChooseOption("  Select a category option:", options.Select(o => o.Label).ToList());
        return options[choice].Resolve();
    }

    /// <summary>
    /// For a brand-new description, show all categories from the DB (which now
    /// includes the one from the sheet) and let the user pick.
    /// </summary>
    private Category ResolveNewDescription(string description, Category fromSheet)
    {
        Console.WriteLine($"  \u2605  New location: '{description}'");
        Console.WriteLine($"     Spreadsheet suggests category: [{fromSheet.Name}]");

        var allCategories = _categories.GetAll();

        // Put the sheet's suggested category first for convenience
        var ordered = allCategories
            .OrderBy(c => c.Id != fromSheet.Id)   // false (0) sorts before true (1)
            .ThenBy(c => c.Name)
            .ToList();

        int choice = _prompt.ChooseOption(
            "  Select a category for this transaction:",
            ordered.Select(c => c.Id == fromSheet.Id ? $"{c.Name} (suggested)" : c.Name).ToList());

        return ordered[choice];
    }
}
