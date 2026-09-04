using Dapper;
using Inventory.Api.Infrastructure.Data;

namespace Inventory.Api.Infrastructure.Logging;

public sealed record ApiActionLogEntry(
    string TraceId,
    string Method,
    string Path,
    string? QueryString,
    string? Endpoint,
    int StatusCode,
    long ElapsedMs,
    string Actor,
    string? ClientIp,
    string? UserAgent,
    string? ErrorMessage,
    DateTime CreatedAt);

public class ApiActionLogService
{
    private readonly Db _db;
    private readonly IWebHostEnvironment _env;
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public ApiActionLogService(Db db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task CleanupAsync(int retentionDays)
    {
        retentionDays = Math.Clamp(retentionDays, 1, 3650);
        try
        {
            using var c = _db.Open();
            await c.ExecuteAsync("DELETE FROM ApiActionLog WHERE datetime(CreatedAt) < datetime('now', @Offset)", new { Offset = $"-{retentionDays} days" });
        }
        catch { }

        try
        {
            var logDir = Path.Combine(_env.ContentRootPath, "logs");
            if (!Directory.Exists(logDir)) return;
            var threshold = DateTime.Today.AddDays(-retentionDays);
            foreach (var file in Directory.EnumerateFiles(logDir, "api-*.log"))
            {
                if (File.GetLastWriteTime(file) < threshold) File.Delete(file);
            }
        }
        catch { }
    }

    public async Task WriteAsync(ApiActionLogEntry entry)
    {
        try
        {
            using var c = _db.Open();
            await c.ExecuteAsync(@"INSERT INTO ApiActionLog
(TraceId,Method,Path,QueryString,Endpoint,StatusCode,ElapsedMs,Actor,ClientIp,UserAgent,ErrorMessage,CreatedAt)
VALUES(@TraceId,@Method,@Path,@QueryString,@Endpoint,@StatusCode,@ElapsedMs,@Actor,@ClientIp,@UserAgent,@ErrorMessage,@CreatedAt)", entry);
        }
        catch
        {
            // Logging must never break the business request.
        }

        try
        {
            var logDir = Path.Combine(_env.ContentRootPath, "logs");
            Directory.CreateDirectory(logDir);
            var file = Path.Combine(logDir, $"api-{entry.CreatedAt:yyyyMMdd}.log");
            var line = $"{entry.CreatedAt:yyyy-MM-dd HH:mm:ss.fff}\t{entry.TraceId}\t{entry.Method}\t{entry.Path}{entry.QueryString}\t{entry.StatusCode}\t{entry.ElapsedMs}ms\t{entry.Actor}\t{entry.ClientIp}\t{entry.Endpoint}\t{entry.ErrorMessage}{Environment.NewLine}";
            await FileLock.WaitAsync();
            try { await File.AppendAllTextAsync(file, line); }
            finally { FileLock.Release(); }
        }
        catch
        {
            // File logging is best-effort.
        }
    }
}
