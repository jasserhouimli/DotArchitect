using Reflow.Modules.WorkflowExecution.Domain;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Persistence;

public class WorkflowExecutionDbContext : DbContext
{
    public WorkflowExecutionDbContext(DbContextOptions<WorkflowExecutionDbContext> options) : base(options) { }

    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<TaskRun> TaskRuns => Set<TaskRun>();
    public DbSet<TaskAttempt> TaskAttempts => Set<TaskAttempt>();
    public DbSet<ExecutionLog> ExecutionLogs => Set<ExecutionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("workflow_execution");

        modelBuilder.Entity<WorkflowRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.WorkflowId);
            e.HasIndex(x => x.Status);
        });
        modelBuilder.Entity<TaskRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.WorkflowRunId);
            e.HasIndex(x => new { x.WorkflowRunId, x.Status });
            e.Property(x => x.ConfigJson).HasMaxLength(20000);
            e.Property(x => x.RowVersion).IsRowVersion();
        });
        modelBuilder.Entity<TaskAttempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TaskRunId);
        });
        modelBuilder.Entity<ExecutionLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.WorkflowRunId);
        });
    }
}
