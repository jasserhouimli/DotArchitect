using Reflow.Infrastructure.Results;
using Reflow.Infrastructure.Runs;

namespace Reflow.Modules.WorkflowExecution.Features.StartWorkflowRun;

public class StartWorkflowRunHandler(IWorkflowRunStarter starter)
{
    public Task<Result<Guid>> Handle(Guid workflowId, Guid userId, CancellationToken ct)
    {
        return starter.StartRunAsync(workflowId, userId, null, ct);
    }
}
