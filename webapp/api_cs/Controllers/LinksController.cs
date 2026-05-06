using Microsoft.Data.Sqlite;

public static class LinksController
{
    public static void MapLinksEndpoints(this WebApplication app, Func<SqliteConnection> open)
    {
        // ── GET /links ───────────────────────────────────────────────────────────
        app.MapGet("/links", () =>
        {
            using var conn = open();
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

        // ── POST /links ──────────────────────────────────────────────────────────
        app.MapPost("/links", async (HttpRequest request) =>
        {
            var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            var url  = body?.GetValueOrDefault("url")?.Trim() ?? "";

            if (string.IsNullOrEmpty(url))
                return Results.BadRequest(new { error = "url is required" });

            var isLinkedIn     = url.Contains("linkedin.com/jobs");
            var isIndeed       = url.Contains("indeed.com");
            var isGlassdoor    = url.Contains("glassdoor.com");
            var isZipRecruiter = url.Contains("ziprecruiter.com");

            if (!isLinkedIn && !isIndeed && !isGlassdoor && !isZipRecruiter)
                return Results.BadRequest(new { error = "Only LinkedIn, Indeed, Glassdoor and ZipRecruiter job URLs are supported" });

            var source = isLinkedIn ? "linkedin"
                       : isIndeed   ? "indeed"
                       : isGlassdoor ? "glassdoor"
                       : "ziprecruiter";
            var addedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            using var conn = open();
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

        // ── DELETE /links/{id} ───────────────────────────────────────────────────
        app.MapDelete("/links/{id:int}", (int id) =>
        {
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM job_links WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            return Results.Ok(new { message = "Deleted" });
        });

        // ── POST /links/process ──────────────────────────────────────────────────
        app.MapPost("/links/process", async (HttpContext ctx) =>
        {
            using var conn = open();
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

        // ── POST /links/{id}/reset ───────────────────────────────────────────────
        app.MapPost("/links/{id:int}/reset", (int id) =>
        {
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE job_links SET processed = 0, processed_at = NULL WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            return Results.Ok(new { message = "Reset to pending" });
        });

        // ── POST /links/reset-all ────────────────────────────────────────────────
        app.MapPost("/links/reset-all", () =>
        {
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE job_links SET processed = 0, processed_at = NULL WHERE processed = 1";
            var rows = cmd.ExecuteNonQuery();
            return Results.Ok(new { message = $"Reset {rows} link(s) to pending" });
        });

        // ── DELETE /links/all ────────────────────────────────────────────────────
        app.MapDelete("/links/all", () =>
        {
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM job_links WHERE processed = 1";
            var rows = cmd.ExecuteNonQuery();
            return Results.Ok(new { message = $"Deleted {rows} imported link(s)" });
        });
    }
}
