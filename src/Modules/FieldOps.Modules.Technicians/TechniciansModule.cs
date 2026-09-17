using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.Technicians.Features.CreateTechnician;
using FieldOps.Modules.Technicians.Features.GetTechnician;
using FieldOps.Modules.Technicians.Features.GetTechnicians;
using FieldOps.Modules.Technicians.Features.UpdateTechnician;
using FieldOps.Modules.Technicians.Features.DeleteTechnician;
using FieldOps.Modules.Technicians.Features.AddTechnicianSkill;
using FieldOps.Modules.Technicians.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Modules.Technicians;

public static class TechniciansModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("FieldOps");

        builder.Services.AddDbContext<TechniciansDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddValidatorsFromAssemblyContaining<TechniciansDbContext>();
        builder.Services.AddScoped<TechniciansDbContext>();
        builder.Services.AddScoped<CreateTechnicianHandler>();
        builder.Services.AddScoped<GetTechnicianHandler>();
        builder.Services.AddScoped<GetTechniciansHandler>();
        builder.Services.AddScoped<UpdateTechnicianHandler>();
        builder.Services.AddScoped<DeleteTechnicianHandler>();
        builder.Services.AddScoped<AddTechnicianSkillHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreateTechnicianEndpoint.Map(app);
        GetTechnicianEndpoint.Map(app);
        GetTechniciansEndpoint.Map(app);
        UpdateTechnicianEndpoint.Map(app);
        DeleteTechnicianEndpoint.Map(app);
        AddTechnicianSkillEndpoint.Map(app);
    }
}
