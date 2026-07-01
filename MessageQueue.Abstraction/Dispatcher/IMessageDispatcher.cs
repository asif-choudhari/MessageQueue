namespace MessageQueue.Abstraction.Dispatcher;

public interface IMessageDispatcher
{
    Task SendAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default);

    Task SendAsync<TMessage>(
        TMessage message,
        string? messageId,
        CancellationToken cancellationToken = default);
}
