using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reflow.Api.DesignTime;

public class WorkflowDesignDbContextFactory : IDesignTimeDbContextFactory<WorkflowDesignDbContext>
{
    public WorkflowDesignDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkflowDesignDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=reflow;Username=postgres;Password=root",
            b => b.MigrationsAssembly("Reflow.Api"));
        return new WorkflowDesignDbContext(optionsBuilder.Options);
    }
}
