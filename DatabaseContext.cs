// =============================================================================
// Data/DatabaseContext.cs
// Manages the SQLite connection and owns schema initialisation.
//
// Tables
// ------
//   Categories          – dynamic, grown from each import
//   TransactionTypes    – Need / Want (also grown dynamically)
//   Transactions        – one row per transaction, FK to both lookup tables
//   DescriptionMappings – remembers Description → CategoryId from past imports
// =============================================================================

using Microsoft.Data.Sqlite;

namespace BudgetTracker.Data;

public class DatabaseContext : IDisposable
{
    // Path to the SQLite file, placed next to the executable by default.
    private const string DbFileName = "budget_tracker.db";

    private readonly SqliteConnection _connection;

    public DatabaseContext()
    {
        _connection = new SqliteConnection($"Data Source={DbFileName}");
        _connection.Open();
        InitialiseSchema();
    }

    // -------------------------------------------------------------------------
    // Public accessor so repositories can share the same connection.
    // -------------------------------------------------------------------------
    public SqliteConnection Connection => _connection;

    // -------------------------------------------------------------------------
    // Create all tables if they do not already exist.
    // -------------------------------------------------------------------------
    private void InitialiseSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            PRAGMA foreign_keys = ON;

            -- Lookup: spending categories (populated dynamically from spreadsheet)
            CREATE TABLE IF NOT EXISTS Categories (
                Id      INTEGER PRIMARY KEY AUTOINCREMENT,
                Name    TEXT    NOT NULL UNIQUE COLLATE NOCASE
            );

            -- Lookup: transaction types (e.g. Need, Want)
            CREATE TABLE IF NOT EXISTS TransactionTypes (
                Id      INTEGER PRIMARY KEY AUTOINCREMENT,
                Name    TEXT    NOT NULL UNIQUE COLLATE NOCASE
            );

            -- Core transactions table
            CREATE TABLE IF NOT EXISTS Transactions (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Date        TEXT    NOT NULL,
                Cost        REAL    NOT NULL,
                Description TEXT    NOT NULL,
                TypeId      INTEGER NOT NULL REFERENCES TransactionTypes(Id),
                CategoryId  INTEGER NOT NULL REFERENCES Categories(Id)
            );

            -- Remembers which category a description was assigned to in the past,
            -- so the app can suggest it automatically on future imports.
            CREATE TABLE IF NOT EXISTS DescriptionMappings (
                Description TEXT    NOT NULL UNIQUE COLLATE NOCASE,
                CategoryId  INTEGER NOT NULL REFERENCES Categories(Id),
                PRIMARY KEY (Description)
            );
        ";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
