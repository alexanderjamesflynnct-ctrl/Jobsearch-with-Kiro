using Microsoft.Data.Sqlite;
using System.IO;

namespace JobSearchAPI.Data;

public class JobSearchDatabase
{
    private readonly string _connectionString;

    public JobSearchDatabase()
    {
        // Resolve DB path relative to the runtime directory
        var dbPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SearchCode", "jobs.db"));
        
        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// Creates a new connection to the SQLite database.
    /// Used by Controllers to perform Dapper or ADO.NET operations.
    /// </summary>
    public SqliteConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// Runs on application startup to ensure the schema is ready.
    /// </summary>
    public void Initialize()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            -- 1. Searches Table
            CREATE TABLE IF NOT EXISTS searches (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                searched_at TEXT    NOT NULL,
                keywords    TEXT    NOT NULL,
                location    TEXT    NOT NULL DEFAULT '',
                country     TEXT
            );

            -- 2. Job Listings Table
            CREATE TABLE IF NOT EXISTS job_listings (
                id          TEXT    PRIMARY KEY,
                search_id   INTEGER NOT NULL REFERENCES searches(id),
                searched_at TEXT    NOT NULL,
                date_posted TEXT,
                country     TEXT,
                title       TEXT,
                company     TEXT,
                location    TEXT,
                city        TEXT,
                state       TEXT,
                job_type    TEXT,
                salary      TEXT,
                url         TEXT,
                source      TEXT,
                is_remote   TEXT,
                description TEXT
            );

            -- 3. Job Links Table
            CREATE TABLE IF NOT EXISTS job_links (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                url           TEXT    NOT NULL UNIQUE,
                source        TEXT    NOT NULL DEFAULT 'unknown',
                added_at      TEXT    NOT NULL,
                processed     INTEGER NOT NULL DEFAULT 0,
                processed_at  TEXT,
                error_message TEXT
            );

            -- 4. Kanban Jobs Table
            CREATE TABLE IF NOT EXISTS kanban_jobs (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                job_listing_id TEXT    NOT NULL UNIQUE REFERENCES job_listings(id),
                status         TEXT    NOT NULL DEFAULT 'Searched/Found',
                notes          TEXT,
                is_active      INTEGER NOT NULL DEFAULT 0,
                fail_type      TEXT,
                updated_at     TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_kanban_status ON kanban_jobs(status);

            -- 5. Kanban History Table
            CREATE TABLE IF NOT EXISTS kanban_history (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                kanban_id   INTEGER NOT NULL REFERENCES kanban_jobs(id),
                from_status TEXT,
                to_status   TEXT    NOT NULL,
                changed_at  TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_history_kanban_id ON kanban_history(kanban_id);

            -- 6. Kanban Notes Table
            CREATE TABLE IF NOT EXISTS kanban_notes (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                kanban_id  INTEGER NOT NULL REFERENCES kanban_jobs(id),
                note       TEXT    NOT NULL,
                created_at TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_notes_kanban_id ON kanban_notes(kanban_id);

            -- 7. Settings Table
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            INSERT OR IGNORE INTO settings (key, value) VALUES ('timezone', 'America/New_York');
            INSERT OR IGNORE INTO settings (key, value) VALUES ('search_keywords', 'Director of Software Engineering');

            -- 8. Prompts Log
            CREATE TABLE IF NOT EXISTS prompts_log (
                id       INTEGER PRIMARY KEY AUTOINCREMENT,
                sequence INTEGER NOT NULL,
                date     TEXT    NOT NULL,
                prompt   TEXT    NOT NULL,
                category TEXT    NOT NULL DEFAULT '',
                response TEXT    NOT NULL DEFAULT ''
            );

            -- 9. Code Stats Table
            CREATE TABLE IF NOT EXISTS code_stats (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                file_type  TEXT    NOT NULL,
                file_count INTEGER NOT NULL DEFAULT 0,
                line_count INTEGER NOT NULL DEFAULT 0,
                scanned_at TEXT    NOT NULL
            );

            -- 10. Job Timers Table
            CREATE TABLE IF NOT EXISTS job_timers (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                kanban_id        INTEGER NOT NULL REFERENCES kanban_jobs(id),
                duration_seconds INTEGER NOT NULL,
                created_at       TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_timers_kanban_id ON job_timers(kanban_id);

            -- 11. Useful Links Table
            CREATE TABLE IF NOT EXISTS useful_links (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                description TEXT    NOT NULL,
                url         TEXT    NOT NULL,
                added_at    TEXT    NOT NULL
            );
        ";

        cmd.ExecuteNonQuery();
    }
}