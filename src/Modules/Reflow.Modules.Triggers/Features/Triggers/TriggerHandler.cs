using Reflow.Infrastructure.Results;
using Reflow.Modules.Triggers.Domain;
using Reflow.Modules.Triggers.Persistence;
using Reflow.Modules.Triggers.Services;
using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.Triggers.Features.Triggers;

public class TriggerHandler(TriggersDbContext db, WorkflowDesignDbContext designDb)
{
    public async Task<List<TriggerDto>?> ListAsync(Guid workflowId, Guid userId, CancellationToken ct)
    {
        var owns = await designDb.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == userId, ct);
        if (!owns) return null;

        return await db.Triggers
            .AsNoTracking()
            .Where(t => t.WorkflowId == workflowId && t.OwnerId == userId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new TriggerDto(t.Id, t.WorkflowId, (int)t.Kind, t.Name, t.IsEnabled,
                t.CronExpression, t.Timezone, (int)t.OverlapPolicy, t.NextRunAt, t.LastFiredAt,
                t.CreatedAt, t.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<Result<TriggerDto>> CreateScheduleAsync(
        Guid workflowId, CreateScheduleRequest request, Guid userId, CancellationToken ct)
    {
        var owns = await designDb.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == userId, ct);
        if (!owns) return Result<TriggerDto>.Failure("Workflow not found", 404);

        var error = ValidateSchedule(request.Name, request.CronExpression, request.Timezone, request.OverlapPolicy);
        if (error is not null) return Result<TriggerDto>.Failure(error, 400);

        var trigger = new Trigger
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            OwnerId = userId,
            Kind = TriggerKind.Schedule,
            Name = request.Name.Trim(),
            IsEnabled = request.IsEnabled,
            CronExpression = request.CronExpression.Trim(),
            Timezone = request.Timezone,
            OverlapPolicy = (OverlapPolicy)request.OverlapPolicy,
            NextRunAt = CronSchedule.GetNextOccurrence(
                request.CronExpression.Trim(), request.Timezone, DateTime.UtcNow)
        };
        db.Triggers.Add(trigger);
        await db.SaveChangesAsync(ct);
        return Result<TriggerDto>.Success(ToDto(trigger), 201);
    }

    public async Task<Result<TriggerDto>> UpdateScheduleAsync(
        Guid workflowId, Guid triggerId, UpdateScheduleRequest request, Guid userId, CancellationToken ct)
    {
        var trigger = await db.Triggers.FirstOrDefaultAsync(
            t => t.Id == triggerId && t.WorkflowId == workflowId && t.OwnerId == userId
                && t.Kind == TriggerKind.Schedule, ct);
        if (trigger is null) return Result<TriggerDto>.Failure("Trigger not found", 404);

        var name = request.Name ?? trigger.Name;
        var cron = request.CronExpression ?? trigger.CronExpression ?? string.Empty;
        var timezone = request.Timezone ?? trigger.Timezone ?? "UTC";
        var overlap = request.OverlapPolicy.HasValue ? (OverlapPolicy)request.OverlapPolicy.Value : trigger.OverlapPolicy;

        var error = ValidateSchedule(name, cron, timezone, (int)overlap);
        if (error is not null) return Result<TriggerDto>.Failure(error, 400);

        var scheduleChanged = cron.Trim() != trigger.CronExpression || timezone != trigger.Timezone;
        trigger.Name = name.Trim();
        trigger.CronExpression = cron.Trim();
        trigger.Timezone = timezone;
        trigger.OverlapPolicy = overlap;
        if (request.IsEnabled.HasValue)
            trigger.IsEnabled = request.IsEnabled.Value;
        if (scheduleChanged)
            trigger.NextRunAt = CronSchedule.GetNextOccurrence(cron.Trim(), timezone, DateTime.UtcNow);
        trigger.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result<TriggerDto>.Success(ToDto(trigger));
    }

    public async Task<Result<WebhookCreatedDto>> CreateWebhookAsync(
        Guid workflowId, CreateWebhookRequest request, string baseUrl, Guid userId, CancellationToken ct)
    {
        var owns = await designDb.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == userId, ct);
        if (!owns) return Result<WebhookCreatedDto>.Failure("Workflow not found", 404);
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<WebhookCreatedDto>.Failure("Name is required", 400);

        var token = WebhookToken.Generate();
        var trigger = new Trigger
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            OwnerId = userId,
            Kind = TriggerKind.Webhook,
            Name = request.Name.Trim(),
            IsEnabled = true,
            SecretTokenHash = WebhookToken.Hash(token)
        };
        db.Triggers.Add(trigger);
        await db.SaveChangesAsync(ct);

        return Result<WebhookCreatedDto>.Success(
            new WebhookCreatedDto(trigger.Id, trigger.Name, $"{baseUrl.TrimEnd('/')}/api/v1/hooks/{token}"), 201);
    }

    public async Task<Result<WebhookCreatedDto>> RegenerateWebhookAsync(
        Guid workflowId, Guid triggerId, string baseUrl, Guid userId, CancellationToken ct)
    {
        var trigger = await db.Triggers.FirstOrDefaultAsync(
            t => t.Id == triggerId && t.WorkflowId == workflowId && t.OwnerId == userId
                && t.Kind == TriggerKind.Webhook, ct);
        if (trigger is null) return Result<WebhookCreatedDto>.Failure("Trigger not found", 404);

        var token = WebhookToken.Generate();
        trigger.SecretTokenHash = WebhookToken.Hash(token);
        trigger.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result<WebhookCreatedDto>.Success(
            new WebhookCreatedDto(trigger.Id, trigger.Name, $"{baseUrl.TrimEnd('/')}/api/v1/hooks/{token}"));
    }

    public async Task<Result> DeleteAsync(Guid workflowId, Guid triggerId, Guid userId, CancellationToken ct)
    {
        var trigger = await db.Triggers.FirstOrDefaultAsync(
            t => t.Id == triggerId && t.WorkflowId == workflowId && t.OwnerId == userId, ct);
        if (trigger is null) return Result.Failure("Trigger not found", 404);

        db.Triggers.Remove(trigger);
        await db.SaveChangesAsync(ct);
        return Result.Success(200);
    }

    private static string? ValidateSchedule(string name, string cron, string timezone, int overlap)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required";
        if (name.Trim().Length > 200)
            return "Name must be 200 characters or fewer";
        if (!CronSchedule.TryParse(cron.Trim(), out var cronError))
            return $"Invalid cron expression: {cronError}";
        if (!CronSchedule.IsValidTimezone(timezone))
            return $"Unknown timezone: {timezone}";
        if (overlap != 0 && overlap != 1)
            return "Overlap policy must be 0 (skip) or 1 (queue)";
        return null;
    }

    private static TriggerDto ToDto(Trigger t) => new(t.Id, t.WorkflowId, (int)t.Kind, t.Name,
        t.IsEnabled, t.CronExpression, t.Timezone, (int)t.OverlapPolicy, t.NextRunAt, t.LastFiredAt,
        t.CreatedAt, t.UpdatedAt);
}

public record CreateScheduleRequest(string Name, string CronExpression, string Timezone, int OverlapPolicy, bool IsEnabled);

public record UpdateScheduleRequest(string? Name, string? CronExpression, string? Timezone, int? OverlapPolicy, bool? IsEnabled);

public record CreateWebhookRequest(string Name);

public record TriggerDto(Guid Id, Guid WorkflowId, int Kind, string Name, bool IsEnabled,
    string? CronExpression, string? Timezone, int OverlapPolicy,
    DateTime? NextRunAt, DateTime? LastFiredAt, DateTime CreatedAt, DateTime UpdatedAt);

public record WebhookCreatedDto(Guid Id, string Name, string Url);
