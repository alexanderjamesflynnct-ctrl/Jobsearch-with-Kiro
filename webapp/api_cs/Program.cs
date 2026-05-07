using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
        p.WithOrigins("http://localhost:5173")
         .AllowAnyMethod()
         .AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// Resolve DB path relative to this source file's location
var dbPath = Path.GetFullPath(
    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SearchCode", "jobs.db"));

SqliteConnection Open() => new($"Data Source={dbPath}");

// Ensure all tables exist on every startup — safe to run repeatedly
using (var initConn = Open())
{
    initConn.Open();
    using var initCmd = initConn.CreateCommand();
    initCmd.CommandText = """
        CREATE TABLE IF NOT EXISTS searches (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            searched_at TEXT    NOT NULL,
            keywords    TEXT    NOT NULL,
            location    TEXT    NOT NULL DEFAULT '',
            country     TEXT
        );
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
        CREATE TABLE IF NOT EXISTS job_links (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            url           TEXT    NOT NULL UNIQUE,
            source        TEXT    NOT NULL DEFAULT 'unknown',
            added_at      TEXT    NOT NULL,
            processed     INTEGER NOT NULL DEFAULT 0,
            processed_at  TEXT,
            error_message TEXT
        );
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

        CREATE TABLE IF NOT EXISTS kanban_history (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            kanban_id   INTEGER NOT NULL REFERENCES kanban_jobs(id),
            from_status TEXT,
            to_status   TEXT    NOT NULL,
            changed_at  TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_history_kanban_id ON kanban_history(kanban_id);

        CREATE TABLE IF NOT EXISTS kanban_notes (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            kanban_id  INTEGER NOT NULL REFERENCES kanban_jobs(id),
            note       TEXT    NOT NULL,
            created_at TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_notes_kanban_id ON kanban_notes(kanban_id);

        CREATE TABLE IF NOT EXISTS settings (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        INSERT OR IGNORE INTO settings (key, value) VALUES ('timezone', 'America/New_York');
        INSERT OR IGNORE INTO settings (key, value) VALUES ('search_keywords', 'Director of Software Engineering');

        CREATE TABLE IF NOT EXISTS prompts_log (
            id       INTEGER PRIMARY KEY AUTOINCREMENT,
            sequence INTEGER NOT NULL,
            date     TEXT    NOT NULL,
            prompt   TEXT    NOT NULL,
            category TEXT    NOT NULL DEFAULT '',
            response TEXT    NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS code_stats (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            file_type  TEXT    NOT NULL,
            file_count INTEGER NOT NULL DEFAULT 0,
            line_count INTEGER NOT NULL DEFAULT 0,
            scanned_at TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS useful_links (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            description TEXT    NOT NULL,
            url         TEXT    NOT NULL,
            added_at    TEXT    NOT NULL
        );
        """;
    initCmd.ExecuteNonQuery();
}

// Register all controller endpoints
app.MapJobsEndpoints(Open);
app.MapKanbanEndpoints(Open);
app.MapLinksEndpoints(Open);
app.MapStatsEndpoints(Open);
app.MapUsefulLinksEndpoints(Open);
app.MapSettingsEndpoints(Open);
app.MapPromptsEndpoints(Open);
app.MapCodeStatsEndpoints(Open);
app.MapAdzunaEndpoints(Open);

app.Run("http://localhost:8000");
