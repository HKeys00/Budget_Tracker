// =============================================================================
// Data/TransactionRepository.cs
// Handles persistence and retrieval of Transactions.
// =============================================================================

using BudgetTracker.Models;
using Microsoft.Data.Sqlite;

namespace BudgetTracker.Data;

public class TransactionRepository
{
    private readonly SqliteConnection _db;

    public TransactionRepository(DatabaseContext context)
    {
        _db = context.Connection;
    }

    // -------------------------------------------------------------------------
    // Write
    // -------------------------------------------------------------------------

    /// <summary>Inserts a fully-resolved transaction into the database.</summary>
    public void Insert(Transaction transaction)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Transactions (Date, Cost, Description, IsExpense, TypeId, CategoryId)
            VALUES (@date, @cost, @desc, @isExpense, @typeId, @catId);";

        cmd.Parameters.AddWithValue("@date",      transaction.Date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@cost",      (double)transaction.Cost);
        cmd.Parameters.AddWithValue("@desc",      transaction.Description);
        cmd.Parameters.AddWithValue("@isExpense", transaction.IsExpense ? 1 : 0);
        cmd.Parameters.AddWithValue("@typeId",    transaction.TypeId);
        cmd.Parameters.AddWithValue("@catId",     transaction.CategoryId);
        cmd.ExecuteNonQuery();
    }

    // -------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all transactions joined with their type and category names,
    /// ordered by date descending.
    /// </summary>
    public List<Transaction> GetAll()
    {
        var list = new List<Transaction>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT t.Id, t.Date, t.Cost, t.Description, t.IsExpense,
                   tt.Id, tt.Name,
                   c.Id,  c.Name
            FROM Transactions t
            JOIN TransactionTypes tt ON tt.Id = t.TypeId
            JOIN Categories       c  ON c.Id  = t.CategoryId
            ORDER BY t.Date DESC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Transaction
            {
                Id           = reader.GetInt32(0),
                Date         = DateTime.Parse(reader.GetString(1)),
                Cost         = (decimal)reader.GetDouble(2),
                Description  = reader.GetString(3),
                IsExpense    = reader.GetInt32(4) == 1,
                TypeId       = reader.GetInt32(5),
                TypeName     = reader.GetString(6),
                CategoryId   = reader.GetInt32(7),
                CategoryName = reader.GetString(8)
            });
        }

        return list;
    }

    /// <summary>
    /// Returns summary totals grouped by category for expense rows only.
    /// Income rows (IsExpense = 0) are intentionally excluded.
    /// </summary>
    public List<(string Category, string Type, decimal Total, int Count)> GetSummary()
    {
        var result = new List<(string, string, decimal, int)>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT c.Name, tt.Name, SUM(t.Cost), COUNT(t.Id)
            FROM Transactions t
            JOIN TransactionTypes tt ON tt.Id = t.TypeId
            JOIN Categories       c  ON c.Id  = t.CategoryId
            WHERE t.IsExpense = 1
            GROUP BY c.Id, tt.Id
            ORDER BY SUM(t.Cost) DESC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetString(1),
                        (decimal)reader.GetDouble(2), reader.GetInt32(3)));

        return result;
    }

    /// <summary>
    /// Returns the total income (IsExpense = 0) across all stored transactions.
    /// </summary>
    public decimal GetTotalIncome()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(Cost), 0) FROM Transactions WHERE IsExpense = 0;";
        return (decimal)(double)(cmd.ExecuteScalar() ?? 0.0);
    }

    // -------------------------------------------------------------------------
    // Monthly queries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all (Year, Month) pairs that have at least one transaction,
    /// ordered chronologically oldest → newest.
    /// </summary>
    public List<(int Year, int Month)> GetAvailableMonths()
    {
        var months = new List<(int, int)>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT DISTINCT
                CAST(strftime('%Y', Date) AS INTEGER) AS Year,
                CAST(strftime('%m', Date) AS INTEGER) AS Month
            FROM Transactions
            ORDER BY Year ASC, Month ASC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            months.Add((reader.GetInt32(0), reader.GetInt32(1)));

        return months;
    }

    /// <summary>
    /// Returns per-category totals for a specific month, grouped by type.
    /// Income and expense rows are both included so the caller can separate them.
    /// </summary>
    public List<MonthlyRow> GetMonthlySummary(int year, int month)
    {
        var result = new List<MonthlyRow>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT
                CAST(strftime('%Y', t.Date) AS INTEGER)  AS Year,
                CAST(strftime('%m', t.Date) AS INTEGER)  AS Month,
                tt.Name                                  AS TypeName,
                c.Name                                   AS CategoryName,
                SUM(t.Cost)                              AS Total,
                COUNT(t.Id)                              AS Count,
                t.IsExpense
            FROM Transactions t
            JOIN TransactionTypes tt ON tt.Id = t.TypeId
            JOIN Categories       c  ON c.Id  = t.CategoryId
            WHERE strftime('%Y', t.Date) = @year
              AND strftime('%m', t.Date) = @month
            GROUP BY tt.Id, c.Id, t.IsExpense
            ORDER BY t.IsExpense DESC, SUM(t.Cost) DESC;";

        // SQLite strftime needs zero-padded strings
        cmd.Parameters.AddWithValue("@year",  year.ToString("D4"));
        cmd.Parameters.AddWithValue("@month", month.ToString("D2"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new MonthlyRow
            {
                Year         = reader.GetInt32(0),
                Month        = reader.GetInt32(1),
                TypeName     = reader.GetString(2),
                CategoryName = reader.GetString(3),
                Total        = (decimal)reader.GetDouble(4),
                Count        = reader.GetInt32(5),
                IsExpense    = reader.GetInt32(6) == 1
            });
        }

        return result;
    }

    /// <summary>
    /// Returns a high-level overview row per calendar month:
    /// total income, total needs, total wants for each month.
    /// Used to render the all-months overview table.
    /// </summary>
    public List<(int Year, int Month, decimal Income, decimal Needs, decimal Wants)> GetMonthlyOverview()
    {
        var result = new List<(int, int, decimal, decimal, decimal)>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT
                CAST(strftime('%Y', t.Date) AS INTEGER) AS Year,
                CAST(strftime('%m', t.Date) AS INTEGER) AS Month,
                COALESCE(SUM(CASE WHEN t.IsExpense = 0 THEN t.Cost ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN t.IsExpense = 1 AND LOWER(tt.Name) = 'need'  THEN t.Cost ELSE 0 END), 0) AS Needs,
                COALESCE(SUM(CASE WHEN t.IsExpense = 1 AND LOWER(tt.Name) = 'want'  THEN t.Cost ELSE 0 END), 0) AS Wants
            FROM Transactions t
            JOIN TransactionTypes tt ON tt.Id = t.TypeId
            GROUP BY Year, Month
            ORDER BY Year ASC, Month ASC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((
                reader.GetInt32(0),
                reader.GetInt32(1),
                (decimal)reader.GetDouble(2),
                (decimal)reader.GetDouble(3),
                (decimal)reader.GetDouble(4)
            ));
        }

        return result;
    }
}
