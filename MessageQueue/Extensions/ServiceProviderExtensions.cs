using Microsoft.Extensions.DependencyInjection;

namespace MessageQueue;

/// <summary>
/// Manual lifecycle helpers, for applications that set
/// <see cref="MessageQueueOptions.AutoStartProcessing"/> to <c>false</c>.
/// </summary>
public static class MessageQueueServiceProviderExtensions
{
    /// <summary>
    /// Starts listening on every registered queue.
    /// </summary>
    /// <param name="provider">The application's service provider (e.g. <c>app.Services</c>).</param>
    /// <param name="cancellationToken">Token used to cancel start-up.</param>
    public static Task StartMessageProcessingAsync(
        this IServiceProvider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.GetRequiredService<IMessageProcessor>().StartProcessingAsync(cancellationToken);
    }

    /// <summary>
    /// Stops listening on all registered queues. In-flight messages are allowed to finish.
    /// </summary>
    /// <param name="provider">The application's service provider (e.g. <c>app.Services</c>).</param>
    /// <param name="cancellationToken">Token used to cancel the shutdown wait.</param>
    public static Task StopMessageProcessingAsync(
        this IServiceProvider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.GetRequiredService<IMessageProcessor>().StopProcessingAsync(cancellationToken);
    }
}
