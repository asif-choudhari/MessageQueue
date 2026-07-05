using Azure.Messaging.ServiceBus;

namespace MessageQueue.Options;

/// <summary>
/// Per-handler settings, including tuning for the underlying Service Bus processor.
/// </summary>
public sealed class MessageHandlerOptions : QueueOptions
{
    /// <summary>
    /// Processor tuning: <see cref="ServiceBusProcessorOptions.MaxConcurrentCalls"/>,
    /// <see cref="ServiceBusProcessorOptions.PrefetchCount"/>, etc.
    /// </summary>
    /// <remarks>
    /// <see cref="ServiceBusProcessorOptions.AutoCompleteMessages"/> is always forced off — the
    /// library settles every message explicitly (complete on success, abandon on failure).
    /// When several handlers share one queue, the first registration's processor options apply.
    /// </remarks>
    public ServiceBusProcessorOptions ProcessorOptions { get; set; } = new();
}
