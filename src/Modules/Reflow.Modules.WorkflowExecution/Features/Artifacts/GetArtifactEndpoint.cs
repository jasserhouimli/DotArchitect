using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowExecution.Features.Artifacts;

public static class GetArtifactEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/runs/{runId:guid}/artifacts/{nodeId}", async (
            Guid runId, string nodeId, GetArtifactHandler handler, HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(runId, nodeId, userId, ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            var (content, contentType, fileName) = result.Value;
            return Results.File(Encoding.UTF8.GetBytes(content), contentType, fileName);
        }).RequireAuthorization().WithName("GetArtifact").WithTags("Runs");
    }
}
