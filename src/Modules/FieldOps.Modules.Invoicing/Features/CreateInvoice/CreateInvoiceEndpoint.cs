using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Invoicing.Features.CreateInvoice;

public static class CreateInvoiceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/invoices", async (
            CreateInvoiceHandler handler,
            CreateInvoiceRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("CreateInvoice")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(201)
        .Produces(400);
    }
}
