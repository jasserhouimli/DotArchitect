using System.Security.Claims;
using Reflow.Infrastructure.Results;
using Reflow.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Reflow.Modules.Identity.Features.GetUser;

public class GetCurrentUserHandler(UserManager<User> userManager)
{
    public async Task<Result<GetCurrentUserResponse>> Handle(
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Result<GetCurrentUserResponse>.Failure("Unauthorized", 401);

        var found = await userManager.FindByIdAsync(userId);

        if (found is null)
            return Result<GetCurrentUserResponse>.Failure("User not found", 404);

        return Result<GetCurrentUserResponse>.Success(new GetCurrentUserResponse(
            found.Id,
            found.Email!,
            found.FullName,
            found.CreatedAt));
    }
}

public record GetCurrentUserResponse(
    string Id,
    string Email,
    string FullName,
    DateTime CreatedAt
);
