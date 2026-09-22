using System.Security.Claims;
using FluentValidation;
using Reflow.Modules.WorkflowDesign.Features.CreateWorkflow;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.WorkflowDesign.Features.CreateWorkflow;

public static class CreateWorkflowEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/workflows", async (
            CreateWorkflowRequest request,
            IValidator<CreateWorkflowRequest> validator,
            CreateWorkflowHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var ownerId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await handler.Handle(request, ownerId, ct);

            return result.StatusCode switch
            {
                201 => Results.Created($"/api/v1/workflows/{result.Value}", new { id = result.Value }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .RequireAuthorization()
        .WithName("CreateWorkflow")
        .WithTags("Workflows");
    }
}
