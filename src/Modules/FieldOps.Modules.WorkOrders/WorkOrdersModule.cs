using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.WorkOrders.Features.CreateWorkOrder;
using FieldOps.Modules.WorkOrders.Features.GetWorkOrder;
using FieldOps.Modules.WorkOrders.Features.GetWorkOrders;
using FieldOps.Modules.WorkOrders.Features.UpdateWorkOrder;
using FieldOps.Modules.WorkOrders.Features.DeleteWorkOrder;
using FieldOps.Modules.WorkOrders.Features.AssignTechnician;
using FieldOps.Modules.WorkOrders.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Modules.WorkOrders;

public static class WorkOrdersModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("FieldOps");

        builder.Services.AddDbContext<WorkOrdersDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddValidatorsFromAssemblyContaining<WorkOrdersDbContext>();
        builder.Services.AddScoped<WorkOrdersDbContext>();
        builder.Services.AddScoped<CreateWorkOrderHandler>();
        builder.Services.AddScoped<GetWorkOrderHandler>();
        builder.Services.AddScoped<GetWorkOrdersHandler>();
        builder.Services.AddScoped<UpdateWorkOrderHandler>();
        builder.Services.AddScoped<DeleteWorkOrderHandler>();
        builder.Services.AddScoped<AssignTechnicianHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreateWorkOrderEndpoint.Map(app);
        GetWorkOrderEndpoint.Map(app);
        GetWorkOrdersEndpoint.Map(app);
        UpdateWorkOrderEndpoint.Map(app);
        DeleteWorkOrderEndpoint.Map(app);
        AssignTechnicianEndpoint.Map(app);
    }
}
