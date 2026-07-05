using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MessageQueue.Internal;

/// <summary>
/// Runs one <see cref="ServiceBusProcessor"/> per queue. Registrations on the same queue are
/// multiplexed: incoming messages are routed to the right handler by the message-type header the
/// dispatcher stamps on every send. Each message is handled in a fresh DI scope and settled
/// explicitly — completed on success, abandoned on failure so the broker redelivers and
/// dead-letters once the queue's max delivery count is exceeded.
/// </summary>
internal sealed class MessageProcessor(
    IServiceScopeFactory scopeFactory,
    ServiceBusClientProvider clients,
    IEnumerable<HandlerRegistration> registrations,
    ILogger<MessageProcessor> logger)
    : IMessageProcessor, IAsyncDisposable, IDisposable
{
    private readonly List<ServiceBusProcessor> _processors = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;

    public async Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_started) return;

            foreach (var group in registrations.GroupBy(r => (r.ConnectionString, r.QueueName)))
            {
                var subscription = QueueSubscription.Create(group.Key.QueueName, [.. group]);

                var processorOptions = subscription.Handlers[0].ProcessorOptions;
                processorOptions.AutoCompleteMessages = false; // the library settles explicitly

                var processor = clients
                    .Get(group.Key.ConnectionString)
                    .CreateProcessor(group.Key.QueueName, processorOptions);

                processor.ProcessMessageAsync += args => OnMessageAsync(args, subscription);
                processor.ProcessErrorAsync += args => OnErrorAsync(args, subscription);

                _processors.Add(processor);
                await processor.StartProcessingAsync(cancellationToken);

                logger.LogInformation(
                    "Listening on queue {Queue} with {HandlerCount} handler(s).",
                    subscription.QueueName,
                    subscription.Handlers.Length);
            }

            _started = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopProcessingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var processor in _processors)
            {
                await processor.StopProcessingAsync(cancellationToken);
            }

            _started = false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args, QueueSubscription subscription)
    {
        var message = args.Message;
        var registration = subscription.Resolve(message, out var messageTypeName);

        if (registration is null)
        {
            logger.LogWarning(
                "No handler on queue {Queue} for message type {MessageType} (id {MessageId}); dead-lettering.",
                subscription.QueueName,
                messageTypeName,
                message.MessageId);

            Instrumentation.RecordFailed(
                subscription.QueueName, messageTypeName ?? "unknown", "no_handler");

            await args.DeadLetterMessageAsync(
                message,
                "NoHandlerRegistered",
                $"No handler registered on queue '{subscription.QueueName}' for message type '{messageTypeName}'.",
                args.CancellationToken);

            return;
        }

        using var activity = Instrumentation.StartProcess(
            subscription.QueueName, registration.MessageTypeName, message);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await registration.Invoke(scope.ServiceProvider, message.Body, args.CancellationToken);
            await args.CompleteMessageAsync(message, args.CancellationToken);

            Instrumentation.RecordProcessed(
                subscription.QueueName,
                registration.MessageTypeName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            logger.LogDebug(
                "Handled {MessageType} {MessageId} on queue {Queue}.",
                registration.MessageTypeName,
                message.MessageId,
                subscription.QueueName);
        }
        catch (Exception ex) when (!args.CancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Instrumentation.RecordFailed(
                subscription.QueueName, registration.MessageTypeName, "handler_exception");

            logger.LogError(
                ex,
                "Handler failed for message {MessageId} on queue {Queue} (delivery {DeliveryCount}); abandoning for retry.",
                message.MessageId,
                subscription.QueueName,
                message.DeliveryCount);

            // Abandon (not dead-letter): the broker redelivers, then dead-letters automatically
            // once the queue's max delivery count is exceeded.
            await args.AbandonMessageAsync(message, cancellationToken: CancellationToken.None);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args, QueueSubscription subscription)
    {
        logger.LogError(
            args.Exception,
            "Service Bus error on queue {Queue} ({ErrorSource}).",
            subscription.QueueName,
            args.ErrorSource);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _processors)
        {
            await processor.DisposeAsync();
        }

        _gate.Dispose();
    }

    /// <summary>Supports containers disposed synchronously (tests, console apps).</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>
    /// All handlers listening on one queue, indexed by message type for routing.
    /// </summary>
    private sealed class QueueSubscription
    {
        public string QueueName { get; }
        public HandlerRegistration[] Handlers { get; }
        private readonly Dictionary<string, HandlerRegistration> _byMessageType;

        private QueueSubscription(
            string queueName,
            HandlerRegistration[] handlers,
            Dictionary<string, HandlerRegistration> byMessageType)
        {
            QueueName = queueName;
            Handlers = handlers;
            _byMessageType = byMessageType;
        }

        public static QueueSubscription Create(string queueName, HandlerRegistration[] handlers)
        {
            var byType = new Dictionary<string, HandlerRegistration>();
            foreach (var handler in handlers)
            {
                if (!byType.TryAdd(handler.MessageTypeName, handler))
                    throw new InvalidOperationException(
                        $"Queue '{queueName}' has more than one handler registered for message type '{handler.MessageTypeName}'.");
            }

            return new QueueSubscription(queueName, handlers, byType);
        }

        /// <summary>
        /// Picks the handler for <paramref name="message"/>. A queue with a single handler
        /// accepts everything (so messages from foreign producers without the type header still
        /// work); multi-handler queues route by the type header, falling back to the subject.
        /// </summary>
        public HandlerRegistration? Resolve(ServiceBusReceivedMessage message, out string? messageTypeName)
        {
            messageTypeName =
                message.ApplicationProperties.TryGetValue(MessageHeaders.MessageType, out var value) &&
                value is string typeName
                    ? typeName
                    : message.Subject;

            if (Handlers.Length == 1) return Handlers[0];

            return messageTypeName is null ? null : _byMessageType.GetValueOrDefault(messageTypeName);
        }
    }
}
