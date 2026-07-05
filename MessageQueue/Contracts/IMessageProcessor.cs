namespace MessageQueue.Contracts;

/// <summary>
/// Starts and stops consumption of all registered queues.
/// </summary>
/// <remarks>
/// By default consumption starts automatically with the host (see
/// <c>MessageQueueOptions.AutoStartProcessing</c>), so most applications never touch this
/// interface. Resolve it — or use the <c>StartMessageProcessingAsync</c> /
/// <c>StopMessageProcessingAsync</c> extensions on <see cref="IServiceProvider"/> — only when
/// managing the lifecycle manually.
/// </remarks>
public interface IMessageProcessor
{
    /// <summary>
    /// Begins listening on every registered queue and dispatching messages to their handlers.
    /// Idempotent: calling it while already started is a no-op.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel start-up.</param>
    Task StartProcessingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops listening on all queues. In-flight messages are allowed to finish.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the shutdown wait.</param>
    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}
