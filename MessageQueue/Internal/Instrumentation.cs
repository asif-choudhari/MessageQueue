using System.Diagnostics;
using System.Diagnostics.Metrics;
using Azure.Messaging.ServiceBus;

namespace MessageQueue.Internal;

/// <summary>
/// The library's ActivitySource and Meter, following OpenTelemetry messaging conventions.
/// When the application configures no listeners the Start/Record calls are near-free, so the
/// library instruments unconditionally and stays free of OpenTelemetry package dependencies.
/// </summary>
internal static class Instrumentation
{
    /// <summary>W3C trace parent stamped on outgoing messages so consumers join the trace.</summary>
    public const string DiagnosticIdProperty = "Diagnostic-Id";

    private static readonly string? Version =
        typeof(Instrumentation).Assembly.GetName().Version?.ToString();

    private static readonly ActivitySource ActivitySource =
        new(MessageQueueDiagnostics.ActivitySourceName, Version);

    private static readonly Meter Meter = new(MessageQueueDiagnostics.MeterName, Version);

    private static readonly Counter<long> SentCounter = Meter.CreateCounter<long>(
        "messagequeue.messages.sent",
        description: "Number of messages sent to a queue.");

    private static readonly Counter<long> ProcessedCounter = Meter.CreateCounter<long>(
        "messagequeue.messages.processed",
        description: "Number of messages handled successfully.");

    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>(
        "messagequeue.messages.failed",
        description: "Number of messages that failed processing or had no handler.");

    private static readonly Histogram<double> HandlerDuration = Meter.CreateHistogram<double>(
        "messagequeue.handler.duration",
        unit: "ms",
        description: "Time spent handling a single message.");

    public static Activity? StartPublish(string queueName, string messageType)
    {
        var activity = ActivitySource.StartActivity($"{queueName} publish", ActivityKind.Producer);
        if (activity is null) return null;

        activity.SetTag("messaging.system", "servicebus");
        activity.SetTag("messaging.operation", "publish");
        activity.SetTag("messaging.destination.name", queueName);
        activity.SetTag("messagequeue.message_type", messageType);
        return activity;
    }

    public static Activity? StartProcess(string queueName, string messageType, ServiceBusReceivedMessage message)
    {
        // Continue the producer's trace when the message carries a W3C trace parent.
        var parentContext = default(ActivityContext);
        if (message.ApplicationProperties.TryGetValue(DiagnosticIdProperty, out var value) &&
            value is string diagnosticId)
            ActivityContext.TryParse(diagnosticId, null, isRemote: true, out parentContext);

        var activity = ActivitySource.StartActivity(
            $"{queueName} process", ActivityKind.Consumer, parentContext);
        if (activity is null) return null;

        activity.SetTag("messaging.system", "servicebus");
        activity.SetTag("messaging.operation", "process");
        activity.SetTag("messaging.destination.name", queueName);
        activity.SetTag("messaging.message.id", message.MessageId);
        activity.SetTag("messagequeue.message_type", messageType);
        return activity;
    }

    public static void RecordSent(string queueName, string messageType, long count = 1) =>
        SentCounter.Add(count, Tags(queueName, messageType));

    public static void RecordProcessed(string queueName, string messageType, double elapsedMs)
    {
        var tags = Tags(queueName, messageType);
        ProcessedCounter.Add(1, tags);
        HandlerDuration.Record(elapsedMs, tags);
    }

    public static void RecordFailed(string queueName, string messageType, string reason)
    {
        var tags = Tags(queueName, messageType);
        tags.Add("reason", reason);
        FailedCounter.Add(1, tags);
    }

    private static TagList Tags(string queueName, string messageType) => new()
    {
        { "queue", queueName },
        { "message_type", messageType }
    };
}
