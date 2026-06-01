using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using JobSearchAPI.Data;

namespace JobSearchAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Kanban")]
public class KanbanController : ControllerBase
{
    private readonly JobSearchDatabase _db;

    public KanbanController(JobSearchDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Synchronizes jobs and retrieves all cards for the Kanban board.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetKanbanCards()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // Pass 1: Sync - Ensure all job_listings have a corresponding Kanban row
        const string syncSql = @"
            INSERT OR IGNORE INTO kanban_jobs (job_listing_id, status, updated_at)
            SELECT id, 'Searched/Found', searched_at FROM job_listings
            WHERE id NOT IN (SELECT job_listing_id FROM kanban_jobs)";
        
        using var syncCmd = conn.CreateCommand();
        syncCmd.CommandText = syncSql;
        await syncCmd.ExecuteNonQueryAsync();

        // Pass 2: Retrieval with Join
        const string fetchSql = @"
            SELECT k.id, k.job_listing_id, k.status, k.notes, k.is_active, k.fail_type, k.updated_at,
                   j.title, j.company, j.location, j.state, j.country,
                   j.salary, j.job_type, j.is_remote, j.source, j.url, j.date_posted
            FROM kanban_jobs k
            JOIN job_listings j ON j.id = k.job_listing_id
            ORDER BY k.updated_at DESC";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = fetchSql;

        var cards = new List<Dictionary<string, object?>>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                cards.Add(row);
            }
        }
        return Ok(cards);
    }

    /// <summary>
    /// Updates the status of a specific Kanban card and logs the history.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] Dictionary<string, string> body)
    {
        var status = body?.GetValueOrDefault("status")?.Trim() ?? "";
        if (string.IsNullOrEmpty(status)) return BadRequest(new { error = "status required" });

        var updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // 1. Get current status for history
        using var getCmd = conn.CreateCommand();
        getCmd.CommandText = "SELECT status FROM kanban_jobs WHERE id = @id";
        getCmd.Parameters.AddWithValue("@id", id);
        var fromStatus = (await getCmd.ExecuteScalarAsync())?.ToString();

        // 2. Update the record
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE kanban_jobs SET status = @s, updated_at = @at WHERE id = @id";
        cmd.Parameters.AddWithValue("@s", status);
        cmd.Parameters.AddWithValue("@at", updatedAt);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();

        // 3. Conditional: Clear active flag if moving out of Searched/Found
        if (fromStatus == "Searched/Found" && status != "Searched/Found")
        {
            using var clearActive = conn.CreateCommand();
            clearActive.CommandText = "UPDATE kanban_jobs SET is_active = 0 WHERE id = @id";
            clearActive.Parameters.AddWithValue("@id", id);
            await clearActive.ExecuteNonQueryAsync();
        }

        // 4. Log to history
        const string historySql = "INSERT INTO kanban_history (kanban_id, from_status, to_status, changed_at) VALUES (@kid, @from, @to, @at)";
        using var logCmd = conn.CreateCommand();
        logCmd.CommandText = historySql;
        logCmd.Parameters.AddWithValue("@kid", id);
        logCmd.Parameters.AddWithValue("@from", fromStatus ?? "");
        logCmd.Parameters.AddWithValue("@to", status);
        logCmd.Parameters.AddWithValue("@at", updatedAt);
        await logCmd.ExecuteNonQueryAsync();

        return Ok(new { message = "Updated", status, updated_at = updatedAt });
    }

    [HttpPatch("{id:int}/fail-type")]
    public async Task<IActionResult> UpdateFailType(int id, [FromBody] Dictionary<string, string> body)
    {
        var failType = body?.GetValueOrDefault("fail_type")?.Trim() ?? "";
        var updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE kanban_jobs SET fail_type = @ft, updated_at = @at WHERE id = @id";
        cmd.Parameters.AddWithValue("@ft", string.IsNullOrEmpty(failType) ? DBNull.Value : failType);
        cmd.Parameters.AddWithValue("@at", updatedAt);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
        
        return Ok(new { message = "Updated" });
    }

    [HttpPost("{id:int}/active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT is_active FROM kanban_jobs WHERE id = @id";
        checkCmd.Parameters.AddWithValue("@id", id);
        var current = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

        using var clearCmd = conn.CreateCommand();
        clearCmd.CommandText = "UPDATE kanban_jobs SET is_active = 0";
        await clearCmd.ExecuteNonQueryAsync();

        if (current == 0)
        {
            using var setCmd = conn.CreateCommand();
            setCmd.CommandText = "UPDATE kanban_jobs SET is_active = 1 WHERE id = @id";
            setCmd.Parameters.AddWithValue("@id", id);
            await setCmd.ExecuteNonQueryAsync();
        }

        return Ok(new { message = current == 0 ? "Set as active" : "Deselected" });
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT from_status, to_status, changed_at FROM kanban_history WHERE kanban_id = @id ORDER BY changed_at DESC";
        cmd.Parameters.AddWithValue("@id", id);
        
        var rows = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new {
                from_status = reader.IsDBNull(0) ? null : reader.GetString(0),
                to_status = reader.GetString(1),
                changed_at = reader.GetString(2)
            });
        }
        return Ok(rows);
    }

    [HttpGet("{id:int}/notes")]
    public async Task<IActionResult> GetNotes(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, note, created_at FROM kanban_notes WHERE kanban_id = @id ORDER BY created_at DESC";
        cmd.Parameters.AddWithValue("@id", id);
        
        var rows = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new {
                id = reader.GetInt32(0),
                note = reader.GetString(1),
                created_at = reader.GetString(2)
            });
        }
        return Ok(rows);
    }

    [HttpPost("{id:int}/notes")]
    public async Task<IActionResult> AddNote(int id, [FromBody] Dictionary<string, string> body)
    {
        var note = body?.GetValueOrDefault("note")?.Trim() ?? "";
        if (string.IsNullOrEmpty(note)) return BadRequest(new { error = "note is required" });

        var createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO kanban_notes (kanban_id, note, created_at) VALUES (@id, @note, @at)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@note", note);
        cmd.Parameters.AddWithValue("@at", createdAt);
        await cmd.ExecuteNonQueryAsync();
        
        return Ok(new { message = "Note saved", created_at = createdAt });
    }

    [HttpDelete("notes/{noteId:int}")]
    public async Task<IActionResult> DeleteNote(int noteId)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM kanban_notes WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", noteId);
        await cmd.ExecuteNonQueryAsync();
        return Ok(new { message = "Deleted" });
    }

    [HttpGet("{id:int}/timers")]
    public async Task<IActionResult> GetTimers(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, duration_seconds, created_at FROM job_timers WHERE kanban_id = @id ORDER BY created_at DESC";
        cmd.Parameters.AddWithValue("@id", id);
        
        var rows = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new {
                id = reader.GetInt32(0),
                duration_seconds = reader.GetInt32(1),
                created_at = reader.GetString(2)
            });
        }
        return Ok(rows);
    }

    [HttpPost("{id:int}/timer")]
    public async Task<IActionResult> AddTimer(int id, [FromBody] Dictionary<string, int> body)
    {
        var duration = body?.GetValueOrDefault("duration_seconds") ?? 0;
        if (duration <= 0) return BadRequest(new { error = "duration_seconds must be > 0" });

        var createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO job_timers (kanban_id, duration_seconds, created_at) VALUES (@id, @dur, @at)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@dur", duration);
        cmd.Parameters.AddWithValue("@at", createdAt);
        await cmd.ExecuteNonQueryAsync();
        
        return Ok(new { message = "Timer saved", duration_seconds = duration });
    }
}