using Azure.Messaging.ServiceBus;

namespace MessageQueue.Internal;

/// <summary>
/// Deserializes a raw message body and invokes the matching handler within the given scope.
/// Captured once at registration so the processor performs no per-message reflection.
/// </summary>
internal delegate Task HandlerInvoker(
    IServiceProvider serviceProvider,
    BinaryData body,
    CancellationToken cancellationToken);

/// <summary>
/// One registered handler subscription: where to listen, which message type it consumes,
/// how to tune the processor, and how to dispatch a received message.
/// </summary>
internal sealed record HandlerRegistration(
    string ConnectionString,
    string QueueName,
    string MessageTypeName,
    ServiceBusProcessorOptions ProcessorOptions,
    HandlerInvoker Invoke);
