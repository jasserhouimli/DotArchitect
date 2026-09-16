using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Identity;

public static class IdentityModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Identity");

        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<IdentityDbContext>();
    }
}
