using Microsoft.Data.Sqlite;

public static class UsefulLinksController
{
    public static void MapUsefulLinksEndpoints(this WebApplication app, Func<SqliteConnection> open)
    {
        // ── GET /useful-links ─────────────────────────────────────────────────────
        app.MapGet("/useful-links", () =>
        {
            using var conn = open();
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

        // ── POST /useful-links ────────────────────────────────────────────────────
        app.MapPost("/useful-links", async (HttpRequest request) =>
        {
            var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            var url  = body?.GetValueOrDefault("url")?.Trim() ?? "";
            var desc = body?.GetValueOrDefault("description")?.Trim() ?? "";

            if (string.IsNullOrEmpty(url))   return Results.BadRequest(new { error = "url is required" });
            if (string.IsNullOrEmpty(desc))  return Results.BadRequest(new { error = "description is required" });

            var addedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO useful_links (description, url, added_at) VALUES ($desc, $url, $at)";
            cmd.Parameters.AddWithValue("$desc", desc);
            cmd.Parameters.AddWithValue("$url",  url);
            cmd.Parameters.AddWithValue("$at",   addedAt);
            cmd.ExecuteNonQuery();
            return Results.Ok(new { message = "Link added", added_at = addedAt });
        });

        // ── PATCH /useful-links/{id} ──────────────────────────────────────────────
        app.MapMethods("/useful-links/{id:int}", ["PATCH"], async (int id, HttpRequest request) =>
        {
            var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            var url  = body?.GetValueOrDefault("url")?.Trim();
            var desc = body?.GetValueOrDefault("description")?.Trim();

            using var conn = open();
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

        // ── DELETE /useful-links/{id} ─────────────────────────────────────────────
        app.MapDelete("/useful-links/{id:int}", (int id) =>
        {
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM useful_links WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            return Results.Ok(new { message = "Deleted" });
        });
    }
}
