using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MessageQueue.Contracts;
using MessageQueue.Options;
using Microsoft.Extensions.Logging;

namespace MessageQueue.Internal;

/// <summary>
/// Dispatcher for <typeparamref name="TMessage"/>, bound to the queue that type was registered
/// for. Draws its sender from the shared pool, emits publish spans and send metrics, and stamps
/// a W3C trace parent on every outgoing message so consumers join the trace.
/// </summary>
internal sealed class MessageDispatcher<TMessage>(
    ServiceBusClientProvider clients,
    MessageDispatcherOptions options,
    ILogger logger) : IMessageDispatcher<TMessage>
    where TMessage : class
{
    private static readonly string MessageTypeName =
        typeof(TMessage).FullName ?? typeof(TMessage).Name;

    private readonly ServiceBusSender _sender =
        clients.GetSender(options.ConnectionString, options.QueueName);

    private readonly string _queueName = options.QueueName;
    private readonly JsonSerializerOptions _json = options.JsonSerializerOptions;

    public Task SendAsync(TMessage message, CancellationToken cancellationToken = default) =>
        SendAsync(message, messageId: null, cancellationToken);

    public async Task SendAsync(
        TMessage message,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        using var activity = Instrumentation.StartPublish(_queueName, MessageTypeName);

        var serviceBusMessage = Build(message, messageId, activity?.Id);
        await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);

        Instrumentation.RecordSent(_queueName, MessageTypeName);
        logger.LogDebug(
            "Sent {MessageType} {MessageId} to queue {Queue}.",
            MessageTypeName,
            serviceBusMessage.MessageId,
            _queueName);
    }

    public async Task SendBatchAsync(
        IEnumerable<TMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        using var enumerator = messages.GetEnumerator();
        if (!enumerator.MoveNext()) return;

        using var activity = Instrumentation.StartPublish(_queueName, MessageTypeName);
        long sent = 0;

        var batch = await _sender.CreateMessageBatchAsync(cancellationToken);
        try
        {
            do
            {
                var message = Build(enumerator.Current, messageId: null, activity?.Id);

                if (batch.TryAddMessage(message)) continue;

                if (batch.Count == 0)
                    throw new InvalidOperationException(
                        "A single message exceeds the maximum Service Bus batch size.");

                // Batch is full: flush it and start a new one for the current message.
                sent += batch.Count;
                await _sender.SendMessagesAsync(batch, cancellationToken);
                batch.Dispose();
                batch = await _sender.CreateMessageBatchAsync(cancellationToken);

                if (!batch.TryAddMessage(message))
                    throw new InvalidOperationException(
                        "A single message exceeds the maximum Service Bus batch size.");
            } while (enumerator.MoveNext());

            if (batch.Count > 0)
            {
                sent += batch.Count;
                await _sender.SendMessagesAsync(batch, cancellationToken);
            }
        }
        finally
        {
            batch.Dispose();
        }

        Instrumentation.RecordSent(_queueName, MessageTypeName, sent);
        logger.LogDebug(
            "Sent batch of {Count} {MessageType} to queue {Queue}.",
            sent,
            MessageTypeName,
            _queueName);
    }

    public async Task<long> ScheduleAsync(
        TMessage message,
        DateTimeOffset enqueueAt,
        CancellationToken cancellationToken = default)
    {
        using var activity = Instrumentation.StartPublish(_queueName, MessageTypeName);

        var serviceBusMessage = Build(message, messageId: null, activity?.Id);
        var sequenceNumber = await _sender.ScheduleMessageAsync(
            serviceBusMessage, enqueueAt, cancellationToken);

        Instrumentation.RecordSent(_queueName, MessageTypeName);
        logger.LogDebug(
            "Scheduled {MessageType} {MessageId} on queue {Queue} for {EnqueueAt:O}.",
            MessageTypeName,
            serviceBusMessage.MessageId,
            _queueName,
            enqueueAt);

        return sequenceNumber;
    }

    public Task CancelScheduledAsync(
        long sequenceNumber,
        CancellationToken cancellationToken = default) =>
        _sender.CancelScheduledMessageAsync(sequenceNumber, cancellationToken);

    private ServiceBusMessage Build(TMessage message, string? messageId, string? traceParent)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = JsonSerializer.SerializeToUtf8Bytes(message, _json);

        var serviceBusMessage = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            MessageId = messageId ?? Guid.NewGuid().ToString("N"),
            Subject = MessageTypeName,
            ApplicationProperties = { [MessageHeaders.MessageType] = MessageTypeName }
        };

        if (traceParent is not null)
            serviceBusMessage.ApplicationProperties[Instrumentation.DiagnosticIdProperty] = traceParent;

        return serviceBusMessage;
    }
}
