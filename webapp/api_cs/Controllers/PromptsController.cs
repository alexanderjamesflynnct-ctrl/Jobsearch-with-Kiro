using Microsoft.Data.Sqlite;

public static class PromptsController
{
    public static void MapPromptsEndpoints(this WebApplication app, Func<SqliteConnection> open)
    {
        // ── GET /prompts ──────────────────────────────────────────────────────────
        app.MapGet("/prompts", () =>
        {
            using var conn = open();
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
    }
}
