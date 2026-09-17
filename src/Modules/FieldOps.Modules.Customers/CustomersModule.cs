using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.Customers.Features.CreateCustomer;
using FieldOps.Modules.Customers.Features.GetCustomer;
using FieldOps.Modules.Customers.Features.GetCustomers;
using FieldOps.Modules.Customers.Features.UpdateCustomer;
using FieldOps.Modules.Customers.Features.DeleteCustomer;
using FieldOps.Modules.Customers.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Modules.Customers;

public static class CustomersModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("FieldOps");

        builder.Services.AddDbContext<CustomersDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddValidatorsFromAssemblyContaining<CustomersDbContext>();
        builder.Services.AddScoped<CustomersDbContext>();
        builder.Services.AddScoped<CreateCustomerHandler>();
        builder.Services.AddScoped<GetCustomerHandler>();
        builder.Services.AddScoped<GetCustomersHandler>();
        builder.Services.AddScoped<UpdateCustomerHandler>();
        builder.Services.AddScoped<DeleteCustomerHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreateCustomerEndpoint.Map(app);
        GetCustomerEndpoint.Map(app);
        GetCustomersEndpoint.Map(app);
        UpdateCustomerEndpoint.Map(app);
        DeleteCustomerEndpoint.Map(app);
    }
}
