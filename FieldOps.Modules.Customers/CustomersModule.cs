using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.Customers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Customers;

public static class CustomersModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Customers");

        builder.Services.AddDbContext<CustomersDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<CustomersDbContext>();
    }
}
