using Microsoft.Extensions.Hosting;

namespace MessageQueue.Internal;

/// <summary>
/// Hosted service that starts consumption when the host starts and stops it on shutdown. In a
/// web application it runs in parallel with the request pipeline; in a worker it is the whole
/// app. Registered only when <see cref="MessageQueueOptions.AutoStartProcessing"/> is enabled.
/// </summary>
internal sealed class MessageProcessingService(IMessageProcessor processor) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        processor.StartProcessingAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        processor.StopProcessingAsync(cancellationToken);
}
