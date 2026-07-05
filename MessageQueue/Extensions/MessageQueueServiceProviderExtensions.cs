using MessageQueue.Contracts;
using MessageQueue.Options;
using Microsoft.Extensions.DependencyInjection;

namespace MessageQueue.Extensions;

/// <summary>
/// Manual lifecycle helpers, for applications that set
/// <see cref="MessageQueueOptions.AutoStartProcessing"/> to <c>false</c>.
/// </summary>
public static class MessageQueueServiceProviderExtensions
{
    /// <param name="provider">The application's service provider (e.g. <c>app.Services</c>).</param>
    extension(IServiceProvider provider)
    {
        /// <summary>
        /// Starts listening on every registered queue.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel start-up.</param>
        public Task StartMessageProcessingAsync(CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(provider);
            return provider.GetRequiredService<IMessageProcessor>().StartProcessingAsync(cancellationToken);
        }

        /// <summary>
        /// Stops listening on all registered queues. In-flight messages are allowed to finish.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the shutdown wait.</param>
        public Task StopMessageProcessingAsync(CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(provider);
            return provider.GetRequiredService<IMessageProcessor>().StopProcessingAsync(cancellationToken);
        }
    }
}
