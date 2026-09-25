using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.WorkflowExecution.Persistence;

namespace Reflow.Modules.WorkflowExecution.Hubs;

[Authorize]
public class RunHub : Hub
{
    public static string GroupFor(Guid runId) => $"run:{runId:N}";

    public async Task JoinRun(Guid runId)
    {
        var userId = UserIdOrThrow();
        var db = Context.GetHttpContext()!.RequestServices.GetRequiredService<WorkflowExecutionDbContext>();
        var owned = await db.WorkflowRuns.AnyAsync(r => r.Id == runId && r.CreatedBy == userId);
        if (!owned)
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
