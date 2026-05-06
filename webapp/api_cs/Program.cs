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
            description TEXT
        );
        CREATE TABLE IF NOT EXISTS job_links (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            url          TEXT    NOT NULL UNIQUE,
            source       TEXT    NOT NULL DEFAULT 'unknown',
            added_at     TEXT    NOT NULL,
            processed    INTEGER NOT NULL DEFAULT 0,
            processed_at TEXT
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

// ── GET /jobs ────────────────────────────────────────────────────────────────
app.MapGet("/jobs", (
    string? keywords,
    string? country,
    string? source,
    string? job_type,
    string? is_remote,
    string? state,
    string? sort,
    string? order,
    int page = 1,
    int per_page = 25) =>
{
    var clauses = new List<string>();
    var parms   = new Dictionary<string, object>();

    if (!string.IsNullOrWhiteSpace(keywords))
    {
        clauses.Add("(title LIKE $kw OR company LIKE $kw OR description LIKE $kw)");
        parms["$kw"] = $"%{keywords}%";
    }
    if (!string.IsNullOrWhiteSpace(country))   { clauses.Add("country = $country");     parms["$country"]   = country; }
    if (!string.IsNullOrWhiteSpace(source))    { clauses.Add("source = $source");       parms["$source"]    = source; }
    if (!string.IsNullOrWhiteSpace(job_type))  { clauses.Add("job_type LIKE $jtype");   parms["$jtype"]     = $"%{job_type}%"; }
    if (!string.IsNullOrWhiteSpace(is_remote)) { clauses.Add("is_remote = $is_remote"); parms["$is_remote"] = is_remote; }
    if (!string.IsNullOrWhiteSpace(state))     { clauses.Add("state = $state");         parms["$state"]     = state; }
    var where  = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : "";
    var col    = sort switch { "date_posted" or "title" or "company" or "country" or "salary" or "source" or "state" or "job_type" => sort, _ => "searched_at" };
    var dir    = order?.ToLower() == "asc" ? "ASC" : "DESC";
    var offset = (page - 1) * per_page;

    using var conn = Open();
    conn.Open();

    // total count
    using var countCmd = conn.CreateCommand();
    countCmd.CommandText = $"SELECT COUNT(*) FROM job_listings {where}";
    foreach (var (k, v) in parms) countCmd.Parameters.AddWithValue(k, v);
    var total = Convert.ToInt32(countCmd.ExecuteScalar());

    // paged results
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"SELECT * FROM job_listings {where} ORDER BY {col} {dir} LIMIT $limit OFFSET $offset";
    foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v);
    cmd.Parameters.AddWithValue("$limit",  per_page);
    cmd.Parameters.AddWithValue("$offset", offset);

    var jobs = new List<Dictionary<string, object?>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var row = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        jobs.Add(row);
    }

    return Results.Ok(new { total, page, per_page, jobs });
});

// ── POST /jobs/manual ─────────────────────────────────────────────────────────
app.MapPost("/jobs/manual", async (HttpRequest request) =>
{
    var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    if (body == null) return Results.BadRequest(new { error = "Body required" });

    var id        = $"manual_{Guid.NewGuid():N}".Substring(0, 20);
    var now       = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    var title     = body.GetValueOrDefault("title")?.Trim() ?? "";
    var company   = body.GetValueOrDefault("company")?.Trim() ?? "";
    var location  = body.GetValueOrDefault("location")?.Trim() ?? "";
    var city      = body.GetValueOrDefault("city")?.Trim() ?? "";
    var state     = body.GetValueOrDefault("state")?.Trim() ?? "";
    var country   = body.GetValueOrDefault("country")?.Trim() ?? "";
    var jobType   = body.GetValueOrDefault("job_type")?.Trim() ?? "";
    var salary    = body.GetValueOrDefault("salary")?.Trim() ?? "";
    var url       = body.GetValueOrDefault("url")?.Trim() ?? "";
    var source    = body.GetValueOrDefault("source")?.Trim() ?? "manual";
    var isRemote  = body.GetValueOrDefault("is_remote")?.Trim() ?? "";
    var desc      = body.GetValueOrDefault("description")?.Trim() ?? "";

    using var conn = Open();
    conn.Open();

    // Create a search record for manual entries
    using var searchCmd = conn.CreateCommand();
    searchCmd.CommandText = """
        INSERT INTO searches (searched_at, keywords, location, country)
        VALUES ($at, 'Manual Entry', '', $country)
        """;
    searchCmd.Parameters.AddWithValue("$at", now);
    searchCmd.Parameters.AddWithValue("$country", country);
    searchCmd.ExecuteNonQuery();

    using var lastIdCmd = conn.CreateCommand();
    lastIdCmd.CommandText = "SELECT last_insert_rowid()";
    var searchId = Convert.ToInt64(lastIdCmd.ExecuteScalar());

    // Insert the job listing
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        INSERT INTO job_listings
            (id, search_id, searched_at, date_posted, country, title, company,
             location, city, state, job_type, salary, url, source, is_remote, description)
        VALUES ($id, $sid, $at, $at, $country, $title, $company,
                $loc, $city, $state, $jtype, $salary, $url, $source, $remote, $desc)
        """;
    cmd.Parameters.AddWithValue("$id",      id);
    cmd.Parameters.AddWithValue("$sid",     searchId);
    cmd.Parameters.AddWithValue("$at",      now);
    cmd.Parameters.AddWithValue("$country", country);
    cmd.Parameters.AddWithValue("$title",   title);
    cmd.Parameters.AddWithValue("$company", company);
    cmd.Parameters.AddWithValue("$loc",     location);
    cmd.Parameters.AddWithValue("$city",    city);
    cmd.Parameters.AddWithValue("$state",   state);
    cmd.Parameters.AddWithValue("$jtype",   jobType);
    cmd.Parameters.AddWithValue("$salary",  salary);
    cmd.Parameters.AddWithValue("$url",     url);
    cmd.Parameters.AddWithValue("$source",  source);
    cmd.Parameters.AddWithValue("$remote",  string.IsNullOrEmpty(isRemote) ? DBNull.Value : isRemote);
    cmd.Parameters.AddWithValue("$desc",    desc);
    cmd.ExecuteNonQuery();

    // Auto-create kanban card
    using var kanbanCmd = conn.CreateCommand();
    kanbanCmd.CommandText = "INSERT INTO kanban_jobs (job_listing_id, status, updated_at) VALUES ($id, 'Searched/Found', $at)";
    kanbanCmd.Parameters.AddWithValue("$id", id);
    kanbanCmd.Parameters.AddWithValue("$at", now);
    kanbanCmd.ExecuteNonQuery();

    return Results.Ok(new { message = "Job added", id });
});

// ── PATCH /jobs/{id}/country ──────────────────────────────────────────────────
app.MapMethods("/jobs/{id}/country", ["PATCH"], async (string id, HttpRequest request) =>
{
    var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    var country = body?.GetValueOrDefault("country")?.Trim() ?? "";
    if (string.IsNullOrEmpty(country))
        return Results.BadRequest(new { error = "country is required" });

    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE job_listings SET country = $country WHERE id = $id";
    cmd.Parameters.AddWithValue("$country", country);
    cmd.Parameters.AddWithValue("$id", id);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Updated" });
});

// ── DELETE /jobs/{id} ────────────────────────────────────────────────────────
app.MapDelete("/jobs/{id}", (string id) =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM job_listings WHERE id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    var rows = cmd.ExecuteNonQuery();
    return rows > 0 ? Results.Ok(new { message = "Deleted" }) : Results.NotFound();
});

// ── GET /countries ───────────────────────────────────────────────────────────
app.MapGet("/countries", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT DISTINCT country FROM job_listings WHERE country IS NOT NULL ORDER BY country";
    var list = new List<string>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) list.Add(reader.GetString(0));
    return Results.Ok(list);
});

// ── GET /sources ──────────────────────────────────────────────────────────────
app.MapGet("/sources", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT DISTINCT source FROM job_listings WHERE source IS NOT NULL ORDER BY source";
    var list = new List<string>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) list.Add(reader.GetString(0));
    return Results.Ok(list);
});

// ── GET /job-types ────────────────────────────────────────────────────────────
app.MapGet("/job-types", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT DISTINCT job_type FROM job_listings WHERE job_type IS NOT NULL AND job_type != '' ORDER BY job_type";
    var list = new List<string>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) list.Add(reader.GetString(0));
    return Results.Ok(list);
});

// ── GET /states ───────────────────────────────────────────────────────────────
app.MapGet("/states", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT DISTINCT state FROM job_listings WHERE state IS NOT NULL AND state != '' ORDER BY state";
    var list = new List<string>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) list.Add(reader.GetString(0));
    return Results.Ok(list);
});

// ── GET /stats ───────────────────────────────────────────────────────────────
app.MapGet("/stats", () =>
{
    using var conn = Open();
    conn.Open();

    using var c1 = conn.CreateCommand();
    c1.CommandText = "SELECT COUNT(*) FROM job_listings";
    var totalJobs = Convert.ToInt32(c1.ExecuteScalar());

    using var c2 = conn.CreateCommand();
    c2.CommandText = "SELECT COUNT(*) FROM searches";
    var totalSearches = Convert.ToInt32(c2.ExecuteScalar());

    using var c3 = conn.CreateCommand();
    c3.CommandText = "SELECT searched_at FROM searches ORDER BY searched_at DESC LIMIT 1";
    var lastSearch = c3.ExecuteScalar()?.ToString();

    using var c4 = conn.CreateCommand();
    c4.CommandText = "SELECT country, COUNT(*) as n FROM job_listings GROUP BY country ORDER BY n DESC";
    var byCountry = new List<Dictionary<string, object>>();
    using var r4 = c4.ExecuteReader();
    while (r4.Read())
        byCountry.Add(new() { ["country"] = r4.GetString(0), ["n"] = r4.GetInt32(1) });

    return Results.Ok(new
    {
        total_jobs     = totalJobs,
        total_searches = totalSearches,
        last_search    = lastSearch,
        by_country     = byCountry
    });
});

// ── GET /prompts ──────────────────────────────────────────────────────────────
app.MapGet("/prompts", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT sequence, date, prompt, category, response FROM prompts_log ORDER BY sequence ASC";
    var rows = new List<Dictionary<string, object?>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        rows.Add(new() {
            ["sequence"] = reader.GetInt32(0),
            ["date"]     = reader.GetString(1),
            ["prompt"]   = reader.GetString(2),
            ["category"] = reader.GetString(3),
            ["response"] = reader.GetString(4),
        });
    }
    return Results.Ok(rows);
});

// ── GET /code-stats ───────────────────────────────────────────────────────────
app.MapGet("/code-stats", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT file_type, file_count, line_count, scanned_at FROM code_stats ORDER BY line_count DESC";
    var rows = new List<Dictionary<string, object>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
        rows.Add(new() {
            ["file_type"]  = reader.GetString(0),
            ["file_count"] = reader.GetInt32(1),
            ["line_count"] = reader.GetInt32(2),
            ["scanned_at"] = reader.GetString(3),
        });

    var totalFiles = rows.Sum(r => (int)r["file_count"]);
    var totalLines = rows.Sum(r => (int)r["line_count"]);

    return Results.Ok(new { total_files = totalFiles, total_lines = totalLines, breakdown = rows });
});

// ── POST /code-stats/scan ────────────────────────────────────────────────────
app.MapPost("/code-stats/scan", async () =>
{
    var scriptPath = Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SearchCode", "scan_code_stats.py"));

    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName               = "py",
        Arguments              = $"-3.14 \"{scriptPath}\"",
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        WorkingDirectory       = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..")),
    };

    var proc   = System.Diagnostics.Process.Start(psi)!;
    var stdout = await proc.StandardOutput.ReadToEndAsync();
    var stderr = await proc.StandardError.ReadToEndAsync();
    await proc.WaitForExitAsync();

    if (proc.ExitCode != 0)
        return Results.Json(new { success = false, output = stdout, errors = stderr }, statusCode: 500);

    return Results.Ok(new { success = true, output = stdout });
});

// ── GET /settings ────────────────────────────────────────────────────────────
app.MapGet("/settings", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT key, value FROM settings";
    var dict = new Dictionary<string, string>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) dict[reader.GetString(0)] = reader.GetString(1);
    return Results.Ok(dict);
});

// ── PATCH /settings ──────────────────────────────────────────────────────────
app.MapMethods("/settings", ["PATCH"], async (HttpRequest request) =>
{
    var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    if (body == null) return Results.BadRequest(new { error = "Body required" });

    using var conn = Open();
    conn.Open();
    foreach (var (key, value) in body)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO settings (key, value) VALUES ($k, $v) ON CONFLICT(key) DO UPDATE SET value = $v";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }
    return Results.Ok(new { message = "Settings saved" });
});

// ── GET /stats/applied-today ──────────────────────────────────────────────────
app.MapGet("/stats/applied-today", () =>
{
    using var conn = Open();
    conn.Open();

    // Get user timezone from settings
    using var tzCmd = conn.CreateCommand();
    tzCmd.CommandText = "SELECT value FROM settings WHERE key = 'timezone'";
    var tzId = tzCmd.ExecuteScalar()?.ToString() ?? "America/New_York";

    // Calculate start/end of today in user's timezone, converted to UTC for comparison
    string todayStart, todayEnd;
    try
    {
        var tz  = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var startLocal = now.Date;                          // midnight today in user's tz
        var endLocal   = startLocal.AddDays(1);             // midnight tomorrow in user's tz
        var startUtc   = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
        var endUtc     = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
        todayStart = startUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        todayEnd   = endUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
    catch
    {
        // Fallback to UTC day
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        todayStart = today + "T00:00:00Z";
        todayEnd   = today + "T23:59:59Z";
    }

    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(DISTINCT kanban_id) FROM kanban_history WHERE to_status = 'Applied' AND changed_at >= $start AND changed_at < $end";
    cmd.Parameters.AddWithValue("$start", todayStart);
    cmd.Parameters.AddWithValue("$end",   todayEnd);
    var count = Convert.ToInt32(cmd.ExecuteScalar());
    return Results.Ok(new { count, todayStart, todayEnd });
});

// ── GET /stats/outcomes ───────────────────────────────────────────────────────
// For jobs that have ever been Applied: what's their current status/fail_type?
app.MapGet("/stats/outcomes", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT
            CASE
                WHEN k.status = 'Failed' THEN COALESCE(k.fail_type, 'Unspecified')
                ELSE k.status
            END as outcome,
            COUNT(*) as n
        FROM kanban_jobs k
        WHERE k.id IN (SELECT DISTINCT kanban_id FROM kanban_history WHERE to_status = 'Applied')
          AND k.status IN ('Applied', 'Interviewed', 'Accepted Job', 'Failed')
        GROUP BY outcome
        ORDER BY n DESC
        """;
    var rows = new List<Dictionary<string, object>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
        rows.Add(new() { ["outcome"] = reader.GetString(0), ["count"] = reader.GetInt32(1) });
    return Results.Ok(rows);
});

// ── GET /stats/applied-count ──────────────────────────────────────────────────
app.MapGet("/stats/applied-count", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(DISTINCT kanban_id) FROM kanban_history WHERE to_status = 'Applied'";
    var count = Convert.ToInt32(cmd.ExecuteScalar());
    return Results.Ok(new { count });
});

// ── GET /kanban ───────────────────────────────────────────────────────────────
// Returns all kanban cards joined with job listing details, grouped by status
app.MapGet("/kanban", () =>
{
    using var conn = Open();
    conn.Open();

    // Ensure all existing job_listings have a kanban row
    using var sync = conn.CreateCommand();
    sync.CommandText = """
        INSERT OR IGNORE INTO kanban_jobs (job_listing_id, status, updated_at)
        SELECT id, 'Searched/Found', searched_at FROM job_listings
        WHERE id NOT IN (SELECT job_listing_id FROM kanban_jobs)
        """;
    sync.ExecuteNonQuery();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT k.id, k.job_listing_id, k.status, k.notes, k.is_active, k.fail_type, k.updated_at,
               j.title, j.company, j.location, j.state, j.country,
               j.salary, j.job_type, j.is_remote, j.source, j.url, j.date_posted
        FROM kanban_jobs k
        JOIN job_listings j ON j.id = k.job_listing_id
        ORDER BY k.updated_at DESC
        """;
    var cards = new List<Dictionary<string, object?>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var row = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        cards.Add(row);
    }
    return Results.Ok(cards);
});

// ── PATCH /kanban/{id}/status ─────────────────────────────────────────────────
app.MapMethods("/kanban/{id:int}/status", ["PATCH"], async (int id, HttpRequest request) =>
{
    var body   = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    var status = body?.GetValueOrDefault("status")?.Trim() ?? "";
    if (string.IsNullOrEmpty(status)) return Results.BadRequest(new { error = "status required" });

    var updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    using var conn = Open();
    conn.Open();

    // Get current status for history log
    using var getCmd = conn.CreateCommand();
    getCmd.CommandText = "SELECT status FROM kanban_jobs WHERE id = $id";
    getCmd.Parameters.AddWithValue("$id", id);
    var fromStatus = getCmd.ExecuteScalar()?.ToString();

    // Update status
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE kanban_jobs SET status = $s, updated_at = $at WHERE id = $id";
    cmd.Parameters.AddWithValue("$s",  status);
    cmd.Parameters.AddWithValue("$at", updatedAt);
    cmd.Parameters.AddWithValue("$id", id);
    cmd.ExecuteNonQuery();

    // Clear active flag if moving out of Searched/Found
    if (fromStatus == "Searched/Found" && status != "Searched/Found")
    {
        using var clearActive = conn.CreateCommand();
        clearActive.CommandText = "UPDATE kanban_jobs SET is_active = 0 WHERE id = $id";
        clearActive.Parameters.AddWithValue("$id", id);
        clearActive.ExecuteNonQuery();
    }

    // Log the change
    using var logCmd = conn.CreateCommand();
    logCmd.CommandText = """
        INSERT INTO kanban_history (kanban_id, from_status, to_status, changed_at)
        VALUES ($kid, $from, $to, $at)
        """;
    logCmd.Parameters.AddWithValue("$kid",  id);
    logCmd.Parameters.AddWithValue("$from", fromStatus ?? "");
    logCmd.Parameters.AddWithValue("$to",   status);
    logCmd.Parameters.AddWithValue("$at",   updatedAt);
    logCmd.ExecuteNonQuery();

    // If moving to Failed from Searched/Found or Researching, also record an Applied step
    if (status == "Failed" && (fromStatus == "Searched/Found" || fromStatus == "Researching"))
    {
        using var appliedLog = conn.CreateCommand();
        appliedLog.CommandText = """
            INSERT INTO kanban_history (kanban_id, from_status, to_status, changed_at)
            VALUES ($kid, $from, 'Applied', $at)
            """;
        appliedLog.Parameters.AddWithValue("$kid",  id);
        appliedLog.Parameters.AddWithValue("$from", fromStatus);
        appliedLog.Parameters.AddWithValue("$at",   updatedAt);
        appliedLog.ExecuteNonQuery();
    }

    return Results.Ok(new { message = "Updated", status, updated_at = updatedAt });
});

// ── GET /kanban/{id}/history ──────────────────────────────────────────────────
app.MapGet("/kanban/{id:int}/history", (int id) =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT from_status, to_status, changed_at
        FROM kanban_history
        WHERE kanban_id = $id
        ORDER BY changed_at DESC
        """;
    cmd.Parameters.AddWithValue("$id", id);
    var rows = new List<Dictionary<string, object?>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
        rows.Add(new() {
            ["from_status"] = reader.IsDBNull(0) ? null : reader.GetString(0),
            ["to_status"]   = reader.GetString(1),
            ["changed_at"]  = reader.GetString(2),
        });
    return Results.Ok(rows);
});

// ── PATCH /kanban/{id}/fail-type ─────────────────────────────────────────────
app.MapMethods("/kanban/{id:int}/fail-type", ["PATCH"], async (int id, HttpRequest request) =>
{
    var body     = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    var failType = body?.GetValueOrDefault("fail_type")?.Trim() ?? "";
    var updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE kanban_jobs SET fail_type = $ft, updated_at = $at WHERE id = $id";
    cmd.Parameters.AddWithValue("$ft", string.IsNullOrEmpty(failType) ? DBNull.Value : failType);
    cmd.Parameters.AddWithValue("$at", updatedAt);
    cmd.Parameters.AddWithValue("$id", id);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Updated" });
});

// ── POST /kanban/{id}/active ──────────────────────────────────────────────────
app.MapPost("/kanban/{id:int}/active", (int id) =>
{
    using var conn = Open();
    conn.Open();

    // Check if already active — if so, deselect it
    using var checkCmd = conn.CreateCommand();
    checkCmd.CommandText = "SELECT is_active FROM kanban_jobs WHERE id = $id";
    checkCmd.Parameters.AddWithValue("$id", id);
    var current = Convert.ToInt32(checkCmd.ExecuteScalar());

    // Clear all active flags first
    using var clearCmd = conn.CreateCommand();
    clearCmd.CommandText = "UPDATE kanban_jobs SET is_active = 0";
    clearCmd.ExecuteNonQuery();

    // Toggle: if it wasn't active, set it; if it was, leave cleared
    if (current == 0)
    {
        using var setCmd = conn.CreateCommand();
        setCmd.CommandText = "UPDATE kanban_jobs SET is_active = 1 WHERE id = $id";
        setCmd.Parameters.AddWithValue("$id", id);
        setCmd.ExecuteNonQuery();
    }

    return Results.Ok(new { message = current == 0 ? "Set as active" : "Deselected" });
});

// ── GET /kanban/{id}/notes ────────────────────────────────────────────────────
app.MapGet("/kanban/{id:int}/notes", (int id) =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT id, note, created_at FROM kanban_notes
        WHERE kanban_id = $id ORDER BY created_at DESC
        """;
    cmd.Parameters.AddWithValue("$id", id);
    var rows = new List<Dictionary<string, object?>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
        rows.Add(new() {
            ["id"]         = reader.GetInt32(0),
            ["note"]       = reader.GetString(1),
            ["created_at"] = reader.GetString(2),
        });
    return Results.Ok(rows);
});

// ── POST /kanban/{id}/notes ───────────────────────────────────────────────────
app.MapPost("/kanban/{id:int}/notes", async (int id, HttpRequest request) =>
{
    var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    var note = body?.GetValueOrDefault("note")?.Trim() ?? "";
    if (string.IsNullOrEmpty(note)) return Results.BadRequest(new { error = "note is required" });

    var createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT INTO kanban_notes (kanban_id, note, created_at) VALUES ($id, $note, $at)";
    cmd.Parameters.AddWithValue("$id",   id);
    cmd.Parameters.AddWithValue("$note", note);
    cmd.Parameters.AddWithValue("$at",   createdAt);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Note saved", created_at = createdAt });
});

// ── DELETE /kanban/notes/{id} ─────────────────────────────────────────────────
app.MapDelete("/kanban/notes/{id:int}", (int id) =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM kanban_notes WHERE id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Deleted" });
});

// ── PATCH /kanban/{id}/notes ──────────────────────────────────────────────────
app.MapMethods("/kanban/{id:int}/notes", ["PATCH"], async (int id, HttpRequest request) =>
{
    var body  = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    var notes = body?.GetValueOrDefault("notes") ?? "";
    var updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE kanban_jobs SET notes = $n, updated_at = $at WHERE id = $id";
    cmd.Parameters.AddWithValue("$n",  notes);
    cmd.Parameters.AddWithValue("$at", updatedAt);
    cmd.Parameters.AddWithValue("$id", id);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Notes saved" });
});

// ── POST /run-adzuna ──────────────────────────────────────────────────────────
app.MapPost("/run-adzuna", async (HttpRequest request) =>
{
    var body     = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    var keywords = body?.GetValueOrDefault("keywords")?.Trim() ?? "Director of Software Engineering";

    var scriptPath = Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SearchCode", "adzuna_jobs.py"));

    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName               = "py",
        Arguments              = $"-3.14 \"{scriptPath}\" \"{keywords}\"",
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        WorkingDirectory       = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..")),
    };

    var proc   = System.Diagnostics.Process.Start(psi)!;
    var stdout = await proc.StandardOutput.ReadToEndAsync();
    var stderr = await proc.StandardError.ReadToEndAsync();
    await proc.WaitForExitAsync();

    if (proc.ExitCode != 0)
        return Results.Json(new { success = false, output = stdout, errors = stderr }, statusCode: 500);

    return Results.Ok(new { success = true, output = stdout, errors = stderr });
});

// ── GET /useful-links ─────────────────────────────────────────────────────────
app.MapGet("/useful-links", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT * FROM useful_links ORDER BY added_at DESC";
    var list = new List<Dictionary<string, object?>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var row = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        list.Add(row);
    }
    return Results.Ok(list);
});

// ── POST /useful-links ────────────────────────────────────────────────────────
app.MapPost("/useful-links", async (HttpRequest request) =>
{
    var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    var url  = body?.GetValueOrDefault("url")?.Trim() ?? "";
    var desc = body?.GetValueOrDefault("description")?.Trim() ?? "";

    if (string.IsNullOrEmpty(url))   return Results.BadRequest(new { error = "url is required" });
    if (string.IsNullOrEmpty(desc))  return Results.BadRequest(new { error = "description is required" });

    var addedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT INTO useful_links (description, url, added_at) VALUES ($desc, $url, $at)";
    cmd.Parameters.AddWithValue("$desc", desc);
    cmd.Parameters.AddWithValue("$url",  url);
    cmd.Parameters.AddWithValue("$at",   addedAt);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Link added", added_at = addedAt });
});

// ── PATCH /useful-links/{id} ──────────────────────────────────────────────────
app.MapMethods("/useful-links/{id:int}", ["PATCH"], async (int id, HttpRequest request) =>
{
    var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    var url  = body?.GetValueOrDefault("url")?.Trim();
    var desc = body?.GetValueOrDefault("description")?.Trim();

    using var conn = Open();
    conn.Open();
    if (!string.IsNullOrEmpty(url))
    {
        using var c = conn.CreateCommand();
        c.CommandText = "UPDATE useful_links SET url = $url WHERE id = $id";
        c.Parameters.AddWithValue("$url", url); c.Parameters.AddWithValue("$id", id);
        c.ExecuteNonQuery();
    }
    if (!string.IsNullOrEmpty(desc))
    {
        using var c = conn.CreateCommand();
        c.CommandText = "UPDATE useful_links SET description = $desc WHERE id = $id";
        c.Parameters.AddWithValue("$desc", desc); c.Parameters.AddWithValue("$id", id);
        c.ExecuteNonQuery();
    }
    return Results.Ok(new { message = "Updated" });
});

// ── DELETE /useful-links/{id} ─────────────────────────────────────────────────
app.MapDelete("/useful-links/{id:int}", (int id) =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM useful_links WHERE id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Deleted" });
});

// ── GET /links ───────────────────────────────────────────────────────────────
app.MapGet("/links", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT * FROM job_links ORDER BY added_at DESC";
    var list = new List<Dictionary<string, object?>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var row = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        list.Add(row);
    }
    return Results.Ok(list);
});

// ── POST /links ──────────────────────────────────────────────────────────────
app.MapPost("/links", async (HttpRequest request) =>
{
    var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    var url  = body?.GetValueOrDefault("url")?.Trim() ?? "";

    if (string.IsNullOrEmpty(url))
        return Results.BadRequest(new { error = "url is required" });

    var isLinkedIn   = url.Contains("linkedin.com/jobs");
    var isIndeed     = url.Contains("indeed.com");
    var isGlassdoor  = url.Contains("glassdoor.com");
    var isZipRecruiter = url.Contains("ziprecruiter.com");

    if (!isLinkedIn && !isIndeed && !isGlassdoor && !isZipRecruiter)
        return Results.BadRequest(new { error = "Only LinkedIn, Indeed, Glassdoor and ZipRecruiter job URLs are supported" });

    var source = isLinkedIn ? "linkedin"
               : isIndeed   ? "indeed"
               : isGlassdoor ? "glassdoor"
               : "ziprecruiter";
    var addedAt  = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        INSERT INTO job_links (url, source, added_at)
        VALUES ($url, $source, $added_at)
        ON CONFLICT(url) DO NOTHING
        """;
    cmd.Parameters.AddWithValue("$url",      url);
    cmd.Parameters.AddWithValue("$source",   source);
    cmd.Parameters.AddWithValue("$added_at", addedAt);
    var rows = cmd.ExecuteNonQuery();

    return rows > 0
        ? Results.Ok(new { message = "Link added", url, source, added_at = addedAt })
        : Results.Ok(new { message = "Link already exists", url });
});

// ── DELETE /links/{id} ───────────────────────────────────────────────────────
app.MapDelete("/links/{id:int}", (int id) =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM job_links WHERE id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Deleted" });
});

// ── POST /links/process ──────────────────────────────────────────────────────
app.MapPost("/links/process", async (HttpContext ctx) =>
{
    using var conn = Open();
    conn.Open();

    using var selectCmd = conn.CreateCommand();
    selectCmd.CommandText = "SELECT id, url FROM job_links WHERE processed = 0";
    var pending = new List<(int id, string url)>();
    using var reader = selectCmd.ExecuteReader();
    while (reader.Read())
        pending.Add((reader.GetInt32(0), reader.GetString(1)));

    if (pending.Count == 0)
        return Results.Ok(new { message = "No unprocessed links", processed = 0, output = "", errors = "" });

    var tmpFile = Path.GetTempFileName();
    await File.WriteAllLinesAsync(tmpFile, pending.Select(p => p.url));

    var scriptPath = Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SearchCode", "linkedin_import.py"));

    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName               = "py",
        Arguments              = $"-3.14 \"{scriptPath}\" --file \"{tmpFile}\"",
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
    };

    var proc   = System.Diagnostics.Process.Start(psi)!;
    var stdout = await proc.StandardOutput.ReadToEndAsync();
    var stderr = await proc.StandardError.ReadToEndAsync();
    await proc.WaitForExitAsync();
    File.Delete(tmpFile);

    if (proc.ExitCode != 0)
        return Results.Json(new { message = "Import script failed", processed = 0, output = stdout, errors = stderr }, statusCode: 500);

    var processedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    foreach (var (id, _) in pending)
    {
        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE job_links SET processed = 1, processed_at = $at WHERE id = $id";
        upd.Parameters.AddWithValue("$at", processedAt);
        upd.Parameters.AddWithValue("$id", id);
        upd.ExecuteNonQuery();
    }

    // Parse output into per-job lines for the UI
    var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                      .Select(l => l.Trim())
                      .Where(l => l.Length > 0)
                      .ToList();

    return Results.Ok(new
    {
        message   = $"Processed {pending.Count} link(s)",
        processed = pending.Count,
        output    = stdout,
        lines,
        errors    = stderr,
    });
});

// ── DELETE /links/all ────────────────────────────────────────────────────────
app.MapDelete("/links/all", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM job_links WHERE processed = 1";
    var rows = cmd.ExecuteNonQuery();
    return Results.Ok(new { message = $"Deleted {rows} imported link(s)" });
});

// ── POST /links/{id}/reset ───────────────────────────────────────────────────
app.MapPost("/links/{id:int}/reset", (int id) =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE job_links SET processed = 0, processed_at = NULL WHERE id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Reset to pending" });
});

// ── POST /links/reset-all ────────────────────────────────────────────────────
app.MapPost("/links/reset-all", () =>
{
    using var conn = Open();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE job_links SET processed = 0, processed_at = NULL WHERE processed = 1";
    var rows = cmd.ExecuteNonQuery();
    return Results.Ok(new { message = $"Reset {rows} link(s) to pending" });
});

app.Run("http://localhost:8000");