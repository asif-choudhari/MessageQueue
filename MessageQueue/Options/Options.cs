using System.Text.Json;

namespace MessageQueue.Options;

public sealed class Options
{
    /// <summary>
    /// Azure Service Bus connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Queue name to listen to.
    /// </summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// Number of messages processed concurrently.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Whether the SDK should automatically complete messages.
    /// Recommended: false.
    /// </summary>
    public bool AutoCompleteMessages { get; set; } = false;

    /// <summary>
    /// Number of messages to prefetch.
    /// Optional optimization.
    /// </summary>
    public int PrefetchCount { get; set; } = 0;

    /// <summary>
    /// JSON options used to deserialize message bodies before invoking handlers.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = JsonSerializerOptions.Default;
}
