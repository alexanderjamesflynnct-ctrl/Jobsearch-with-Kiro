using Microsoft.Data.Sqlite;

public static class AdzunaController
{
    public static void MapAdzunaEndpoints(this WebApplication app, Func<SqliteConnection> open)
    {
        // ── POST /run-adzuna ──────────────────────────────────────────────────────
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
    }
}
