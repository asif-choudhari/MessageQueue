using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace MessageQueue.Options;

/// <summary>
/// Root settings shared by every handler and dispatcher registered through one
/// <c>AddMessageQueue</c> call. Individual registrations inherit these values and may override
/// the connection string and JSON options per queue.
/// </summary>
public sealed class MessageQueueOptions
{
    /// <summary>
    /// Azure Service Bus connection string shared by all registrations. Required.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Default JSON options used to serialize and deserialize message bodies.
    /// Defaults to <see cref="JsonSerializerOptions.Default"/>.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = JsonSerializerOptions.Default;

    /// <summary>
    /// Transport, retry and identity options applied to the underlying
    /// <see cref="ServiceBusClient"/>s. Clients are pooled — one per connection string.
    /// </summary>
    public ServiceBusClientOptions ClientOptions { get; set; } = new();

    /// <summary>
    /// When <c>true</c> (default), a hosted service starts consuming when the host starts and
    /// stops on shutdown — in a web app this runs in parallel with the request pipeline. Set to
    /// <c>false</c> to control the lifecycle yourself via
    /// <c>IServiceProvider.StartMessageProcessingAsync()</c>.
    /// </summary>
    public bool AutoStartProcessing { get; set; } = true;
}
