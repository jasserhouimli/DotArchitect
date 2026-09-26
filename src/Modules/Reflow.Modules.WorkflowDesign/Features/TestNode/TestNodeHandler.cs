using System.Text.Json;
using Reflow.Infrastructure.Data;
using Reflow.Infrastructure.Expressions;
using Reflow.Infrastructure.Results;
using Reflow.Modules.DataProcessing;
using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.TestNode;

public class TestNodeHandler(WorkflowDesignDbContext db, TaskHandlerRegistry registry)
{
    public const int MaxPreviewRows = 20;
    public const int MaxInputRows = 200;

    // Nodes with side effects outside the preview have no meaningful test run.
    private static readonly HashSet<string> Untestable = new(StringComparer.OrdinalIgnoreCase)
    {
        "workflow.call"
    };

    public async Task<Result<TestNodeResult>> Handle(
        Guid workflowId, string nodeId, Guid ownerId, JsonElement request, CancellationToken ct)
    {
        var node = await db.WorkflowNodes
            .FirstOrDefaultAsync(n => n.WorkflowId == workflowId && n.NodeId == nodeId
                && db.Workflows.Any(w => w.Id == workflowId && w.OwnerId == ownerId), ct);
        if (node is null)
            return Result<TestNodeResult>.Failure("Node not found", 404);

        if (Untestable.Contains(node.NodeType))
            return Result<TestNodeResult>.Failure(
                $"Node type '{node.NodeType}' cannot be test-run in isolation.", 400);

        if (!registry.TryGet(node.NodeType, out var handler) || handler is null)
            return Result<TestNodeResult>.Failure($"No handler for type {node.NodeType}", 400);

        string? payloadJson = null;
        if (request.TryGetProperty("samplePayload", out var payload)
            && payload.ValueKind != JsonValueKind.Null)
            payloadJson = payload.GetRawText();

        var inputs = new List<Dataset>();
        if (request.TryGetProperty("inputs", out var inputsEl)
            && inputsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var table in inputsEl.EnumerateArray())
            {
                var ds = new Dataset();
                if (table.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
                    foreach (var c in cols.EnumerateArray())
                        ds.Columns.Add(c.ValueKind == JsonValueKind.String ? c.GetString() ?? string.Empty : c.GetRawText());
                if (table.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var row in rowsEl.EnumerateArray())
                    {
                        if (ds.Rows.Count >= MaxInputRows) break;
                        if (row.ValueKind != JsonValueKind.Array) continue;
                        ds.Rows.Add(row.EnumerateArray().Select(v => v.ValueKind switch
                        {
                            JsonValueKind.String => v.GetString(),
                            JsonValueKind.Null => null,
                            _ => v.GetRawText()
                        }).ToList());
                    }
                }
                ds.Quality.InputCount = ds.Rows.Count;
                inputs.Add(ds);
            }
        }

        var expression = ExpressionResolver.ResolveConfigJson(
            node.ConfigJson,
            new ExpressionContext("manual", null, payloadJson, Guid.NewGuid(), 0));
        if (!expression.Ok)
            return Result<TestNodeResult>.Success(new TestNodeResult(
                false, node.NodeType, null, null, 0, null, expression.ValueOrError), 200);

        var context = new TaskExecutionContext(
            Guid.NewGuid(), Guid.NewGuid(), node.NodeId, node.NodeType,
            expression.ValueOrError, inputs, workflowId, payloadJson);

        TaskExecutionResult result;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            result = await handler.ExecuteAsync(context, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<TestNodeResult>.Success(new TestNodeResult(
                false, node.NodeType, null, null, 0, null, "Test run timed out after 30s."), 200);
        }
        catch (Exception ex)
        {
            return Result<TestNodeResult>.Success(new TestNodeResult(
                false, node.NodeType, null, null, 0, null, ex.Message), 200);
        }

        List<string>? columns = null;
        List<List<string?>>? rows = null;
        var totalRows = 0;
        if (result.OutputJson is not null && Dataset.TryFromJson(result.OutputJson) is { } output)
        {
            columns = output.Columns;
            rows = output.Rows.Take(MaxPreviewRows).ToList();
            totalRows = output.Rows.Count;
        }

        return Result<TestNodeResult>.Success(new TestNodeResult(
            result.IsSuccess, node.NodeType, columns, rows, totalRows, result.Log, result.Error), 200);
    }

    public record TestNodeResult(
        bool Success,
        string NodeType,
        List<string>? Columns,
        List<List<string?>>? Rows,
        int TotalRows,
        string? Log,
        string? Error);
}
