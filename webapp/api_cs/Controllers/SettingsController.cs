using Microsoft.Data.Sqlite;

public static class SettingsController
{
    public static void MapSettingsEndpoints(this WebApplication app, Func<SqliteConnection> open)
    {
        // ── GET /settings ────────────────────────────────────────────────────────
        app.MapGet("/settings", () =>
        {
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT key, value FROM settings";
            var dict = new Dictionary<string, string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) dict[reader.GetString(0)] = reader.GetString(1);
            return Results.Ok(dict);
        });

        // ── PATCH /settings ──────────────────────────────────────────────────────
        app.MapMethods("/settings", ["PATCH"], async (HttpRequest request) =>
        {
            var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            if (body == null) return Results.BadRequest(new { error = "Body required" });

            using var conn = open();
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
    }
}
