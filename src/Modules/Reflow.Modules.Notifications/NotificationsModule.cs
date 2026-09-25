using Reflow.Infrastructure.Events;
using Reflow.Modules.Notifications.Features.NotificationRules;
using Reflow.Modules.Notifications.Features.Notifications;
using Reflow.Modules.Notifications.Persistence;
using Reflow.Modules.Notifications.Services;
using Reflow.Modules.Notifications.Subscribers;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Reflow.Modules.Notifications;

public static class NotificationsModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Reflow");

        builder.Services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString));
        builder.Services.AddScoped<NotificationsDbContext>();

        builder.Services.AddScoped<WebhookSender>();
        builder.Services.AddScoped<NotificationRuleHandler>();
        builder.Services.AddScoped<NotificationInboxHandler>();
        builder.Services.AddScoped<IEventHandler<RunFinishedEvent>, RunFinishedSubscriber>();
        builder.Services.AddScoped<IEventHandler<WorkflowDeletedEvent>, WorkflowDeletedSubscriber>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        NotificationRuleEndpoint.Map(app);
        NotificationInboxEndpoint.Map(app);
    }
}
