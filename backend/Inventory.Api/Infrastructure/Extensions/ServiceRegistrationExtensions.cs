using Inventory.Api.Infrastructure.Logging;
using Inventory.Api.Modules.Audit.Services;
using Inventory.Api.Modules.Master.Services;
using Inventory.Api.Modules.Purchasing.Services;
using Inventory.Api.Modules.Sales.Services;

namespace Inventory.Api.Infrastructure.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddFlowStockModules(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<HistoryService>();
        services.AddScoped<ApiActionLogService>();
        services.AddScoped<MasterService>();
        services.AddScoped<PurchaseService>();
        services.AddScoped<SalesService>();
        return services;
    }
}
