using System.Text.Json;
using Reflow.Infrastructure.Data;
using Reflow.Infrastructure.Runs;
using Reflow.Modules.DataProcessing;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Reflow.Modules.WorkflowExecution.Handlers;

/// <summary>
/// Starts another workflow as a child run. Never blocks the worker thread:
/// wait mode suspends the task (Waiting) and a watcher re-queues it once the
/// child finishes. Never retried: re-running blindly would start duplicates;
/// instead a resumed task reuses its recorded child run id.
/// </summary>
public class WorkflowCallHandler(IServiceScopeFactory scopes) : ITaskHandler
{
    public string TaskType => "workflow.call";
    public bool SupportsRetry => false;

    public const int DefaultTimeoutSeconds = 100;
    public const int MaxTimeoutSeconds = 110;

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken ct)
    {
        JsonDocument config;
        try
        {
            config = JsonDocument.Parse(context.ConfigJson ?? "{}");
        }
        catch (JsonException ex)
        {
            return Fail($"Invalid config: {ex.Message}");
        }

        using (config)
        {
            var root = config.RootElement;
            if (!root.TryGetProperty("targetWorkflowId", out var targetEl)
                || targetEl.ValueKind != JsonValueKind.String
                || !Guid.TryParse(targetEl.GetString(), out var targetId))
                return Fail("Config requires 'targetWorkflowId' (workflow GUID).");

            var mode = root.TryGetProperty("mode", out var modeEl) && modeEl.ValueKind == JsonValueKind.String
                ? modeEl.GetString() ?? "wait"
                : "wait";
            var wait = !string.Equals(mode, "fireAndForget", StringComparison.OrdinalIgnoreCase);
            var timeoutSeconds = ParseTimeoutSeconds(context.ConfigJson);

            string? payloadJson = null;
            if (root.TryGetProperty("payload", out var payloadEl) && payloadEl.ValueKind != JsonValueKind.Null)
            {
                if (payloadEl.ValueKind != JsonValueKind.Object)
                    return Fail("Config 'payload' must be a JSON object.");
                payloadJson = payloadEl.GetRawText();
            }

            using var scope = scopes.CreateScope();
            var services = scope.ServiceProvider;
            var starter = services.GetRequiredService<IWorkflowRunStarter>();
            var execDb = services.GetRequiredService<WorkflowExecutionDbContext>();

            var task = await execDb.TaskRuns.FirstOrDefaultAsync(t => t.Id == context.TaskRunId, ct);
            if (task is null)
                return Fail("Task not found.");
            var parent = await execDb.WorkflowRuns
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == context.WorkflowRunId, ct);
            if (parent is null)
                return Fail("Parent run not found.");

            // Resumed after suspension: reuse the recorded child, never start another.
            if (task.WaitingOnRunId is Guid childId)
                return await CheckChildAsync(execDb, task, childId, timeoutSeconds, ct);

            var start = await starter.StartRunAsync(
                targetId, parent.CreatedBy, new RunTrigger("workflow", null, payloadJson), ct);
            if (!start.IsSuccess || start.Value == Guid.Empty)
                return Fail($"Could not start target workflow: {start.Error ?? "unknown error"}");

            childId = start.Value;
            if (!wait)
                return Success(childId, WorkflowRunStatus.Queued,
                    $"Started child run {Short(childId)} (fire-and-forget).");

            return Deferred(
                childId,
                $"Task {task.NodeId} suspended: waiting for child run {Short(childId)}.");
        }
    }

    internal static int ParseTimeoutSeconds(string? configJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(configJson ?? "{}");
            if (doc.RootElement.TryGetProperty("timeoutSeconds", out var t)
                && t.ValueKind == JsonValueKind.Number
                && t.TryGetInt32(out var seconds))
                return Math.Clamp(seconds, 5, MaxTimeoutSeconds);
        }
        catch (JsonException)
        {
        }
        return DefaultTimeoutSeconds;
    }

    private static async Task<TaskExecutionResult> CheckChildAsync(
        WorkflowExecutionDbContext execDb, TaskRun task, Guid childId, int timeoutSeconds, CancellationToken ct)
    {
        var child = await execDb.WorkflowRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == childId, ct);
        if (child is null)
            return Fail($"Child run {Short(childId)} disappeared.");
        if (child.Status == WorkflowRunStatus.Completed)
            return Success(childId, child.Status, $"Child run {Short(childId)} completed.");
        if (child.Status == WorkflowRunStatus.Failed)
            return Fail($"Child run {Short(childId)} failed: {child.Error}");
        if (child.Status == WorkflowRunStatus.Cancelled)
            return Fail($"Child run {Short(childId)} was cancelled.");

        var waitingSince = task.StartedAt ?? task.CreatedAt;
        if (DateTime.UtcNow - waitingSince > TimeSpan.FromSeconds(timeoutSeconds))
            return Fail($"Child run {Short(childId)} did not finish within {timeoutSeconds}s; it keeps running independently.");

        return Deferred(childId, $"Task {task.NodeId} suspended: waiting for child run {Short(childId)}.");
    }

    private static string Short(Guid id) => id.ToString("N")[..8];

    private static TaskExecutionResult Success(Guid childId, WorkflowRunStatus status, string log)
    {
        var ds = new Dataset();
        ds.Columns.Add("child_run_id");
        ds.Columns.Add("child_status");
        ds.Rows.Add(new List<string?> { childId.ToString(), status.ToString() });
        ds.Quality.InputCount = 0;
        ds.Quality.OutputCount = 1;
        return new TaskExecutionResult(true, null, ds.ToJson(), $"{log} [1 rows x 2 cols (in=0, rejected=0, dupes=0)]");
    }

    private static TaskExecutionResult Deferred(Guid childId, string log) =>
        new(false, null, null, log, false, childId);

    private static TaskExecutionResult Fail(string error) =>
        new(false, error, null, null);
}
