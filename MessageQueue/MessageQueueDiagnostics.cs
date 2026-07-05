namespace MessageQueue;

/// <summary>
/// Names of the library's telemetry sources. Register them with your OpenTelemetry pipeline to
/// receive MessageQueue traces and metrics — the library emits them unconditionally with
/// near-zero overhead when no listener is attached, and needs no OpenTelemetry dependency itself.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t => t
///         .AddSource(MessageQueueDiagnostics.ActivitySourceName)
///         .AddSource("Azure.Messaging.ServiceBus"))     // the SDK's own spans, optional
///     .WithMetrics(m => m.AddMeter(MessageQueueDiagnostics.MeterName));
/// </code>
/// </example>
public static class MessageQueueDiagnostics
{
    /// <summary>Name of the <see cref="System.Diagnostics.ActivitySource"/> for publish/process spans.</summary>
    public const string ActivitySourceName = "MessageQueue";

    /// <summary>Name of the <see cref="System.Diagnostics.Metrics.Meter"/> for counters and histograms.</summary>
    public const string MeterName = "MessageQueue";
}
