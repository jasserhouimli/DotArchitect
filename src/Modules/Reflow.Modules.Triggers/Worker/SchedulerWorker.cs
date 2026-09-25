using Reflow.Infrastructure.Runs;
using Reflow.Modules.Triggers.Domain;
using Reflow.Modules.Triggers.Persistence;
using Reflow.Modules.Triggers.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Reflow.Modules.Triggers.Worker;

public class SchedulerWorker(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<SchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SchedulerWorker started");
        var tickSeconds = configuration.GetValue<int?>("Triggers:TickSeconds") ?? 30;
        var tick = TimeSpan.FromSeconds(Math.Clamp(tickSeconds, 2, 600));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FireDueTriggers(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler tick failed");
            }

            try
            {
                await Task.Delay(tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task FireDueTriggers(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<TriggersDbContext>();
        var starter = sp.GetRequiredService<IWorkflowRunStarter>();
        var monitor = sp.GetRequiredService<IRunMonitor>();

        var now = DateTime.UtcNow;
        var due = await db.Triggers
            .Where(t => t.Kind == TriggerKind.Schedule && t.IsEnabled
                && t.NextRunAt != null && t.NextRunAt <= now)
            .ToListAsync(ct);

        foreach (var trigger in due)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await FireOneAsync(trigger, now, db, starter, monitor, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Schedule {TriggerId} failed to fire", trigger.Id);
            }
        }
    }

    private async Task FireOneAsync(
        Trigger trigger,
        DateTime now,
        TriggersDbContext db,
        IWorkflowRunStarter starter,
        IRunMonitor monitor,
        CancellationToken ct)
    {
        DateTime? next;
        try
        {
            next = CronSchedule.GetNextOccurrence(trigger.CronExpression!, trigger.Timezone ?? "UTC", now);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException || ex is InvalidTimeZoneException || ex is Cronos.CronFormatException)
        {
            logger.LogWarning("Disabling schedule {TriggerId}: invalid cron or timezone", trigger.Id);
            trigger.IsEnabled = false;
            trigger.NextRunAt = null;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (trigger.OverlapPolicy == OverlapPolicy.Skip)
        {
            var active = await monitor.CountActiveRunsAsync(trigger.WorkflowId, trigger.OwnerId, ct);
            if (active > 0)
            {
                logger.LogInformation("Skipping schedule {TriggerId}: {Active} active run(s)", trigger.Id, active);
                trigger.NextRunAt = next;
                await db.SaveChangesAsync(ct);
                return;
            }
        }

        var result = await starter.StartRunAsync(trigger.WorkflowId, trigger.OwnerId,
            new RunTrigger("schedule", trigger.Name, null), ct);

        trigger.LastFiredAt = now;
        trigger.NextRunAt = next;
        await db.SaveChangesAsync(ct);

        if (!result.IsSuccess)
            logger.LogWarning("Schedule {TriggerId} did not start a run: {Error}", trigger.Id, result.Error);
        else
            logger.LogInformation("Schedule {TriggerId} started run {RunId}", trigger.Id, result.Value);
    }
}
