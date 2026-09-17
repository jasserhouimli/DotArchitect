using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using FieldOps.Modules.Identity.Domain;

namespace FieldOps.Modules.Identity.Features.Register;

public static class RegisterEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/register", async (
            HttpContext http,
            RegisterRequest request,
            UserManager<User> userManager,
            CancellationToken ct) =>
        {
            var result = await RegisterHandler.Handle(request, userManager, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("Register")
        .RequireRateLimiting("auth")
        .Produces(201)
        .Produces(409);
    }
}
