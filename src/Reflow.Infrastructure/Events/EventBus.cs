using Microsoft.Extensions.DependencyInjection;

namespace Reflow.Infrastructure.Events;

public interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default);
}

public class InProcessEventBus(IServiceProvider services) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>();
        foreach (var handler in handlers)
            await handler.HandleAsync(@event, ct);
    }
}
