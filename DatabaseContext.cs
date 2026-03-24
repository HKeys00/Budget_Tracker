// =============================================================================
// Data/DatabaseContext.cs
// Manages the SQLite connection and owns schema initialisation.
//
// Tables
// ------
//   Categories          – dynamic, grown from each import
//   TransactionTypes    – Need / Want / Income (also grown dynamically)
//   Transactions        – one row per transaction, FK to both lookup tables
//   DescriptionMappings – remembers Description → CategoryId + TypeId from past imports
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

            -- Lookup: spending categories (populated dynamically at runtime)
            CREATE TABLE IF NOT EXISTS Categories (
                Id      INTEGER PRIMARY KEY AUTOINCREMENT,
                Name    TEXT    NOT NULL UNIQUE COLLATE NOCASE
            );

            -- Lookup: transaction types — Need / Want / Income (grown dynamically)
            CREATE TABLE IF NOT EXISTS TransactionTypes (
                Id      INTEGER PRIMARY KEY AUTOINCREMENT,
                Name    TEXT    NOT NULL UNIQUE COLLATE NOCASE
            );

            -- Core transactions table.
            -- IsExpense = 1 for normal spending, 0 for income rows.
            -- Income rows are stored for completeness but excluded from expense summaries.
            CREATE TABLE IF NOT EXISTS Transactions (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Date        TEXT    NOT NULL,
                Cost        REAL    NOT NULL,
                Description TEXT    NOT NULL,
                IsExpense   INTEGER NOT NULL DEFAULT 1,
                TypeId      INTEGER NOT NULL REFERENCES TransactionTypes(Id),
                CategoryId  INTEGER NOT NULL REFERENCES Categories(Id)
            );

            -- Remembers which Category AND Type a description was assigned to in the past.
            -- Both are stored so the app can suggest the full classification automatically.
            CREATE TABLE IF NOT EXISTS DescriptionMappings (
                Description TEXT    NOT NULL UNIQUE COLLATE NOCASE,
                CategoryId  INTEGER NOT NULL REFERENCES Categories(Id),
                TypeId      INTEGER NOT NULL REFERENCES TransactionTypes(Id),
                PRIMARY KEY (Description)
            );
        ";
        cmd.ExecuteNonQuery();

        // ---- Migrations for databases created before these columns existed ----
        RunMigration("ALTER TABLE Transactions ADD COLUMN IsExpense INTEGER NOT NULL DEFAULT 1;");
        RunMigration("ALTER TABLE DescriptionMappings ADD COLUMN TypeId INTEGER REFERENCES TransactionTypes(Id);");
    }

    /// <summary>
    /// Attempts an ALTER TABLE migration and silently ignores the error if the
    /// column already exists (SQLite has no IF NOT EXISTS for ADD COLUMN).
    /// </summary>
    private void RunMigration(string sql)
    {
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Column already exists — nothing to do.
        }
    }

    public void Dispose() => _connection.Dispose();
}
