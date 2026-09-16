using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace FieldOps.Modules.Identity.Features.GetUser;

public static class GetUserHandler
{
    public static async Task<Result<GetUserResponse>> Handle(
        GetUserQuery query,
        UserManager<User> userManager,
        CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(query.Id.ToString());

        if (user is null)
            return Result<GetUserResponse>.Failure("User not found", 404);

        return Result<GetUserResponse>.Success(new GetUserResponse(
            user.Id,
            user.Email!,
            user.FullName,
            user.CreatedAt));
    }
}

public record GetUserResponse(
    string Id,
    string Email,
    string FullName,
    DateTime CreatedAt
);
