namespace AutoNomX.Domain.Interfaces;

/// <summary>Abstraction for publishing and subscribing to domain events.</summary>
public interface IEventBus
{
    /// <summary>Publishes a payload to the specified channel.</summary>
    Task PublishAsync(string channel, string payload, CancellationToken ct = default);
    /// <summary>Subscribes to events on the specified channel.</summary>
    Task SubscribeAsync(string channel, Func<string, Task> handler, CancellationToken ct = default);
}
