using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using JobSearchAPI.Data;

namespace JobSearchAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("UsefulLinks")]
public class UsefulLinksController : ControllerBase
{
    private readonly JobSearchDatabase _db;

    public UsefulLinksController(JobSearchDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves all useful links ordered by addition date.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsefulLinks()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        const string sql = "SELECT * FROM useful_links ORDER BY added_at DESC";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var list = new List<Dictionary<string, object?>>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                list.Add(row);
            }
        }
        return Ok(list);
    }

    /// <summary>
    /// Adds a new useful link.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddUsefulLink([FromBody] Dictionary<string, string> body)
    {
        var url = body?.GetValueOrDefault("url")?.Trim() ?? "";
        var desc = body?.GetValueOrDefault("description")?.Trim() ?? "";

        if (string.IsNullOrEmpty(url)) return BadRequest(new { error = "url is required" });
        if (string.IsNullOrEmpty(desc)) return BadRequest(new { error = "description is required" });

        var addedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        const string sql = "INSERT INTO useful_links (description, url, added_at) VALUES (@desc, @url, @at)";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@desc", desc);
        cmd.Parameters.AddWithValue("@url", url);
        cmd.Parameters.AddWithValue("@at", addedAt);
        
        await cmd.ExecuteNonQueryAsync();
        return Ok(new { message = "Link added", added_at = addedAt });
    }

    /// <summary>
    /// Partially updates an existing useful link.
    /// </summary>
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateUsefulLink(int id, [FromBody] Dictionary<string, string> body)
    {
        var url = body?.GetValueOrDefault("url")?.Trim();
        var desc = body?.GetValueOrDefault("description")?.Trim();

        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        if (!string.IsNullOrEmpty(url))
        {
            using var c = conn.CreateCommand();
            c.CommandText = "UPDATE useful_links SET url = @url WHERE id = @id";
            c.Parameters.AddWithValue("@url", url);
            c.Parameters.AddWithValue("@id", id);
            await c.ExecuteNonQueryAsync();
        }

        if (!string.IsNullOrEmpty(desc))
        {
            using var c = conn.CreateCommand();
            c.CommandText = "UPDATE useful_links SET description = @desc WHERE id = @id";
            c.Parameters.AddWithValue("@desc", desc);
            c.Parameters.AddWithValue("@id", id);
            await c.ExecuteNonQueryAsync();
        }

        return Ok(new { message = "Updated" });
    }

    /// <summary>
    /// Deletes a specific useful link.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUsefulLink(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        const string sql = "DELETE FROM useful_links WHERE id = @id";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);
        
        await cmd.ExecuteNonQueryAsync();
        return Ok(new { message = "Deleted" });
    }
}