using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.Identity.Features.Register;
using FieldOps.Modules.Identity.Features.Login;
using FieldOps.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Identity;

public static class IdentityModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("FieldOps");

        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<IdentityDbContext>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        RegisterEndpoint.Map(app);
        LoginEndpoint.Map(app);
    }
}
