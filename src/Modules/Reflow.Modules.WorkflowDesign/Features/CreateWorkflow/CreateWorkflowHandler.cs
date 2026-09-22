using Reflow.Infrastructure.Results;
using Reflow.Modules.WorkflowDesign.Domain;
using Reflow.Modules.WorkflowDesign.Persistence;

namespace Reflow.Modules.WorkflowDesign.Features.CreateWorkflow;

public class CreateWorkflowHandler(WorkflowDesignDbContext db)
{
    public async Task<Result<Guid>> Handle(CreateWorkflowRequest request, Guid ownerId, CancellationToken ct)
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            OwnerId = ownerId,
            Status = WorkflowStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Workflows.Add(workflow);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(workflow.Id, 201);
    }
}
