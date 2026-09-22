using Reflow.Infrastructure.Results;
using Reflow.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Reflow.Modules.Identity.Features.GetUser;

public class GetUserHandler(UserManager<User> userManager)
{
    public async Task<Result<GetUserResponse>> Handle(
        GetUserQuery query,
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
