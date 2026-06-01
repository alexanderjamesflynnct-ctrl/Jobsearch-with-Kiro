using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using JobSearchAPI.Data;
using System.Diagnostics;

namespace JobSearchAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("CodeStats")]
public class CodeStatsController : ControllerBase
{
    private readonly JobSearchDatabase _db;

    public CodeStatsController(JobSearchDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves file and line count statistics from the database.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCodeStats()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // Standard SQL string for scannability
        const string sql = "SELECT file_type, file_count, line_count, scanned_at FROM code_stats ORDER BY line_count DESC";
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var rows = new List<Dictionary<string, object>>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                rows.Add(new Dictionary<string, object>
                {
                    ["file_type"] = reader.GetString(0),
                    ["file_count"] = reader.GetInt32(1),
                    ["line_count"] = reader.GetInt32(2),
                    ["scanned_at"] = reader.GetString(3),
                });
            }
        }

        var totalFiles = rows.Sum(r => (int)r["file_count"]);
        var totalLines = rows.Sum(r => (int)r["line_count"]);

        return Ok(new { 
            total_files = totalFiles, 
            total_lines = totalLines, 
            breakdown = rows 
        });
    }

    /// <summary>
    /// Triggers the external Python script to re-scan the repository for stats.
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> TriggerScan()
    {
        var scriptPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SearchCode", "scan_code_stats.py"));

        var psi = new ProcessStartInfo
        {
            FileName = "py",
            Arguments = $"-3.14 \"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..")),
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return StatusCode(500, new { success = false, message = "Failed to start scan process." });

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                return StatusCode(500, new { success = false, output = stdout, errors = stderr });
            }

            return Ok(new { success = true, output = stdout });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}