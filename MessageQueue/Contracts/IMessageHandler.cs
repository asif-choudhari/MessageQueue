namespace MessageQueue.Contracts;

/// <summary>
/// Handles messages of type <typeparamref name="TMessage"/> received from a queue.
/// Implement this interface and register it with
/// <c>AddMessageQueue(...).AddHandler&lt;THandler&gt;("queue-name")</c>.
/// </summary>
/// <remarks>
/// Handlers are resolved from a fresh dependency-injection scope per message, so scoped
/// dependencies (such as a <c>DbContext</c>) behave as expected.
/// </remarks>
/// <typeparam name="TMessage">The message payload type this handler consumes.</typeparam>
public interface IMessageHandler<in TMessage>
{
    /// <summary>
    /// Handles a single deserialized message.
    /// </summary>
    /// <remarks>
    /// Returning normally completes the message (removes it from the queue). Throwing abandons
    /// it, so the broker redelivers it and dead-letters it once the queue's max delivery count
    /// is exceeded.
    /// </remarks>
    /// <param name="message">The deserialized message payload.</param>
    /// <param name="cancellationToken">Signalled when processing is being cancelled or the host is shutting down.</param>
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
