using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;

namespace MessageQueue.Internal;

/// <summary>
/// Pools one <see cref="ServiceBusClient"/> per connection string and one
/// <see cref="ServiceBusSender"/> per queue, so all dispatchers and processors sharing a
/// namespace reuse a single AMQP connection and dispatchers targeting the same queue share a
/// sender. Owns and disposes everything it creates; disposed by the container on shutdown.
/// </summary>
internal sealed class ServiceBusClientProvider(ServiceBusClientOptions clientOptions)
    : IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, ServiceBusClient> _clients = new();
    private readonly ConcurrentDictionary<(string ConnectionString, string QueueName), ServiceBusSender> _senders = new();

    /// <summary>
    /// Returns the shared client for <paramref name="connectionString"/>, creating it on first use.
    /// </summary>
    public ServiceBusClient Get(string connectionString) =>
        _clients.GetOrAdd(connectionString, cs => new ServiceBusClient(cs, clientOptions));

    /// <summary>
    /// Returns the shared sender for the queue, creating it on first use.
    /// </summary>
    public ServiceBusSender GetSender(string connectionString, string queueName) =>
        _senders.GetOrAdd(
            (connectionString, queueName),
            key => Get(key.ConnectionString).CreateSender(key.QueueName));

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }

        foreach (var client in _clients.Values)
        {
            await client.DisposeAsync();
        }

        _senders.Clear();
        _clients.Clear();
    }

    /// <summary>Supports containers disposed synchronously (tests, console apps).</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
