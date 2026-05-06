using Microsoft.Data.Sqlite;

public static class CodeStatsController
{
    public static void MapCodeStatsEndpoints(this WebApplication app, Func<SqliteConnection> open)
    {
        // ── GET /code-stats ───────────────────────────────────────────────────────
        app.MapGet("/code-stats", () =>
        {
            using var conn = open();
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

        // ── POST /code-stats/scan ────────────────────────────────────────────────
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
    }
}
