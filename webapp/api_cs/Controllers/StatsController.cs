using Microsoft.Data.Sqlite;

public static class StatsController
{
    public static void MapStatsEndpoints(this WebApplication app, Func<SqliteConnection> open)
    {
        // ── GET /stats ───────────────────────────────────────────────────────────
        app.MapGet("/stats", () =>
        {
            using var conn = open();
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

        // ── GET /stats/applied-count ──────────────────────────────────────────────
        app.MapGet("/stats/applied-count", () =>
        {
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(DISTINCT kanban_id) FROM kanban_history WHERE to_status = 'Applied'";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return Results.Ok(new { count });
        });

        // ── GET /stats/applied-today ──────────────────────────────────────────────
        app.MapGet("/stats/applied-today", () =>
        {
            using var conn = open();
            conn.Open();

            using var tzCmd = conn.CreateCommand();
            tzCmd.CommandText = "SELECT value FROM settings WHERE key = 'timezone'";
            var tzId = tzCmd.ExecuteScalar()?.ToString() ?? "America/New_York";

            string todayStart, todayEnd;
            try
            {
                var tz  = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                var startLocal = now.Date;
                var endLocal   = startLocal.AddDays(1);
                var startUtc   = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
                var endUtc     = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
                todayStart = startUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
                todayEnd   = endUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
            }
            catch
            {
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

        // ── GET /stats/outcomes ───────────────────────────────────────────────────
        app.MapGet("/stats/outcomes", () =>
        {
            using var conn = open();
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
    }
}
