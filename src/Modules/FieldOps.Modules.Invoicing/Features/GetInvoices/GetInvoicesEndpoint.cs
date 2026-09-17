using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using FieldOps.Modules.Invoicing.Domain;

namespace FieldOps.Modules.Invoicing.Features.GetInvoices;

public static class GetInvoicesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/invoices", async (
            GetInvoicesHandler handler,
            int? page,
            int? pageSize,
            Guid? customerId,
            string? status,
            string? search,
            CancellationToken ct) =>
        {
            var query = new GetInvoicesQuery(
                page ?? 1, pageSize ?? 20,
                customerId,
                Enum.TryParse<InvoiceStatus>(status, true, out var s) ? s : null,
                search);

            var result = await handler.Handle(query, ct);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("GetInvoices")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200);
    }
}
