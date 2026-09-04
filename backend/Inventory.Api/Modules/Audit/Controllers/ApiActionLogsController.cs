using Dapper;
using Inventory.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Audit.Controllers;

[ApiController]
[Route("api/system/action-logs")]
public class ApiActionLogsController : ControllerBase
{
    private readonly Db _db;
    public ApiActionLogsController(Db db)=>_db=db;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int limit=200,[FromQuery] string? path=null,[FromQuery] int? statusCode=null)
    {
        limit=Math.Clamp(limit,1,2000);
        using var c=_db.Open();
        return Ok(await c.QueryAsync(@"SELECT Id,TraceId,Method,Path,QueryString,Endpoint,StatusCode,ElapsedMs,Actor,ClientIp,ErrorMessage,CreatedAt
FROM ApiActionLog
WHERE (@Path IS NULL OR Path LIKE '%' || @Path || '%')
  AND (@StatusCode IS NULL OR StatusCode=@StatusCode)
ORDER BY Id DESC LIMIT @Limit",new{Path=string.IsNullOrWhiteSpace(path)?null:path,StatusCode=statusCode,Limit=limit}));
    }
}
