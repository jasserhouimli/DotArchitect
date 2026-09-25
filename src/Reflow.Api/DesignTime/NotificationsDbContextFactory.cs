using Reflow.Modules.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reflow.Api.DesignTime;

public class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationsDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=reflow;Username=postgres;Password=root",
            b => b.MigrationsAssembly("Reflow.Api"));
        return new NotificationsDbContext(optionsBuilder.Options);
    }
}
