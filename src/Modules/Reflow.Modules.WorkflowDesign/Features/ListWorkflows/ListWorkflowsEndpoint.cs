using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.ListWorkflows;

public static class ListWorkflowsEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/workflows", async (
            ListWorkflowsHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(ownerId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ListWorkflows")
        .WithTags("Workflows");
    }
}
