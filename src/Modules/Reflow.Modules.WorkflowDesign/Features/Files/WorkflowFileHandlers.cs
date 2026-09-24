using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Infrastructure.Storage;
using Reflow.Modules.WorkflowDesign.Persistence;

namespace Reflow.Modules.WorkflowDesign.Features.Files;

public class UploadWorkflowFileHandler(WorkflowDesignDbContext db, IUploadStore uploads)
{
    public async Task<Result<UploadedFile>> Handle(Guid workflowId, Guid ownerId, string fileName, Stream content, CancellationToken ct)
    {
        var owns = await db.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);
        if (!owns)
            return Result<UploadedFile>.Failure("Workflow not found", 404);

        try
        {
            var saved = await uploads.SaveAsync(workflowId, fileName, content, ct);
            return Result<UploadedFile>.Success(saved, 201);
        }
        catch (InvalidOperationException ex)
        {
            return Result<UploadedFile>.Failure(ex.Message, 400);
        }
    }
}

public class ListWorkflowFilesHandler(WorkflowDesignDbContext db, IUploadStore uploads)
{
    public async Task<IReadOnlyList<UploadedFile>?> Handle(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var owns = await db.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);
        if (!owns)
            return null;

        return await uploads.ListAsync(workflowId, ct);
    }
}

public class DeleteWorkflowFileHandler(WorkflowDesignDbContext db, IUploadStore uploads)
{
    public async Task<Result> Handle(Guid workflowId, string fileId, Guid ownerId, CancellationToken ct)
    {
        var owns = await db.Workflows.AnyAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);
        if (!owns)
            return Result.Failure("Workflow not found", 404);

        if (!uploads.IsValidFileId(fileId))
            return Result.Failure("Unknown file", 404);

        var referenced = await db.WorkflowVersions
            .AnyAsync(v => v.WorkflowId == workflowId && v.DefinitionJson.Contains(fileId), ct);
        if (referenced)
            return Result.Failure("File is referenced by a published version and cannot be deleted", 409);

        var deleted = await uploads.DeleteAsync(workflowId, fileId, ct);
        if (!deleted)
            return Result.Failure("File not found", 404);

        return Result.Success(200);
    }
}
