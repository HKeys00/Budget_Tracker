// =============================================================================
// Program.cs
// Application entry point.  Wires up all services via simple manual DI and
// runs the main menu loop.
//
// Class responsibilities at a glance:
//   DatabaseContext        – SQLite connection + schema
//   CategoryRepository     – Categories & TransactionTypes CRUD
//   MappingRepository      – Description → Category + Type memory
//   TransactionRepository  – Transaction persistence & queries
//   ExcelImportService     – Reads .xlsx into ImportRow list
//   ImportOrchestrator     – Drives the import flow + user interaction
//   SummaryService         – Console spending summaries (all-time + monthly)
//   ConsolePrompt          – All numbered-list user input
//   ConsoleDisplay         – Coloured output helpers
// =============================================================================

using BudgetTracker.Data;
using BudgetTracker.Services;
using BudgetTracker.UI;

// ---------------------------------------------------------------------------
// Composition root – wire up dependencies manually (no DI framework needed)
// ---------------------------------------------------------------------------

using var dbContext = new DatabaseContext();

var categoryRepo    = new CategoryRepository(dbContext);
var mappingRepo     = new MappingRepository(dbContext);
var transactionRepo = new TransactionRepository(dbContext);
var prompt          = new ConsolePrompt();
var excelService    = new ExcelImportService();
var orchestrator    = new ImportOrchestrator(excelService, categoryRepo, mappingRepo, transactionRepo, prompt);
var summaryService  = new SummaryService(transactionRepo);

// ---------------------------------------------------------------------------
// Main menu loop
// ---------------------------------------------------------------------------

ConsoleDisplay.AppHeader();

while (true)
{
    ConsoleDisplay.MainMenu();
    string? input = Console.ReadLine()?.Trim();

    switch (input)
    {
        // ---- 1: Import spreadsheet ----
        case "1":
            string? path = prompt.AskForFilePath("Enter the full path to your Excel file (.xlsx)");
            if (path == null)
            {
                ConsoleDisplay.Warning("Import cancelled.");
                break;
            }

            var saved = orchestrator.Import(path);

            if (saved > 0)
            {
                ConsoleDisplay.Success($"Import complete — {saved} saved");
                summaryService.ShowSummary();
            }
            break;

        // ---- 2: All-time summary ----
        case "2":
            summaryService.ShowSummary();
            break;

        // ---- 3: Monthly overview (all months in one table) ----
        case "3":
            summaryService.ShowAllMonthsOverview();
            break;

        // ---- 4: Monthly detail (user picks a month) ----
        case "4":
            ShowMonthlyDetail(transactionRepo, summaryService, prompt);
            break;

        // ---- 5: Recent transactions ----
        case "5":
            summaryService.ShowRecentTransactions(count: 15);
            break;

        // ---- 6: Exit ----
        case "6":
            Console.WriteLine("\n  Goodbye!\n");
            return;

        default:
            ConsoleDisplay.Warning("Please enter a number between 1 and 6.");
            break;
    }

    Console.Write("\n  Press Enter to continue...");
    Console.ReadLine();
    ConsoleDisplay.AppHeader();
}

// ---------------------------------------------------------------------------
// Month picker — fetches available months and prompts the user to choose one
// ---------------------------------------------------------------------------

static void ShowMonthlyDetail(
    TransactionRepository repo,
    SummaryService        summary,
    ConsolePrompt         prompt)
{
    var months = repo.GetAvailableMonths();

    if (months.Count == 0)
    {
        ConsoleDisplay.Warning("No transactions found in the database yet.");
        return;
    }

    // Build display labels e.g. "Jan 2024", "Feb 2024" …
    var labels = months
        .Select(m => new DateTime(m.Year, m.Month, 1).ToString("MMM yyyy"))
        .ToList();

    int choice = prompt.ChooseOption("Select a month to view:", labels);
    var (year, month) = months[choice];

    summary.ShowMonthDetail(year, month);
}
