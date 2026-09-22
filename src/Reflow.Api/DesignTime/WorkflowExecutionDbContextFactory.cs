using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reflow.Api.DesignTime;

public class WorkflowExecutionDbContextFactory : IDesignTimeDbContextFactory<WorkflowExecutionDbContext>
{
    public WorkflowExecutionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkflowExecutionDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=reflow;Username=postgres;Password=root",
            b => b.MigrationsAssembly("Reflow.Api"));
        return new WorkflowExecutionDbContext(optionsBuilder.Options);
    }
}
