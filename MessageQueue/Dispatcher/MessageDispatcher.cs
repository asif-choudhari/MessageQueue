using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MessageQueue.Abstraction.Dispatcher;
using MessageQueue.Options;
using Microsoft.Extensions.Options;

namespace MessageQueue.Dispatcher;

internal sealed class MessageDispatcher : IMessageDispatcher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly Options.Options _options;

    public MessageDispatcher(
        ServiceBusClient client,
        IOptions<Options.Options> options)
    {
        _options = options.Value;
        _sender = client.CreateSender(_options.QueueName);
    }

    public Task SendAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(message, messageId: null, cancellationToken);
    }

    public async Task SendAsync<TMessage>(
        TMessage message,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = typeof(TMessage).FullName ?? typeof(TMessage).Name;
        var body = JsonSerializer.SerializeToUtf8Bytes(
            message,
            _options.JsonSerializerOptions);

        var serviceBusMessage = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            MessageId = messageId ?? Guid.NewGuid().ToString("N"),
            Subject = messageType
        };

        serviceBusMessage.ApplicationProperties["MessageType"] = messageType;

        await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _sender.DisposeAsync();
    }
}
