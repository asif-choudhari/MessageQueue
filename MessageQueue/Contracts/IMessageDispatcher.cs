namespace MessageQueue.Contracts;

/// <summary>
/// Sends messages of type <typeparamref name="TMessage"/> to the queue that type was registered
/// for with <c>AddDispatcher&lt;TMessage&gt;("queue-name")</c>. Inject it and send — the
/// container hands each service the dispatcher for the message type it produces, no marker
/// types, keyed services or attributes.
/// </summary>
/// <example>
/// <code>
/// public sealed class OrderService(IMessageDispatcher&lt;OrderCreated&gt; dispatcher)
/// {
///     public Task PlaceAsync(OrderCreated order) =&gt; dispatcher.SendAsync(order);
/// }
/// </code>
/// </example>
/// <typeparam name="TMessage">
/// The message type this dispatcher sends. When the type is registered for a single queue,
/// inject <c>IMessageDispatcher&lt;TMessage&gt;</c> directly; when it is registered for several
/// queues, pick one with <c>[FromKeyedServices("queue-name")]</c>.
/// </typeparam>
public interface IMessageDispatcher<in TMessage>
    where TMessage : class
{
    /// <summary>
    /// Serializes and sends a message. A random message id is assigned.
    /// </summary>
    /// <param name="message">The payload to send. Must not be <c>null</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SendAsync(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes and sends a message with an explicit message id (useful for de-duplication).
    /// </summary>
    /// <param name="message">The payload to send. Must not be <c>null</c>.</param>
    /// <param name="messageId">The message id, or <c>null</c> to auto-generate one.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SendAsync(TMessage message, string? messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes and sends many messages efficiently, packing them into as few broker batches
    /// as possible.
    /// </summary>
    /// <param name="messages">The payloads to send.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SendBatchAsync(IEnumerable<TMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a message to become visible on the queue at <paramref name="enqueueAt"/>.
    /// </summary>
    /// <param name="message">The payload to schedule. Must not be <c>null</c>.</param>
    /// <param name="enqueueAt">The UTC time at which the message should be enqueued.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A sequence number that can be passed to <see cref="CancelScheduledAsync"/>.</returns>
    Task<long> ScheduleAsync(TMessage message, DateTimeOffset enqueueAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a message previously scheduled with <see cref="ScheduleAsync"/>.
    /// </summary>
    /// <param name="sequenceNumber">The sequence number returned by the schedule call.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task CancelScheduledAsync(long sequenceNumber, CancellationToken cancellationToken = default);
}
