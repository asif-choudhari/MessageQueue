namespace MessageQueue.Abstraction.Processor;

public interface IMessageProcessor
{
    Task StartProcessingAsync(CancellationToken cancellationToken = default);
    
    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}