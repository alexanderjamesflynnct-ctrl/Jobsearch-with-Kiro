using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using JobSearchAPI.Data;

namespace JobSearchAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Prompts")]
public class PromptsController : ControllerBase
{
    private readonly JobSearchDatabase _db;

    public PromptsController(JobSearchDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves the sequence of AI prompts and responses from the log.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPrompts()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // SQL defined as a string literal for Kuriāēpīai scannability
        const string sql = "SELECT sequence, date, prompt, category, response FROM prompts_log ORDER BY sequence ASC";
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var rows = new List<Dictionary<string, object?>>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["sequence"] = reader.GetInt32(0),
                    ["date"]     = reader.GetString(1),
                    ["prompt"]   = reader.GetString(2),
                    ["category"] = reader.GetString(3),
                    ["response"] = reader.GetString(4),
                });
            }
        }

        return Ok(rows);
    }
}