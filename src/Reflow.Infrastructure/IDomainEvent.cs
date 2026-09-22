namespace Reflow.Infrastructure;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
