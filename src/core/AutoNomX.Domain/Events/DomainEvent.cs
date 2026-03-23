using MediatR;

namespace AutoNomX.Domain.Events;

/// <summary>
/// Tüm domain event'ler için temel sınıf.
/// </summary>
public abstract record DomainEvent : INotification
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
