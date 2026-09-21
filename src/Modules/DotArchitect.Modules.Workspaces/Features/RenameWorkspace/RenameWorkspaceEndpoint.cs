using System.Security.Claims;
using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Workspaces.Features.RenameWorkspace;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Workspaces.Features.RenameWorkspace;

public static class RenameWorkspaceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPatch("/api/v1/workspaces/{workspaceId:guid}", async (
            Guid workspaceId,
            RenameWorkspaceRequest request,
            IValidator<RenameWorkspaceRequest> validator,
            RenameWorkspaceHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(workspaceId, request, ownerId, ct);

            return result.StatusCode switch
            {
                204 => Results.NoContent(),
                404 => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("RenameWorkspace")
        .WithTags("Workspaces");
    }
}
