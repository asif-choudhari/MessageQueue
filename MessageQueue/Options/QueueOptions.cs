using System.Text.Json;

namespace MessageQueue;

/// <summary>
/// Per-registration settings shared by handlers and dispatchers. Values are inherited from the
/// root <see cref="MessageQueueOptions"/> and can be overridden per queue.
/// </summary>
public abstract class QueueOptions
{
    /// <summary>
    /// The queue this registration is bound to. Set from the registration call.
    /// </summary>
    public string QueueName { get; internal set; } = string.Empty;

    /// <summary>
    /// Azure Service Bus connection string. Inherited from the root options; override only to
    /// target a different namespace for this queue.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// JSON options used to serialize and deserialize message bodies for this queue.
    /// Inherited from the root options.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = JsonSerializerOptions.Default;

    /// <summary>
    /// Throws a descriptive <see cref="ArgumentException"/> when required settings are missing.
    /// </summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(QueueName))
            throw new ArgumentException("A queue name is required.");

        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentException(
                $"ConnectionString is required. Set it in AddMessageQueue(...) or override it on the registration for queue '{QueueName}'.");
    }
}
