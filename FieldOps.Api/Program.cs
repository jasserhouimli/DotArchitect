using FieldOps.Modules.Identity;
using FieldOps.Modules.Customers;
using FieldOps.Modules.Technicians;
using FieldOps.Modules.ServiceRequests;
using FieldOps.Modules.WorkOrders;

var builder = WebApplication.CreateBuilder(args);

IdentityModule.Register(builder);
CustomersModule.Register(builder);
TechniciansModule.Register(builder);
ServiceRequestsModule.Register(builder);
WorkOrdersModule.Register(builder);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
