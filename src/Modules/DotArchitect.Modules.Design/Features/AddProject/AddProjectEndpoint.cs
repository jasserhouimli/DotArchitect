using DotArchitect.Modules.Design.Features.AddProject;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.AddProject;

public static class AddProjectEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/designs/{designId:guid}/projects", async (
            Guid designId,
            AddProjectRequest request,
            IValidator<AddProjectRequest> validator,
            AddProjectHandler handler,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.Handle(designId, request, ct);
            return result.StatusCode switch
            {
                201 => Results.Created($"/api/v1/designs/{designId}/projects/{result.Value}", new { id = result.Value }),
                404 => Results.NotFound(new { error = result.Error }),
                409 => Results.Conflict(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("AddProject")
        .WithTags("Design");
    }
}
