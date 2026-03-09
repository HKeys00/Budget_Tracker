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
            INSERT INTO Transactions (Date, Cost, Description, TypeId, CategoryId)
            VALUES (@date, @cost, @desc, @typeId, @catId);";

        cmd.Parameters.AddWithValue("@date", transaction.Date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@cost", (double)transaction.Cost);
        cmd.Parameters.AddWithValue("@desc", transaction.Description);
        cmd.Parameters.AddWithValue("@typeId", transaction.TypeId);
        cmd.Parameters.AddWithValue("@catId", transaction.CategoryId);
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
            SELECT t.Id, t.Date, t.Cost, t.Description,
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
                TypeId       = reader.GetInt32(4),
                TypeName     = reader.GetString(5),
                CategoryId   = reader.GetInt32(6),
                CategoryName = reader.GetString(7)
            });
        }

        return list;
    }

    /// <summary>
    /// Returns summary totals grouped by category, useful for the console
    /// spending summary display.
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
            GROUP BY c.Id, tt.Id
            ORDER BY SUM(t.Cost) DESC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetString(1),
                        (decimal)reader.GetDouble(2), reader.GetInt32(3)));

        return result;
    }
}
