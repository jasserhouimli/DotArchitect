using DotArchitect.Modules.Design.Features.CreateDesign;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotArchitect.Modules.Design.Features.CreateDesign;

public static class CreateDesignEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workspaces/{workspaceId:guid}/designs", async (
            Guid workspaceId,
            CreateDesignRequest request,
            IValidator<CreateDesignRequest> validator,
            CreateDesignHandler handler,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.Handle(request, workspaceId, ct);
            return result.StatusCode switch
            {
                201 => Results.Created($"/api/v1/designs/{result.Value}", new { id = result.Value }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("CreateDesign")
        .WithTags("Design");
    }
}
