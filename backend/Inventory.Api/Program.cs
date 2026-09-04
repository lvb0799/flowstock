using Inventory.Api.Infrastructure.Data;
using Inventory.Api.Infrastructure.Extensions;
using Inventory.Api.Infrastructure.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddSingleton<Db>();
builder.Services.AddFlowStockModules();

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "data"));
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "logs"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Db>();
    await db.InitializeAsync();
    var actionLog = scope.ServiceProvider.GetRequiredService<ApiActionLogService>();
    var retentionDays = builder.Configuration.GetValue<int?>("ApiLogging:RetentionDays") ?? 90;
    await actionLog.CleanupAsync(retentionDays);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Frontend");

// Records every /api request to SQLite ApiActionLog and logs/api-YYYYMMDD.log.
// It also provides one global exception handling point for modular services.
app.UseMiddleware<ApiActionLogMiddleware>();

app.MapControllers();
app.Run();
