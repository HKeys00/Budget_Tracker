// =============================================================================
// Data/CategoryRepository.cs
// Handles all database reads and writes for Categories and TransactionTypes.
// New values encountered in the spreadsheet are inserted automatically.
// =============================================================================

using BudgetTracker.Models;
using Microsoft.Data.Sqlite;

namespace BudgetTracker.Data;

public class CategoryRepository
{
    private readonly SqliteConnection _db;

    public CategoryRepository(DatabaseContext context)
    {
        _db = context.Connection;
    }

    // -------------------------------------------------------------------------
    // Categories
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the existing category with <paramref name="name"/>, or inserts a
    /// new one and returns it.  Case-insensitive thanks to COLLATE NOCASE.
    /// </summary>
    public Category GetOrCreate(string name)
    {
        name = name.Trim();

        // Try to find existing
        using var selectCmd = _db.CreateCommand();
        selectCmd.CommandText = "SELECT Id, Name FROM Categories WHERE Name = @name LIMIT 1;";
        selectCmd.Parameters.AddWithValue("@name", name);

        using var reader = selectCmd.ExecuteReader();
        if (reader.Read())
            return new Category { Id = reader.GetInt32(0), Name = reader.GetString(1) };

        reader.Close();

        // Insert new category
        using var insertCmd = _db.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Categories (Name) VALUES (@name); SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("@name", name);
        var id = Convert.ToInt32(insertCmd.ExecuteScalar());

        return new Category { Id = id, Name = name };
    }

    /// <summary>Returns all categories currently in the database.</summary>
    public List<Category> GetAll()
    {
        var list = new List<Category>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Categories ORDER BY Name;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new Category { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        return list;
    }

    // -------------------------------------------------------------------------
    // Transaction Types
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the existing type with <paramref name="name"/>, or inserts a new
    /// one.  Handles "Need" / "Want" and any other values found in the sheet.
    /// </summary>
    public TransactionType GetOrCreateType(string name)
    {
        name = name.Trim();

        using var selectCmd = _db.CreateCommand();
        selectCmd.CommandText = "SELECT Id, Name FROM TransactionTypes WHERE Name = @name LIMIT 1;";
        selectCmd.Parameters.AddWithValue("@name", name);

        using var reader = selectCmd.ExecuteReader();
        if (reader.Read())
            return new TransactionType { Id = reader.GetInt32(0), Name = reader.GetString(1) };

        reader.Close();

        using var insertCmd = _db.CreateCommand();
        insertCmd.CommandText = "INSERT INTO TransactionTypes (Name) VALUES (@name); SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("@name", name);
        var id = Convert.ToInt32(insertCmd.ExecuteScalar());

        return new TransactionType { Id = id, Name = name };
    }

    /// <summary>Returns all transaction types currently in the database.</summary>
    public List<TransactionType> GetAllTypes()
    {
        var list = new List<TransactionType>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM TransactionTypes ORDER BY Name;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new TransactionType { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        return list;
    }
}
