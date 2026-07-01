using Azure.Messaging.ServiceBus;

namespace MessageQueue.Abstraction.Handler;

public interface IHandlerCollection
{
    Task StartProcessingAsync(CancellationToken cancellationToken = default);
    
    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}