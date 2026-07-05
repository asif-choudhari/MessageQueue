using MessageQueue.Contracts;
using MessageQueue.Options;
using Microsoft.Extensions.DependencyInjection;

namespace MessageQueue;

/// <summary>
/// Fluent builder for registering handlers and typed dispatchers against a shared connection.
/// Obtained from <c>services.AddMessageQueue(...)</c>.
/// </summary>
public interface IMessageQueueBuilder
{
    /// <summary>The underlying service collection, exposed for advanced scenarios.</summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Maps <typeparamref name="TMessage"/> to <paramref name="queueName"/> and registers its
    /// dispatcher. Inject <c>IMessageDispatcher&lt;TMessage&gt;</c> to send. The dispatcher is
    /// also resolvable keyed by the queue name, so the same message type may be registered for
    /// several queues and picked with <c>[FromKeyedServices("queue-name")]</c>; plain injection
    /// then fails with a descriptive error instead of guessing a queue.
    /// </summary>
    /// <typeparam name="TMessage">The message type this dispatcher sends.</typeparam>
    /// <param name="queueName">The queue to send to.</param>
    /// <param name="configure">Optional per-queue overrides (connection string, JSON options).</param>
    /// <returns>The same builder, for chaining.</returns>
    IMessageQueueBuilder AddDispatcher<TMessage>(
        string queueName,
        Action<MessageDispatcherOptions>? configure = null)
        where TMessage : class;

    /// <summary>
    /// Registers <typeparamref name="THandler"/> on <paramref name="queueName"/>, inferring the
    /// message type from the <see cref="IMessageHandler{TMessage}"/> it implements.
    /// </summary>
    /// <typeparam name="THandler">
    /// The handler implementation. Must implement exactly one <see cref="IMessageHandler{TMessage}"/>;
    /// handlers of several message types use the explicit overload once per message type.
    /// </typeparam>
    /// <param name="queueName">The queue to listen on.</param>
    /// <param name="configure">Optional per-queue overrides (processor tuning, connection string, JSON options).</param>
    /// <returns>The same builder, for chaining.</returns>
    IMessageQueueBuilder AddHandler<THandler>(
        string queueName,
        Action<MessageHandlerOptions>? configure = null)
        where THandler : class;

    /// <summary>
    /// Registers <typeparamref name="THandler"/> as the consumer of <typeparamref name="TMessage"/>
    /// on <paramref name="queueName"/>. Several message types can share one queue — messages are
    /// routed to the right handler by their type.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation. Registered as scoped.</typeparam>
    /// <typeparam name="TMessage">The message type consumed.</typeparam>
    /// <param name="queueName">The queue to listen on.</param>
    /// <param name="configure">Optional per-queue overrides (processor tuning, connection string, JSON options).</param>
    /// <returns>The same builder, for chaining.</returns>
    IMessageQueueBuilder AddHandler<THandler, TMessage>(
        string queueName,
        Action<MessageHandlerOptions>? configure = null)
        where THandler : class, IMessageHandler<TMessage>
        where TMessage : class;
}
