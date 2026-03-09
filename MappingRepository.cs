// =============================================================================
// Data/MappingRepository.cs
// Stores and retrieves the Description → Category mapping so that the app can
// suggest previously used categories for known merchants / restaurants.
// =============================================================================

using BudgetTracker.Models;
using Microsoft.Data.Sqlite;

namespace BudgetTracker.Data;

public class MappingRepository
{
    private readonly SqliteConnection _db;

    public MappingRepository(DatabaseContext context)
    {
        _db = context.Connection;
    }

    /// <summary>
    /// Returns the category previously assigned to <paramref name="description"/>,
    /// or null if this description has never been seen before.
    /// </summary>
    public Category? GetCategoryForDescription(string description)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT c.Id, c.Name
            FROM DescriptionMappings dm
            JOIN Categories c ON c.Id = dm.CategoryId
            WHERE dm.Description = @desc
            LIMIT 1;";
        cmd.Parameters.AddWithValue("@desc", description.Trim());

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return new Category { Id = reader.GetInt32(0), Name = reader.GetString(1) };

        return null;
    }

    /// <summary>
    /// Inserts or updates the mapping for <paramref name="description"/>.
    /// Called after the user confirms or overrides a category choice.
    /// </summary>
    public void Upsert(string description, int categoryId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO DescriptionMappings (Description, CategoryId)
            VALUES (@desc, @catId)
            ON CONFLICT(Description) DO UPDATE SET CategoryId = excluded.CategoryId;";
        cmd.Parameters.AddWithValue("@desc", description.Trim());
        cmd.Parameters.AddWithValue("@catId", categoryId);
        cmd.ExecuteNonQuery();
    }
}
