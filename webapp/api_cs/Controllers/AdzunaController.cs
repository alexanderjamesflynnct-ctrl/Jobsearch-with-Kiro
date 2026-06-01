using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace JobSearchAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Adzuna")]
public class AdzunaController : ControllerBase
{
    /// <summary>
    /// Triggers the Adzuna Python scraping script.
    /// </summary>
    /// <param name="body">JSON body containing 'keywords'</param>
    [HttpPost("run")]
    public async Task<IActionResult> RunAdzuna([FromBody] Dictionary<string, string> body)
    {
        var keywords = body?.GetValueOrDefault("keywords")?.Trim() ?? "Director of Software Engineering";

        // Logic to find the script path relative to the runtime
        var scriptPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SearchCode", "adzuna_jobs.py"));

        var psi = new ProcessStartInfo
        {
            FileName = "py",
            Arguments = $"-3.14 \"{scriptPath}\" \"{keywords}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..")),
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return StatusCode(500, new { success = false, message = "Failed to start Python process." });

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                return StatusCode(500, new { success = false, output = stdout, errors = stderr });
            }

            return Ok(new { success = true, output = stdout, errors = stderr });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}