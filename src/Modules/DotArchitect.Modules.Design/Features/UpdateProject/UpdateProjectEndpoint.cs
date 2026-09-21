using DotArchitect.Modules.Design.Features.UpdateProject;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.UpdateProject;

public static class UpdateProjectEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPatch("/api/v1/designs/{designId:guid}/projects/{projectId:guid}", async (
            Guid designId, Guid projectId,
            UpdateProjectRequest request,
            IValidator<UpdateProjectRequest> validator,
            UpdateProjectHandler handler,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.Handle(designId, projectId, request, ct);
            return result.StatusCode switch
            {
                204 => Results.NoContent(),
                404 => Results.NotFound(new { error = result.Error }),
                409 => Results.Conflict(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("UpdateProject")
        .WithTags("Design");
    }
}
