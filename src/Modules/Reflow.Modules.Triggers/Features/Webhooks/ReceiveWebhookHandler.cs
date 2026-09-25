using Reflow.Infrastructure.Results;
using Reflow.Infrastructure.Runs;
using Reflow.Modules.Triggers.Domain;
using Reflow.Modules.Triggers.Persistence;
using Reflow.Modules.Triggers.Services;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.Triggers.Features.Webhooks;

public class ReceiveWebhookHandler(
    TriggersDbContext db,
    IWorkflowRunStarter starter,
    IRunMonitor monitor)
{
    public const int MaxQueuedRuns = 5;

    public async Task<Result<Guid>> Handle(string token, string? payloadJson, CancellationToken ct)
    {
        var hash = WebhookToken.Hash(token);
        var trigger = await db.Triggers.FirstOrDefaultAsync(
            t => t.Kind == TriggerKind.Webhook && t.SecretTokenHash == hash, ct);
        if (trigger is null || !trigger.IsEnabled)
            return Result<Guid>.Failure("Unknown webhook", 404);

        var active = await monitor.CountActiveRunsAsync(trigger.WorkflowId, trigger.OwnerId, ct);
        if (active >= MaxQueuedRuns)
            return Result<Guid>.Failure("Too many active runs for this workflow", 429);

        return await starter.StartRunAsync(trigger.WorkflowId, trigger.OwnerId,
            new RunTrigger("webhook", trigger.Name, payloadJson), ct);
    }
}
