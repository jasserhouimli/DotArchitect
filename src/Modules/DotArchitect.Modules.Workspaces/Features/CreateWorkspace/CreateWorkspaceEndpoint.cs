using System.Security.Claims;
using DotArchitect.Infrastructure.Results;
using DotArchitect.Modules.Workspaces.Features.CreateWorkspace;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DotArchitect.Modules.Workspaces.Features.CreateWorkspace;

public static class CreateWorkspaceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workspaces", async (
            CreateWorkspaceRequest request,
            IValidator<CreateWorkspaceRequest> validator,
            CreateWorkspaceHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(request, ownerId, ct);

            return result.StatusCode switch
            {
                201 => Results.Created($"/api/v1/workspaces/{result.Value}", new { id = result.Value }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("CreateWorkspace")
        .WithTags("Workspaces");
    }
}
