using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.TestNode;

public static class TestNodeEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workflows/{workflowId:guid}/nodes/{nodeId}/test", async (
            Guid workflowId,
            string nodeId,
            JsonElement request,
            TestNodeHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, nodeId, ownerId, request, ct);
            if (!result.IsSuccess)
                return result.StatusCode == 404
                    ? Results.NotFound(new { error = result.Error })
                    : Results.BadRequest(new { error = result.Error });
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("TestNode")
        .WithTags("Workflows");
    }
}
