using Dapper;
using Inventory.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Audit.Controllers;

[ApiController]
[Route("api/history")]
public class HistoryController : ControllerBase
{
    private readonly Db _db;
    public HistoryController(Db db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int limit = 500)
    {
        limit = Math.Clamp(limit, 1, 2000);
        using var c = _db.Open();
        return Ok(await c.QueryAsync(@"SELECT Id,Action,EntityType,EntityId,EntityLabel,Description,Actor,CreatedAt
FROM ActivityHistory ORDER BY Id DESC LIMIT @limit", new { limit }));
    }
}
