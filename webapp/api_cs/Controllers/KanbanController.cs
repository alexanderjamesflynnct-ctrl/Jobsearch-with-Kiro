using Microsoft.Data.Sqlite;

public static class KanbanController
{
    public static void MapKanbanEndpoints(this WebApplication app, Func<SqliteConnection> open)
    {
        // ── GET /kanban ───────────────────────────────────────────────────────────
        app.MapGet("/kanban", () =>
        {
            using var conn = open();
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

        // ── PATCH /kanban/{id}/status ─────────────────────────────────────────────
        app.MapMethods("/kanban/{id:int}/status", ["PATCH"], async (int id, HttpRequest request) =>
        {
            var body   = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            var status = body?.GetValueOrDefault("status")?.Trim() ?? "";
            if (string.IsNullOrEmpty(status)) return Results.BadRequest(new { error = "status required" });

            var updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            using var conn = open();
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

        // ── PATCH /kanban/{id}/fail-type ─────────────────────────────────────────
        app.MapMethods("/kanban/{id:int}/fail-type", ["PATCH"], async (int id, HttpRequest request) =>
        {
            var body     = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            var failType = body?.GetValueOrDefault("fail_type")?.Trim() ?? "";
            var updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE kanban_jobs SET fail_type = $ft, updated_at = $at WHERE id = $id";
            cmd.Parameters.AddWithValue("$ft", string.IsNullOrEmpty(failType) ? DBNull.Value : failType);
            cmd.Parameters.AddWithValue("$at", updatedAt);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            return Results.Ok(new { message = "Updated" });
        });

        // ── POST /kanban/{id}/active ──────────────────────────────────────────────
        app.MapPost("/kanban/{id:int}/active", (int id) =>
        {
            using var conn = open();
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

        // ── GET /kanban/{id}/history ──────────────────────────────────────────────
        app.MapGet("/kanban/{id:int}/history", (int id) =>
        {
            using var conn = open();
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

        // ── GET /kanban/{id}/notes ────────────────────────────────────────────────
        app.MapGet("/kanban/{id:int}/notes", (int id) =>
        {
            using var conn = open();
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

        // ── POST /kanban/{id}/notes ───────────────────────────────────────────────
        app.MapPost("/kanban/{id:int}/notes", async (int id, HttpRequest request) =>
        {
            var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            var note = body?.GetValueOrDefault("note")?.Trim() ?? "";
            if (string.IsNullOrEmpty(note)) return Results.BadRequest(new { error = "note is required" });

            var createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO kanban_notes (kanban_id, note, created_at) VALUES ($id, $note, $at)";
            cmd.Parameters.AddWithValue("$id",   id);
            cmd.Parameters.AddWithValue("$note", note);
            cmd.Parameters.AddWithValue("$at",   createdAt);
            cmd.ExecuteNonQuery();
            return Results.Ok(new { message = "Note saved", created_at = createdAt });
        });

        // ── DELETE /kanban/notes/{id} ─────────────────────────────────────────────
        app.MapDelete("/kanban/notes/{id:int}", (int id) =>
        {
            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM kanban_notes WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            return Results.Ok(new { message = "Deleted" });
        });
    }
}
