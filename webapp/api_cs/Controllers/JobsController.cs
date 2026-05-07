using Microsoft.Data.Sqlite;

public static class JobsController
{
    public static void MapJobsEndpoints(this WebApplication app, Func<SqliteConnection> open)
    {
        app.MapGet("/jobs", (
            string? keywords, string? country, string? source,
            string? job_type, string? is_remote, string? state,
            string? sort, string? order, int page = 1, int per_page = 25) =>
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

            using var conn = open();
            conn.Open();

            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM job_listings {where}";
            foreach (var (k, v) in parms) countCmd.Parameters.AddWithValue(k, v);
            var total = Convert.ToInt32(countCmd.ExecuteScalar());

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

        app.MapDelete("/jobs/{id}", (string id) =>
        {
            using var conn = open();
            conn.Open();

            // Delete related kanban notes first
            using var notesCmd = conn.CreateCommand();
            notesCmd.CommandText = "DELETE FROM kanban_notes WHERE kanban_id IN (SELECT id FROM kanban_jobs WHERE job_listing_id = $id)";
            notesCmd.Parameters.AddWithValue("$id", id);
            notesCmd.ExecuteNonQuery();

            // Delete related kanban history
            using var histCmd = conn.CreateCommand();
            histCmd.CommandText = "DELETE FROM kanban_history WHERE kanban_id IN (SELECT id FROM kanban_jobs WHERE job_listing_id = $id)";
            histCmd.Parameters.AddWithValue("$id", id);
            histCmd.ExecuteNonQuery();

            // Delete kanban card
            using var kanbanCmd = conn.CreateCommand();
            kanbanCmd.CommandText = "DELETE FROM kanban_jobs WHERE job_listing_id = $id";
            kanbanCmd.Parameters.AddWithValue("$id", id);
            kanbanCmd.ExecuteNonQuery();

            // Delete the job listing
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM job_listings WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            var rows = cmd.ExecuteNonQuery();
            return rows > 0 ? Results.Ok(new { message = "Deleted" }) : Results.NotFound();
        });

        app.MapMethods("/jobs/{id}/country", ["PATCH"], async (string id, HttpRequest request) =>
        {
            var body = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            var country = body?.GetValueOrDefault("country")?.Trim() ?? "";
            if (string.IsNullOrEmpty(country)) return Results.BadRequest(new { error = "country is required" });

            using var conn = open();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE job_listings SET country = $country WHERE id = $id";
            cmd.Parameters.AddWithValue("$country", country);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            return Results.Ok(new { message = "Updated" });
        });

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
            var st        = body.GetValueOrDefault("state")?.Trim() ?? "";
            var ctry      = body.GetValueOrDefault("country")?.Trim() ?? "";
            var jobType   = body.GetValueOrDefault("job_type")?.Trim() ?? "";
            var salary    = body.GetValueOrDefault("salary")?.Trim() ?? "";
            var url       = body.GetValueOrDefault("url")?.Trim() ?? "";
            var src       = body.GetValueOrDefault("source")?.Trim() ?? "manual";
            var isRemote  = body.GetValueOrDefault("is_remote")?.Trim() ?? "";
            var desc      = body.GetValueOrDefault("description")?.Trim() ?? "";

            using var conn = open();
            conn.Open();

            using var searchCmd = conn.CreateCommand();
            searchCmd.CommandText = "INSERT INTO searches (searched_at, keywords, location, country) VALUES ($at, 'Manual Entry', '', $country)";
            searchCmd.Parameters.AddWithValue("$at", now);
            searchCmd.Parameters.AddWithValue("$country", ctry);
            searchCmd.ExecuteNonQuery();

            using var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid()";
            var searchId = Convert.ToInt64(lastIdCmd.ExecuteScalar());

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO job_listings (id, search_id, searched_at, date_posted, country, title, company, location, city, state, job_type, salary, url, source, is_remote, description)
                VALUES ($id, $sid, $at, $at, $country, $title, $company, $loc, $city, $state, $jtype, $salary, $url, $source, $remote, $desc)
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$sid", searchId);
            cmd.Parameters.AddWithValue("$at", now);
            cmd.Parameters.AddWithValue("$country", ctry);
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$company", company);
            cmd.Parameters.AddWithValue("$loc", location);
            cmd.Parameters.AddWithValue("$city", city);
            cmd.Parameters.AddWithValue("$state", st);
            cmd.Parameters.AddWithValue("$jtype", jobType);
            cmd.Parameters.AddWithValue("$salary", salary);
            cmd.Parameters.AddWithValue("$url", url);
            cmd.Parameters.AddWithValue("$source", src);
            cmd.Parameters.AddWithValue("$remote", string.IsNullOrEmpty(isRemote) ? DBNull.Value : isRemote);
            cmd.Parameters.AddWithValue("$desc", desc);
            cmd.ExecuteNonQuery();

            using var kanbanCmd = conn.CreateCommand();
            kanbanCmd.CommandText = "INSERT INTO kanban_jobs (job_listing_id, status, updated_at) VALUES ($id, 'Searched/Found', $at)";
            kanbanCmd.Parameters.AddWithValue("$id", id);
            kanbanCmd.Parameters.AddWithValue("$at", now);
            kanbanCmd.ExecuteNonQuery();

            return Results.Ok(new { message = "Job added", id });
        });

        app.MapGet("/countries", () =>
        {
            using var conn = open(); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT country FROM job_listings WHERE country IS NOT NULL ORDER BY country";
            var list = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetString(0));
            return Results.Ok(list);
        });

        app.MapGet("/sources", () =>
        {
            using var conn = open(); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT source FROM job_listings WHERE source IS NOT NULL ORDER BY source";
            var list = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetString(0));
            return Results.Ok(list);
        });

        app.MapGet("/job-types", () =>
        {
            using var conn = open(); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT job_type FROM job_listings WHERE job_type IS NOT NULL AND job_type != '' ORDER BY job_type";
            var list = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetString(0));
            return Results.Ok(list);
        });

        app.MapGet("/states", () =>
        {
            using var conn = open(); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT state FROM job_listings WHERE state IS NOT NULL AND state != '' ORDER BY state";
            var list = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetString(0));
            return Results.Ok(list);
        });

        // ── DELETE /jobs/stale ────────────────────────────────────────────────────
        // Deletes all jobs in Searched/Found that have never moved to another lane
        app.MapDelete("/jobs/stale", () =>
        {
            using var conn = open();
            conn.Open();

            // Find kanban cards in Searched/Found with no history (never moved)
            using var findCmd = conn.CreateCommand();
            findCmd.CommandText = """
                SELECT k.id, k.job_listing_id FROM kanban_jobs k
                WHERE k.status = 'Searched/Found'
                  AND k.id NOT IN (SELECT DISTINCT kanban_id FROM kanban_history)
                """;
            var stale = new List<(int kanbanId, string jobId)>();
            using var reader = findCmd.ExecuteReader();
            while (reader.Read())
                stale.Add((reader.GetInt32(0), reader.GetString(1)));

            if (stale.Count == 0)
                return Results.Ok(new { message = "No stale jobs found", deleted = 0 });

            foreach (var (kanbanId, jobId) in stale)
            {
                // Delete notes
                using var n = conn.CreateCommand();
                n.CommandText = "DELETE FROM kanban_notes WHERE kanban_id = $kid";
                n.Parameters.AddWithValue("$kid", kanbanId);
                n.ExecuteNonQuery();

                // Delete kanban card
                using var k = conn.CreateCommand();
                k.CommandText = "DELETE FROM kanban_jobs WHERE id = $kid";
                k.Parameters.AddWithValue("$kid", kanbanId);
                k.ExecuteNonQuery();

                // Delete job listing
                using var j = conn.CreateCommand();
                j.CommandText = "DELETE FROM job_listings WHERE id = $jid";
                j.Parameters.AddWithValue("$jid", jobId);
                j.ExecuteNonQuery();
            }

            return Results.Ok(new { message = $"Deleted {stale.Count} stale job(s)", deleted = stale.Count });
        });
    }
}
