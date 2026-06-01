using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using JobSearchAPI.Data;
using System.Diagnostics;

namespace JobSearchAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Links")]
public class LinksController : ControllerBase
{
    private readonly JobSearchDatabase _db;

    public LinksController(JobSearchDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves all saved job links.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLinks()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        
        const string sql = "SELECT * FROM job_links ORDER BY added_at DESC";
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
    /// Adds a new job link for processing.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddLink([FromBody] Dictionary<string, string> body)
    {
        var url = body?.GetValueOrDefault("url")?.Trim() ?? "";

        if (string.IsNullOrEmpty(url))
            return BadRequest(new { error = "url is required" });

        // Domain Validation
        var isLinkedIn = url.Contains("linkedin.com/jobs");
        var isIndeed = url.Contains("indeed.com");
        var isGlassdoor = url.Contains("glassdoor.com");
        var isZipRecruiter = url.Contains("ziprecruiter.com");

        if (!isLinkedIn && !isIndeed && !isGlassdoor && !isZipRecruiter)
            return BadRequest(new { error = "Only LinkedIn, Indeed, Glassdoor and ZipRecruiter job URLs are supported" });

        var source = isLinkedIn ? "linkedin" : isIndeed ? "indeed" : isGlassdoor ? "glassdoor" : "ziprecruiter";
        var addedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO job_links (url, source, added_at) VALUES (@url, @source, @added_at) ON CONFLICT(url) DO NOTHING";
        cmd.Parameters.AddWithValue("@url", url);
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@added_at", addedAt);
        
        var rows = await cmd.ExecuteNonQueryAsync();

        return rows > 0
            ? Ok(new { message = "Link added", url, source, added_at = addedAt })
            : Ok(new { message = "Link already exists", url });
    }

    /// <summary>
    /// Deletes a specific job link.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLink(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM job_links WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
        return Ok(new { message = "Deleted" });
    }

    /// <summary>
    /// Triggers the Python script to process all pending (unprocessed) links.
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessLinks()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // 1. Identify pending links
        using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT id, url FROM job_links WHERE processed = 0";
        var pending = new List<(int id, string url)>();
        using (var reader = await selectCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) pending.Add((reader.GetInt32(0), reader.GetString(1)));
        }

        if (pending.Count == 0)
            return Ok(new { message = "No unprocessed links", processed = 0, output = "", errors = "" });

        // 2. Prepare temp file for Python consumption
        var tmpFile = Path.GetTempFileName();
        await System.IO.File.WriteAllLinesAsync(tmpFile, pending.Select(p => p.url));

        var scriptPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SearchCode", "linkedin_import.py"));

        var psi = new ProcessStartInfo
        {
            FileName = "py",
            Arguments = $"-3.14 \"{scriptPath}\" --file \"{tmpFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // 3. Execute Script
        using var proc = Process.Start(psi);
        if (proc == null) return StatusCode(500, "Failed to start import process.");

        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        System.IO.File.Delete(tmpFile);

        if (proc.ExitCode != 0)
            return StatusCode(500, new { message = "Import script failed", processed = 0, output = stdout, errors = stderr });

        var processedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var outputLines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // 4. Update Database based on script output
        foreach (var (id, url) in pending)
        {
            // Check for duplicates in actual job listings
            using var dupCheck = conn.CreateCommand();
            dupCheck.CommandText = "SELECT j.title, k.status FROM job_listings j LEFT JOIN kanban_jobs k ON k.job_listing_id = j.id WHERE j.url LIKE @url LIMIT 1";
            var cleanUrl = url.Split('?')[0];
            dupCheck.Parameters.AddWithValue("@url", $"%{cleanUrl}%");
            
            string? errorMsg = null;
            using (var dupReader = await dupCheck.ExecuteReaderAsync())
            {
                if (await dupReader.ReadAsync())
                {
                    var title = dupReader.IsDBNull(0) ? "" : dupReader.GetString(0);
                    var status = dupReader.IsDBNull(1) ? "Unknown" : dupReader.GetString(1);
                    errorMsg = $"Already imported - {status}: {title}";
                }
                else
                {
                    // Extract result from stdout lines
                    var fetchLine = outputLines.FirstOrDefault(l => l.Contains("Fetching") && l.Contains(url.Split('?')[0].Split('/').Last())) ?? "";
                    var fetchIdx = Array.IndexOf(outputLines, fetchLine);
                    var resultLine = fetchIdx >= 0 && fetchIdx + 1 < outputLines.Length ? outputLines[fetchIdx + 1] : "";
                    if (resultLine.Contains("Skipped") || resultLine.Contains("(no title)"))
                        errorMsg = resultLine.Trim();
                }
            }

            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE job_links SET processed = 1, processed_at = @at, error_message = @err WHERE id = @id";
            upd.Parameters.AddWithValue("@at", processedAt);
            upd.Parameters.AddWithValue("@err", errorMsg != null ? (object)errorMsg : DBNull.Value);
            upd.Parameters.AddWithValue("@id", id);
            await upd.ExecuteNonQueryAsync();
        }

        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        return Ok(new { message = $"Processed {pending.Count} link(s)", processed = pending.Count, output = stdout, lines, errors = stderr });
    }

    [HttpPost("{id:int}/reset")]
    public async Task<IActionResult> ResetLink(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE job_links SET processed = 0, processed_at = NULL WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
        return Ok(new { message = "Reset to pending" });
    }

    [HttpPost("reset-all")]
    public async Task<IActionResult> ResetAll()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE job_links SET processed = 0, processed_at = NULL WHERE processed = 1";
        var rows = await cmd.ExecuteNonQueryAsync();
        return Ok(new { message = $"Reset {rows} link(s) to pending" });
    }

    [HttpDelete("all-processed")]
    public async Task<IActionResult> DeleteAllProcessed()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM job_links WHERE processed = 1";
        var rows = await cmd.ExecuteNonQueryAsync();
        return Ok(new { message = $"Deleted {rows} imported link(s)" });
    }
}