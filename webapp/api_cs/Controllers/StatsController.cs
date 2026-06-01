using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using JobSearchAPI.Data;

namespace JobSearchAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Stats")]
public class StatsController : ControllerBase
{
    private readonly JobSearchDatabase _db;

    public StatsController(JobSearchDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns high-level counts and country breakdown.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOverviewStats()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // Total Jobs
        using var c1 = conn.CreateCommand();
        c1.CommandText = "SELECT COUNT(*) FROM job_listings";
        var totalJobs = Convert.ToInt32(await c1.ExecuteScalarAsync());

        // Total Searches
        using var c2 = conn.CreateCommand();
        c2.CommandText = "SELECT COUNT(*) FROM searches";
        var totalSearches = Convert.ToInt32(await c2.ExecuteScalarAsync());

        // Last Search Timestamp
        using var c3 = conn.CreateCommand();
        c3.CommandText = "SELECT searched_at FROM searches ORDER BY searched_at DESC LIMIT 1";
        var lastSearch = (await c3.ExecuteScalarAsync())?.ToString();

        // Breakdown by Country
        using var c4 = conn.CreateCommand();
        c4.CommandText = "SELECT country, COUNT(*) as n FROM job_listings GROUP BY country ORDER BY n DESC";
        var byCountry = new List<Dictionary<string, object>>();
        using (var r4 = await c4.ExecuteReaderAsync())
        {
            while (await r4.ReadAsync())
                byCountry.Add(new Dictionary<string, object> { ["country"] = r4.GetString(0), ["n"] = r4.GetInt32(1) });
        }

        return Ok(new
        {
            total_jobs = totalJobs,
            total_searches = totalSearches,
            last_search = lastSearch,
            by_country = byCountry
        });
    }

    /// <summary>
    /// Returns the total number of unique jobs that reached 'Applied' status.
    /// </summary>
    [HttpGet("applied-count")]
    public async Task<IActionResult> GetTotalAppliedCount()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(DISTINCT kanban_id) FROM kanban_history WHERE to_status = 'Applied'";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return Ok(new { count });
    }

    /// <summary>
    /// Returns the count of applications submitted today based on the configured timezone.
    /// </summary>
    [HttpGet("applied-today")]
    public async Task<IActionResult> GetAppliedToday()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        using var tzCmd = conn.CreateCommand();
        tzCmd.CommandText = "SELECT value FROM settings WHERE key = 'timezone'";
        var tzId = (await tzCmd.ExecuteScalarAsync())?.ToString() ?? "America/New_York";

        string todayStart, todayEnd;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var startLocal = now.Date;
            var endLocal = startLocal.AddDays(1);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
            todayStart = startUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
            todayEnd = endUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
        catch
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            todayStart = today + "T00:00:00Z";
            todayEnd = today + "T23:59:59Z";
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(DISTINCT kanban_id) FROM kanban_history WHERE to_status = 'Applied' AND changed_at >= @start AND changed_at < @end";
        cmd.Parameters.AddWithValue("@start", todayStart);
        cmd.Parameters.AddWithValue("@end", todayEnd);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        
        return Ok(new { count, todayStart, todayEnd });
    }

    /// <summary>
    /// Returns the current outcomes of all applied jobs (Applied, Interviewed, Accepted, Failed breakdown).
    /// </summary>
    [HttpGet("outcomes")]
    public async Task<IActionResult> GetOutcomes()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        const string sql = @"
            SELECT
                CASE
                    WHEN k.status = 'Failed' THEN COALESCE(k.fail_type, 'Unspecified')
                    ELSE k.status
                END as outcome,
                COUNT(*) as n
            FROM kanban_jobs k
            WHERE k.id IN (SELECT DISTINCT kanban_id FROM kanban_history WHERE to_status = 'Applied')
              AND k.status IN ('Applied', 'Interviewed', 'Accepted Job', 'Failed')
            GROUP BY outcome
            ORDER BY n DESC";

        cmd.CommandText = sql;
        var rows = new List<Dictionary<string, object>>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                rows.Add(new Dictionary<string, object> { ["outcome"] = reader.GetString(0), ["count"] = reader.GetInt32(1) });
        }
        return Ok(rows);
    }
}