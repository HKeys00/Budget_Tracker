// =============================================================================
// Services/ImportOrchestrator.cs
// Coordinates the full import pipeline:
//
//   For each row in the spreadsheet:
//     1. Show a discard prompt — user can skip the transaction entirely
//     2. Look up the description in DescriptionMappings
//        a. KNOWN description → suggest previous Category + Type; user confirms or changes
//        b. NEW description   → user picks/creates a Category, then picks Need / Want / Income
//     3. Persist the transaction and update the description mapping
//
// Neither Type nor Category is read from the spreadsheet — the database memory
// and user input are the only sources of truth.
// =============================================================================

using BudgetTracker.Data;
using BudgetTracker.Models;
using BudgetTracker.UI;

namespace BudgetTracker.Services;

public class ImportOrchestrator
{
    // Type names that flag a row as income rather than an expense.
    private static readonly HashSet<string> IncomeTypeNames =
        new(StringComparer.OrdinalIgnoreCase) { "Income" };

    private readonly ExcelImportService     _excel;
    private readonly CategoryRepository    _categories;
    private readonly MappingRepository     _mappings;
    private readonly TransactionRepository _transactions;
    private readonly ConsolePrompt          _prompt;

    public ImportOrchestrator(
        ExcelImportService     excel,
        CategoryRepository     categories,
        MappingRepository      mappings,
        TransactionRepository  transactions,
        ConsolePrompt           prompt)
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
    /// Returns (saved, skipped) counts.
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
            return (0);
        }

        if (rows.Count == 0)
        {
            ConsoleDisplay.Warning("No data rows found in the spreadsheet.");
            return (0);
        }

        Console.WriteLine($"  Found {rows.Count} transaction(s) to process.\n");

        int saved   = 0;
        int skipped = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            ConsoleDisplay.TransactionHeader(i + 1, rows.Count, row);

            // ---- Step 1: discard gate ----
            if (ShouldDiscard())
            {
                ConsoleDisplay.Warning("  Transaction discarded — skipping.");
                Console.WriteLine();
                skipped++;
                continue;
            }

            // ---- Step 2: resolve Category + Type via description lookup ----
            var (chosenCategory, chosenType) = ResolveClassification(row.Description);

            bool isIncome = IncomeTypeNames.Contains(chosenType.Name);

            // ---- Step 3: persist ----
            var transaction = new Transaction
            {
                Date        = row.Date,
                Cost        = row.Cost,
                Description = row.Description,
                IsExpense   = !isIncome,
                TypeId      = chosenType.Id,
                CategoryId  = chosenCategory.Id
            };

            _transactions.Insert(transaction);
            _mappings.Upsert(row.Description, chosenCategory.Id, chosenType.Id);

            string expenseLabel = isIncome ? "Income" : "Expense";
            ConsoleDisplay.Success(
                $"  Saved [{expenseLabel}] → Category: {chosenCategory.Name} | Type: {chosenType.Name}");
            Console.WriteLine();
            saved++;
        }

        return (saved);
    }

    // -------------------------------------------------------------------------
    // Discard prompt
    // -------------------------------------------------------------------------

    /// <summary>Returns true if the user chose to discard this transaction.</summary>
    private bool ShouldDiscard()
    {
        int choice = _prompt.ChooseOption(
            "What would you like to do with this transaction?",
            new List<string> { "Save this transaction", "Discard (skip) this transaction" });

        return choice == 1;
    }

    // -------------------------------------------------------------------------
    // Classification resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the final (Category, Type) pair for a transaction, either by
    /// suggesting a remembered match or by prompting the user from scratch.
    /// </summary>
    private (Category Category, TransactionType Type) ResolveClassification(string description)
    {
        var match = _mappings.GetMapping(description);

        if (match != null && match.Type.Id != 0)
        {
            // Known description — offer to keep previous classification or change it.
            return ResolveKnownDescription(description, match);
        }
        else
        {
            // Brand-new description — walk the user through picking category then type.
            return ResolveNewDescription(description);
        }
    }

    // -------------------------------------------------------------------------
    // Known description flow
    // -------------------------------------------------------------------------

    /// <summary>
    /// Offers three options for a previously seen description:
    ///   [1] Keep everything as before
    ///   [2] Change category only
    ///   [3] Change type (Need / Want / Income) only
    ///   [4] Change both
    /// </summary>
    private (Category, TransactionType) ResolveKnownDescription(
        string description, DescriptionMatch previous)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(
            $"\n  ℹ  '{description}' was previously: " +
            $"Category [{previous.Category.Name}] | Type [{previous.Type.Name}]");
        Console.ResetColor();

        int action = _prompt.ChooseOption(
            "What would you like to do?",
            new List<string>
            {
                $"Keep previous  →  {previous.Category.Name} / {previous.Type.Name}",
                "Change category only",
                "Change type (Need / Want / Income) only",
                "Change both category and type"
            });

        var category = previous.Category;
        var type     = previous.Type;

        if (action == 1 || action == 3)   // change category
            category = PickCategory(preselectedName: previous.Category.Name);

        if (action == 2 || action == 3)   // change type
            type = PickType();

        return (category, type);
    }

    // -------------------------------------------------------------------------
    // New description flow
    // -------------------------------------------------------------------------

    /// <summary>
    /// Walks the user through picking a category (from existing list or creating
    /// a new one) and then choosing Need / Want / Income.
    /// </summary>
    private (Category, TransactionType) ResolveNewDescription(string description)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  ★  New description: '{description}'");
        Console.ResetColor();

        var category = PickCategory(preselectedName: null);
        var type     = PickType();

        return (category, type);
    }

    // -------------------------------------------------------------------------
    // Shared sub-prompts
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shows the list of all categories currently in the DB, with a "create new"
    /// option at the bottom.  <paramref name="preselectedName"/> is shown as
    /// "(current)" to make it easy to spot when changing.
    /// </summary>
    private Category PickCategory(string? preselectedName)
    {
        var allCategories = _categories.GetAll();

        // Build display labels, marking the current selection if provided.
        var labels = allCategories
            .Select(c => string.Equals(c.Name, preselectedName, StringComparison.OrdinalIgnoreCase)
                ? $"{c.Name}  (current)"
                : c.Name)
            .ToList();

        string chosenName = _prompt.ChooseOrCreateCategory(
            "Select a category:", labels);

        // ChooseOrCreateCategory returns a name; persist it if it's brand new.
        return _categories.GetOrCreate(chosenName);
    }

    /// <summary>
    /// Asks the user to choose between Need, Want, or Income.
    /// Existing type records are reused; new ones are created as needed.
    /// </summary>
    private TransactionType PickType()
    {
        // These are the standard options always offered.
        // Any additional types already in the DB are appended for completeness.
        var standardTypes = new List<string> { "Need", "Want", "Income" };
        var dbTypes       = _categories.GetAllTypes().Select(t => t.Name).ToList();

        // Merge: standard first, then any DB types not already in the standard list.
        var allTypeNames = standardTypes
            .Concat(dbTypes.Where(t => !standardTypes.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        int choice = _prompt.ChooseOption("Select type:", allTypeNames);
        return _categories.GetOrCreateType(allTypeNames[choice]);
    }
}
