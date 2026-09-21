namespace DotArchitect.Infrastructure;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
