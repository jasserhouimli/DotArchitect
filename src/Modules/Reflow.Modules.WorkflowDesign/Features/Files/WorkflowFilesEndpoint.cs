using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Reflow.Modules.WorkflowDesign.Features.Files;

public static class WorkflowFilesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workflows/{workflowId:guid}/files", async (
            Guid workflowId,
            IFormFile file,
            UploadWorkflowFileHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.Json(new { error = "No file uploaded" }, statusCode: 400);

            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await using var stream = file.OpenReadStream();
            var result = await handler.Handle(workflowId, ownerId, file.FileName, stream, ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Json(result.Value, statusCode: 201);
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithMetadata(new RequestSizeLimitAttribute(12 * 1024 * 1024))
        .WithName("UploadWorkflowFile")
        .WithTags("Workflows");

        app.MapGet("/api/v1/workflows/{workflowId:guid}/files", async (
            Guid workflowId,
            ListWorkflowFilesHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, ownerId, ct);
            if (result is null) return Results.NotFound(new { error = "Workflow not found" });
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ListWorkflowFiles")
        .WithTags("Workflows");

        app.MapDelete("/api/v1/workflows/{workflowId:guid}/files/{fileId}", async (
            Guid workflowId,
            string fileId,
            DeleteWorkflowFileHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workflowId, fileId, ownerId, ct);
            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
            return Results.Ok(new { message = "File deleted" });
        })
        .RequireAuthorization()
        .WithName("DeleteWorkflowFile")
        .WithTags("Workflows");
    }
}
