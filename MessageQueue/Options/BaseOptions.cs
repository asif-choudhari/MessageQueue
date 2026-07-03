using System.Text.Json;

namespace MessageQueue.Options;

public class BaseOptions
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
    /// JSON options used to deserialize message bodies before invoking handlers.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = JsonSerializerOptions.Default;
}
