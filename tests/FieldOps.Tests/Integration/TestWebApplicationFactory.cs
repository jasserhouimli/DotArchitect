using FieldOps.Modules.Customers.Persistence;
using FieldOps.Modules.Identity.Persistence;
using FieldOps.Modules.Technicians.Persistence;
using FieldOps.Modules.WorkOrders.Persistence;
using FieldOps.Modules.Inventory.Persistence;
using FieldOps.Modules.Scheduling.Persistence;
using FieldOps.Modules.Invoicing.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FieldOps"] = "Host=localhost;Database=fieldops_test;Username=postgres;Password=postgres",
                ["Jwt:Key"] = "super-secret-key-that-is-at-least-32-characters-long-for-testing!",
                ["Jwt:Issuer"] = "FieldOps",
                ["Jwt:Audience"] = "FieldOps",
                ["Jwt:AccessTokenExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning"
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveDbContext<IdentityDbContext>(services);
            RemoveDbContext<CustomersDbContext>(services);
            RemoveDbContext<TechniciansDbContext>(services);
            RemoveDbContext<WorkOrdersDbContext>(services);
            RemoveDbContext<InventoryDbContext>(services);
            RemoveDbContext<SchedulingDbContext>(services);
            RemoveDbContext<InvoicingDbContext>(services);

            var dbName = $"Test_{Guid.NewGuid()}";

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"{dbName}_Identity"));
            services.AddDbContext<CustomersDbContext>(options =>
                options.UseInMemoryDatabase($"{dbName}_Customers"));
            services.AddDbContext<TechniciansDbContext>(options =>
                options.UseInMemoryDatabase($"{dbName}_Technicians"));
            services.AddDbContext<WorkOrdersDbContext>(options =>
                options.UseInMemoryDatabase($"{dbName}_WorkOrders"));
            services.AddDbContext<InventoryDbContext>(options =>
                options.UseInMemoryDatabase($"{dbName}_Inventory"));
            services.AddDbContext<SchedulingDbContext>(options =>
                options.UseInMemoryDatabase($"{dbName}_Scheduling"));
            services.AddDbContext<InvoicingDbContext>(options =>
                options.UseInMemoryDatabase($"{dbName}_Invoicing"));
        });
    }

    private static void RemoveDbContext<TContext>(IServiceCollection services) where TContext : DbContext
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<TContext>));
        if (descriptor is not null)
            services.Remove(descriptor);
    }
}
