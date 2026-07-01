using MessageQueue.Options;

namespace MessageQueue.Handler;

internal sealed record HandlerRegistration(
    Type ServiceType,
    Type MessageType,
    Options.Options Options);