using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MessageQueue.Abstraction.Handler;
using Microsoft.Extensions.DependencyInjection;

namespace MessageQueue.Handler;

internal sealed class HandlerCollection(
    IServiceScopeFactory scopeFactory,
    IEnumerable<HandlerRegistration> registrations)
    : IHandlerCollection, IAsyncDisposable
{
    private readonly List<ServiceBusProcessor> _processors = [];
    
    public async Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in registrations)
        {
            var client = new ServiceBusClient(
                registration.Options.ConnectionString);

            var processor = client.CreateProcessor(
                registration.Options.QueueName);

            processor.ProcessMessageAsync += args =>
                ProcessAsync(args, registration);

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
        HandlerRegistration registration)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService(
            registration.ServiceType);

        await using var stream = args.Message.Body.ToStream();

        var message = await JsonSerializer.DeserializeAsync(
            stream,
            registration.MessageType,
            cancellationToken: args.CancellationToken);

        if (message is null)
        {
            await args.DeadLetterMessageAsync(args.Message);

            return;
        }

        var method = registration.ServiceType.GetMethod(
            nameof(IMessageHandler<object>.ProcessAsync));

        await (Task)method!.Invoke(handler,
        [
            message,
            args.CancellationToken
        ])!;

        await args.CompleteMessageAsync(args.Message);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _processors)
        {
            await processor.DisposeAsync();
        }
    }
}