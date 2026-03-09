// =============================================================================
// Program.cs
// Application entry point.  Wires up all services via simple manual DI and
// runs the main menu loop.
//
// Class responsibilities at a glance:
//   DatabaseContext        – SQLite connection + schema
//   CategoryRepository     – Categories & TransactionTypes CRUD
//   MappingRepository      – Description → Category memory
//   TransactionRepository  – Transaction persistence & queries
//   ExcelImportService     – Reads .xlsx into ImportRow list
//   ImportOrchestrator     – Drives the import flow + user interaction
//   SummaryService         – Console spending summaries
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

            int saved = orchestrator.Import(path);

            if (saved > 0)
            {
                ConsoleDisplay.Success($"Import complete — {saved} transaction(s) saved.");

                // Automatically show a summary after a successful import.
                summaryService.ShowSummary();
            }
            break;

        // ---- 2: Spending summary ----
        case "2":
            summaryService.ShowSummary();
            break;

        // ---- 3: Recent transactions ----
        case "3":
            summaryService.ShowRecentTransactions(count: 15);
            break;

        // ---- 4: Exit ----
        case "4":
            Console.WriteLine("\n  Goodbye!\n");
            return;

        default:
            ConsoleDisplay.Warning("Please enter 1, 2, 3, or 4.");
            break;
    }

    // Pause so the user can read output before the menu redraws
    Console.Write("\n  Press Enter to continue...");
    Console.ReadLine();
    ConsoleDisplay.AppHeader();
}
