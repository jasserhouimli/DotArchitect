using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.Inventory.Features.CreateInventoryItem;
using FieldOps.Modules.Inventory.Features.GetInventoryItem;
using FieldOps.Modules.Inventory.Features.GetInventoryItems;
using FieldOps.Modules.Inventory.Features.UpdateInventoryItem;
using FieldOps.Modules.Inventory.Features.DeleteInventoryItem;
using FieldOps.Modules.Inventory.Features.RecordTransaction;
using FieldOps.Modules.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Modules.Inventory;

public static class InventoryModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("FieldOps");

        builder.Services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddValidatorsFromAssemblyContaining<InventoryDbContext>();
        builder.Services.AddScoped<InventoryDbContext>();
        builder.Services.AddScoped<CreateInventoryItemHandler>();
        builder.Services.AddScoped<GetInventoryItemHandler>();
        builder.Services.AddScoped<GetInventoryItemsHandler>();
        builder.Services.AddScoped<UpdateInventoryItemHandler>();
        builder.Services.AddScoped<DeleteInventoryItemHandler>();
        builder.Services.AddScoped<RecordTransactionHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreateInventoryItemEndpoint.Map(app);
        GetInventoryItemEndpoint.Map(app);
        GetInventoryItemsEndpoint.Map(app);
        UpdateInventoryItemEndpoint.Map(app);
        DeleteInventoryItemEndpoint.Map(app);
        RecordTransactionEndpoint.Map(app);
    }
}
