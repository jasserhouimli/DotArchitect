using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.Scheduling.Features.CreateSchedule;
using FieldOps.Modules.Scheduling.Features.GetSchedule;
using FieldOps.Modules.Scheduling.Features.GetSchedules;
using FieldOps.Modules.Scheduling.Features.UpdateSchedule;
using FieldOps.Modules.Scheduling.Features.DeleteSchedule;
using FieldOps.Modules.Scheduling.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Modules.Scheduling;

public static class SchedulingModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("FieldOps");

        builder.Services.AddDbContext<SchedulingDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddValidatorsFromAssemblyContaining<SchedulingDbContext>();
        builder.Services.AddScoped<SchedulingDbContext>();
        builder.Services.AddScoped<CreateScheduleHandler>();
        builder.Services.AddScoped<GetScheduleHandler>();
        builder.Services.AddScoped<GetSchedulesHandler>();
        builder.Services.AddScoped<UpdateScheduleHandler>();
        builder.Services.AddScoped<DeleteScheduleHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreateScheduleEndpoint.Map(app);
        GetScheduleEndpoint.Map(app);
        GetSchedulesEndpoint.Map(app);
        UpdateScheduleEndpoint.Map(app);
        DeleteScheduleEndpoint.Map(app);
    }
}
