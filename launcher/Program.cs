using System.Diagnostics;

Console.WriteLine("=== Job Search App Launcher ===");
Console.WriteLine();

var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var apiDir  = Path.Combine(baseDir, "webapp", "api_cs");
var uiDir   = Path.Combine(baseDir, "webapp", "client");

Console.WriteLine($"Workspace: {baseDir}");
Console.WriteLine($"API:       {apiDir}");
Console.WriteLine($"UI:        {uiDir}");
Console.WriteLine();

// Start the C# API
Console.WriteLine("[1/2] Starting API (dotnet run)...");
var apiProcess = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName         = "dotnet",
        Arguments        = "run",
        WorkingDirectory = apiDir,
        UseShellExecute  = false,
        CreateNoWindow   = false,
    }
};
apiProcess.Start();
Console.WriteLine($"      API started (PID: {apiProcess.Id})");

// Small delay to let API start before UI
await Task.Delay(2000);

// Start the React UI
Console.WriteLine("[2/2] Starting UI (npm run dev)...");
var uiProcess = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName         = "cmd.exe",
        Arguments        = "/c set OPENSSL_CONF= && npm run dev",
        WorkingDirectory = uiDir,
        UseShellExecute  = false,
        CreateNoWindow   = false,
    }
};
uiProcess.Start();
Console.WriteLine($"      UI started (PID: {uiProcess.Id})");

Console.WriteLine();
Console.WriteLine("Both services are running.");
Console.WriteLine("  API: http://localhost:8000");
Console.WriteLine("  UI:  http://localhost:5173");
Console.WriteLine();
Console.WriteLine("Press ENTER to stop both and exit...");
Console.ReadLine();

// Shutdown
Console.WriteLine("Stopping services...");

try
{
    if (!apiProcess.HasExited)
    {
        KillProcessTree(apiProcess.Id);
        Console.WriteLine("  API stopped.");
    }
}
catch (Exception ex) { Console.WriteLine($"  API stop error: {ex.Message}"); }

try
{
    if (!uiProcess.HasExited)
    {
        KillProcessTree(uiProcess.Id);
        Console.WriteLine("  UI stopped.");
    }
}
catch (Exception ex) { Console.WriteLine($"  UI stop error: {ex.Message}"); }

Console.WriteLine("Done. Goodbye!");

static void KillProcessTree(int pid)
{
    // Use taskkill /T to kill the process tree on Windows
    var kill = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName        = "taskkill",
            Arguments       = $"/PID {pid} /T /F",
            UseShellExecute = false,
            CreateNoWindow  = true,
        }
    };
    kill.Start();
    kill.WaitForExit(5000);
}
