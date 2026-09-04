using System.Data;
using Dapper;
using Inventory.Api.Infrastructure.Data;

namespace Inventory.Api.Modules.Audit.Services;

public class HistoryService
{
    private readonly Db _db;
    private readonly IHttpContextAccessor _http;
    public HistoryService(Db db, IHttpContextAccessor http){ _db=db; _http=http; }

    public async Task AddAsync(string action,string entityType,long? entityId,string? entityLabel,string? description,IDbConnection? connection=null,IDbTransaction? transaction=null)
    {
        var actor=ResolveActor();
        const string sql=@"INSERT INTO ActivityHistory(Action,EntityType,EntityId,EntityLabel,Description,Actor)
VALUES(@Action,@EntityType,@EntityId,@EntityLabel,@Description,@Actor)";
        var args=new{Action=action,EntityType=entityType,EntityId=entityId,EntityLabel=entityLabel,Description=description,Actor=actor};
        if(connection is not null){await connection.ExecuteAsync(sql,args,transaction);return;}
        using var c=_db.Open(); await c.ExecuteAsync(sql,args);
    }

    private string ResolveActor()
    {
        var ctx=_http.HttpContext;
        if(ctx?.User?.Identity?.IsAuthenticated==true) return ctx.User.Identity?.Name ?? "AUTHENTICATED";
        if(ctx is not null && ctx.Request.Headers.TryGetValue("X-Actor",out var actor) && !string.IsNullOrWhiteSpace(actor)) return actor.ToString();
        return "SYSTEM";
    }
}
