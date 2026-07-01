namespace MessageQueue.Abstraction.Listener;

public interface IServiceBusListener
{
    Task StartAsync(CancellationToken cancellationToken);
    
    Task StopAsync(CancellationToken cancellationToken);
}