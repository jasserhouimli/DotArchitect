using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Reflow.Infrastructure.Realtime;

public interface IRunAccessChecker
{
    Task<bool> CanAccessRunAsync(Guid runId, Guid userId, CancellationToken ct = default);
}

[Authorize]
public class RunHub : Hub
{
    private readonly IRunAccessChecker _access;

    public RunHub(IRunAccessChecker access) => _access = access;

    public static string GroupFor(Guid runId) => $"run:{runId:N}";

    public static string UserGroup(Guid userId) => $"user:{userId:N}";

    public override async Task OnConnectedAsync()
    {
        var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    public async Task JoinRun(Guid runId)
    {
        var userId = UserIdOrThrow();
        if (!await _access.CanAccessRunAsync(runId, userId))
            throw new HubException("Run not found");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(runId));
    }

    public async Task LeaveRun(Guid runId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(runId));
    }

    private Guid UserIdOrThrow()
    {
        var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var userId))
            throw new HubException("Unauthorized");
        return userId;
    }
}
