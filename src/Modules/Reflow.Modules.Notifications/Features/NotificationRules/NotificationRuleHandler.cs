using Reflow.Infrastructure.Results;
using Reflow.Modules.Notifications.Domain;
using Reflow.Modules.Notifications.Persistence;
using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.Notifications.Features.NotificationRules;

public class NotificationRuleHandler(
    WorkflowDesignDbContext designDb,
    NotificationsDbContext db)
{
    public async Task<RuleDto?> GetAsync(Guid workflowId, Guid userId, CancellationToken ct)
    {
        var owns = await designDb.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == userId, ct);
        if (!owns) return null;

        var rule = await db.NotificationRules
            .FirstOrDefaultAsync(r => r.WorkflowId == workflowId && r.OwnerId == userId, ct);
        if (rule is null) return null;

        return new RuleDto(rule.WorkflowId, rule.NotifyOnSuccess, rule.NotifyOnFailure,
            rule.RejectsAbove, rule.WebhookUrl, rule.UpdatedAt);
    }

    public async Task<Result<RuleDto>> UpsertAsync(Guid workflowId, UpsertRuleRequest request, Guid userId, CancellationToken ct)
    {
        var owns = await designDb.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == userId, ct);
        if (!owns) return Result<RuleDto>.Failure("Workflow not found", 404);

        if (!request.NotifyOnSuccess && !request.NotifyOnFailure && request.RejectsAbove is null)
            return Result<RuleDto>.Failure("Enable at least one trigger: success, failure, or a reject threshold", 400);

        if (request.RejectsAbove.HasValue && request.RejectsAbove.Value < 0)
            return Result<RuleDto>.Failure("Reject threshold must be 0 or more", 400);

        if (!string.IsNullOrWhiteSpace(request.WebhookUrl))
        {
            if (!Uri.TryCreate(request.WebhookUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return Result<RuleDto>.Failure("Webhook URL must be an absolute http(s) URL", 400);
        }

        var rule = await db.NotificationRules
            .FirstOrDefaultAsync(r => r.WorkflowId == workflowId && r.OwnerId == userId, ct);

        if (rule is null)
        {
            rule = new NotificationRule
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow
            };
            db.NotificationRules.Add(rule);
        }

        rule.NotifyOnSuccess = request.NotifyOnSuccess;
        rule.NotifyOnFailure = request.NotifyOnFailure;
        rule.RejectsAbove = request.RejectsAbove;
        rule.WebhookUrl = string.IsNullOrWhiteSpace(request.WebhookUrl) ? null : request.WebhookUrl.Trim();
        rule.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result<RuleDto>.Success(new RuleDto(rule.WorkflowId, rule.NotifyOnSuccess,
            rule.NotifyOnFailure, rule.RejectsAbove, rule.WebhookUrl, rule.UpdatedAt));
    }

    public async Task<Result> DeleteAsync(Guid workflowId, Guid userId, CancellationToken ct)
    {
        var rule = await db.NotificationRules
            .FirstOrDefaultAsync(r => r.WorkflowId == workflowId && r.OwnerId == userId, ct);
        if (rule is null) return Result.Failure("Rule not found", 404);

        db.NotificationRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return Result.Success(200);
    }
}

public record UpsertRuleRequest(bool NotifyOnSuccess, bool NotifyOnFailure, int? RejectsAbove, string? WebhookUrl);

public record RuleDto(Guid WorkflowId, bool NotifyOnSuccess, bool NotifyOnFailure, int? RejectsAbove, string? WebhookUrl, DateTime UpdatedAt);
