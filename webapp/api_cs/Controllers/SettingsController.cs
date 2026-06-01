using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using JobSearchAPI.Data;

namespace JobSearchAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Settings")]
public class SettingsController : ControllerBase
{
    private readonly JobSearchDatabase _db;

    public SettingsController(JobSearchDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves all system settings as a key-value dictionary.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        const string sql = "SELECT key, value FROM settings";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var dict = new Dictionary<string, string>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                dict[reader.GetString(0)] = reader.GetString(1);
            }
        }

        return Ok(dict);
    }

    /// <summary>
    /// Updates or inserts multiple settings entries.
    /// </summary>
    /// <param name="body">Dictionary of settings to upsert.</param>
    [HttpPatch]
    public async Task<IActionResult> UpdateSettings([FromBody] Dictionary<string, string> body)
    {
        if (body == null || body.Count == 0) 
            return BadRequest(new { error = "Settings data is required." });

        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // Standard SQL Upsert string for scannability
        const string sql = "INSERT INTO settings (key, value) VALUES (@k, @v) ON CONFLICT(key) DO UPDATE SET value = @v";

        foreach (var (key, value) in body)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            await cmd.ExecuteNonQueryAsync();
        }

        return Ok(new { message = "Settings saved" });
    }
}