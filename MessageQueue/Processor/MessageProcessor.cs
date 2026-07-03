using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MessageQueue.Abstraction.Handler;
using MessageQueue.Abstraction.Processor;
using MessageQueue.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MessageQueue.Processor;

internal sealed class MessageProcessor(
    IServiceScopeFactory scopeFactory,
    IEnumerable<HandlerRecord> records,
    ILogger<MessageProcessor> logger)
    : IMessageProcessor, IAsyncDisposable
{
    private readonly List<ServiceBusProcessor> _processors = [];
    
    public async Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            var client = new ServiceBusClient(
                record.MessageHandlerOptions.ConnectionString);

            var processor = client.CreateProcessor(
                record.MessageHandlerOptions.QueueName,
                record.MessageHandlerOptions.ServiceBusProcessorOptions);

            processor.ProcessMessageAsync += args =>
                ProcessAsync(args, record);

            processor.ProcessErrorAsync += args =>
            {
                Console.WriteLine(args.Exception);

                return Task.CompletedTask;
            };

            _processors.Add(processor);

            await processor.StartProcessingAsync(cancellationToken);
        }
    }

    public async Task StopProcessingAsync(CancellationToken cancellationToken)
    {
        foreach (var processor in _processors)
        {
            await processor.StopProcessingAsync(cancellationToken);
        }
    }

    private async Task ProcessAsync(
        ProcessMessageEventArgs args,
        HandlerRecord record)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var messageType = record.MessageType;
            var serviceType = record.ServiceType;
            var handlerType = typeof(IMessageHandler<>).MakeGenericType(messageType);
            var handlers = scope.ServiceProvider.GetServices(handlerType);

            await using var stream = args.Message.Body.ToStream();
            var message = await JsonSerializer.DeserializeAsync(
                stream,
                messageType,
                record.MessageHandlerOptions.JsonSerializerOptions,
                args.CancellationToken);

            if (message is null) throw new InvalidOperationException("Deserialized message was null.");

            var method = handlerType.GetMethod(nameof(IMessageHandler<object>.ProcessAsync));

            var handler = handlers.SingleOrDefault(h => h!.GetType() == serviceType);
            if (handler is null)
                throw new InvalidOperationException(
                    $"No handler of type {serviceType.Name} registered for {messageType.Name}.");

            await (Task)method!.Invoke(handler, [message, args.CancellationToken])!;

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            await args.DeadLetterMessageAsync(args.Message, ex.Message, ex.ToString());
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _processors)
        {
            await processor.DisposeAsync();
        }
    }
}