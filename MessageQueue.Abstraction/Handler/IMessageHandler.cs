namespace MessageQueue.Abstraction.Handler;

public interface IMessageHandler<in TMessage>
{
    Task ProcessAsync(TMessage message, CancellationToken cancellationToken);
}