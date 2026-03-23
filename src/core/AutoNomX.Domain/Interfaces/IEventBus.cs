namespace AutoNomX.Domain.Interfaces;

public interface IEventBus
{
    Task PublishAsync(string channel, string payload, CancellationToken ct = default);
    Task SubscribeAsync(string channel, Func<string, Task> handler, CancellationToken ct = default);
}
