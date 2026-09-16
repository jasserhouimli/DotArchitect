using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.ServiceRequests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.ServiceRequests;

public static class ServiceRequestsModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("ServiceRequests");

        builder.Services.AddDbContext<ServiceRequestsDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<ServiceRequestsDbContext>();
    }
}
