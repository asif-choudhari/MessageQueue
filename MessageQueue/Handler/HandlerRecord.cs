using MessageQueue.Options;

namespace MessageQueue.Handler;

internal sealed record HandlerRecord(
    Type ServiceType,
    Type MessageType,
    MessageHandlerOptions MessageHandlerOptions);