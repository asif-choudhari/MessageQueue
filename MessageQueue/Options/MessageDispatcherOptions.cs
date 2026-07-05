namespace MessageQueue;

/// <summary>
/// Per-dispatcher settings. Transport and retry are configured once on the root
/// <see cref="MessageQueueOptions.ClientOptions"/>; this type carries the queue binding and the
/// per-queue overrides from <see cref="QueueOptions"/>.
/// </summary>
public sealed class MessageDispatcherOptions : QueueOptions;
