# Budget Tracker — Console App

A C# console application for importing and categorising transactions from Excel spreadsheets. Categories and Types are read dynamically from your spreadsheet — nothing is hardcoded.

---

## Project Structure

```
BudgetTracker/
├── Program.cs                        ← Entry point & main menu loop
│
├── Models/
│   ├── Transaction.cs                ← Core transaction entity
│   ├── Category.cs                   ← Category + TransactionType models
│   └── ImportRow.cs                  ← Raw row from Excel (pre-DB resolution)
│
├── Data/
│   ├── DatabaseContext.cs            ← SQLite connection & schema creation
│   ├── CategoryRepository.cs         ← Categories & TransactionTypes CRUD
│   ├── MappingRepository.cs          ← Description → Category memory
│   └── TransactionRepository.cs      ← Transaction insert & summary queries
│
├── Services/
│   ├── ExcelImportService.cs         ← Parses .xlsx using EPPlus
│   ├── ImportOrchestrator.cs         ← Import pipeline + user interaction
│   └── SummaryService.cs             ← Console spending summaries
│
└── UI/
    ├── ConsolePrompt.cs              ← All numbered-list user input
    └── ConsoleDisplay.cs             ← Coloured output helpers
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Setup & Run

```bash
cd BudgetTracker
dotnet run
```

On first run, `budget_tracker.db` (SQLite) will be created in the same directory as the executable.

---

## Excel Spreadsheet Format

Your `.xlsx` file must have a **header row** with these column names (case-insensitive):

| Column       | Description                            | Example          |
|-------------|----------------------------------------|------------------|
| Date        | Transaction date                        | 15/03/2024       |
| Cost        | Amount (supports $ £ € symbols)         | 45.99            |
| Description | Merchant / location name                | Woolworths       |
| Type        | Need or Want (any value is accepted)    | Need             |
| Category    | Spending category (any value accepted)  | Groceries        |

Columns can be in **any order** — the app reads them by header name.

New Categories and Types encountered in your spreadsheet are automatically added to the database.

---

## How Categorisation Works

### Known merchant (seen before)
```
  ℹ 'Woolworths' was previously categorised as: [Groceries]

  Select a category option:
    [1] Keep previous category: Groceries
    [2] Use spreadsheet category: Groceries (suggested)
    [3] Choose: Entertainment
    [4] Choose: Transport
    ...

  Enter number (1–4):
```

### New merchant (first time seen)
```
  ★ New location: 'Nando's'
     Spreadsheet suggests category: [Food & Dining]

  Select a category for this transaction:
    [1] Food & Dining (suggested)
    [2] Entertainment
    [3] Groceries
    ...

  Enter number (1–3):
```

The chosen category is remembered for future imports.

---

## Database Tables

| Table                | Purpose                                           |
|---------------------|---------------------------------------------------|
| `Categories`         | All spending categories (grown from spreadsheets) |
| `TransactionTypes`   | Need / Want types (grown from spreadsheets)       |
| `Transactions`       | All imported transactions                         |
| `DescriptionMappings`| Merchant name → Category memory                  |

---

## Menu Options

```
  [1] Import Excel spreadsheet      ← Runs the import flow
  [2] View spending summary         ← Totals grouped by Type and Category
  [3] View recent transactions      ← Last 15 transactions
  [4] Exit
```
