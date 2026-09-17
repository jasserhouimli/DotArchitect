using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.Invoicing.Features.CreateInvoice;
using FieldOps.Modules.Invoicing.Features.GetInvoice;
using FieldOps.Modules.Invoicing.Features.GetInvoices;
using FieldOps.Modules.Invoicing.Features.UpdateInvoice;
using FieldOps.Modules.Invoicing.Features.DeleteInvoice;
using FieldOps.Modules.Invoicing.Features.AddInvoiceItem;
using FieldOps.Modules.Invoicing.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Modules.Invoicing;

public static class InvoicingModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("FieldOps");

        builder.Services.AddDbContext<InvoicingDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddValidatorsFromAssemblyContaining<InvoicingDbContext>();
        builder.Services.AddScoped<InvoicingDbContext>();
        builder.Services.AddScoped<CreateInvoiceHandler>();
        builder.Services.AddScoped<GetInvoiceHandler>();
        builder.Services.AddScoped<GetInvoicesHandler>();
        builder.Services.AddScoped<UpdateInvoiceHandler>();
        builder.Services.AddScoped<DeleteInvoiceHandler>();
        builder.Services.AddScoped<AddInvoiceItemHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreateInvoiceEndpoint.Map(app);
        GetInvoiceEndpoint.Map(app);
        GetInvoicesEndpoint.Map(app);
        UpdateInvoiceEndpoint.Map(app);
        DeleteInvoiceEndpoint.Map(app);
        AddInvoiceItemEndpoint.Map(app);
    }
}
