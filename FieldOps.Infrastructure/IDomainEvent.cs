namespace FieldOps.Infrastructure;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
