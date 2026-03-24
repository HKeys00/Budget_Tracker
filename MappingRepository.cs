// =============================================================================
// Data/MappingRepository.cs
// Stores and retrieves Description → Category + Type mappings.
//
// Both the Category (e.g. "Groceries") and the Type (e.g. "Need") are
// remembered per description so that returning merchants can be fully
// auto-suggested without any user input.
// =============================================================================

using BudgetTracker.Models;
using Microsoft.Data.Sqlite;

namespace BudgetTracker.Data;

/// <summary>
/// The full classification remembered for a previously seen description.
/// </summary>
public record DescriptionMatch(Category Category, TransactionType Type);

public class MappingRepository
{
    private readonly SqliteConnection _db;

    public MappingRepository(DatabaseContext context)
    {
        _db = context.Connection;
    }

    // -------------------------------------------------------------------------
    // Lookup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the Category and Type previously assigned to
    /// <paramref name="description"/>, or null if this description has never
    /// been seen before.
    /// </summary>
    public DescriptionMatch? GetMapping(string description)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT c.Id,  c.Name,
                   tt.Id, tt.Name
            FROM DescriptionMappings dm
            JOIN Categories       c  ON c.Id  = dm.CategoryId
            LEFT JOIN TransactionTypes tt ON tt.Id = dm.TypeId
            WHERE dm.Description = @desc
            LIMIT 1;";
        cmd.Parameters.AddWithValue("@desc", description.Trim());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var category = new Category      { Id = reader.GetInt32(0), Name = reader.GetString(1) };

        // TypeId may be NULL in rows migrated from the old schema — fall back gracefully.
        TransactionType type;
        if (reader.IsDBNull(2))
            type = new TransactionType { Id = 0, Name = string.Empty };
        else
            type = new TransactionType { Id = reader.GetInt32(2), Name = reader.GetString(3) };

        return new DescriptionMatch(category, type);
    }

    // -------------------------------------------------------------------------
    // Persist
    // -------------------------------------------------------------------------

    /// <summary>
    /// Inserts or updates the Category + Type mapping for
    /// <paramref name="description"/>. Called after the user confirms or
    /// overrides a classification.
    /// </summary>
    public void Upsert(string description, int categoryId, int typeId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO DescriptionMappings (Description, CategoryId, TypeId)
            VALUES (@desc, @catId, @typeId)
            ON CONFLICT(Description) DO UPDATE
                SET CategoryId = excluded.CategoryId,
                    TypeId     = excluded.TypeId;";
        cmd.Parameters.AddWithValue("@desc",   description.Trim());
        cmd.Parameters.AddWithValue("@catId",  categoryId);
        cmd.Parameters.AddWithValue("@typeId", typeId);
        cmd.ExecuteNonQuery();
    }
}
