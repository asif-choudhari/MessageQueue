# MessageQueue

A lightweight, dependency-injection–first wrapper over [Azure Service Bus](https://learn.microsoft.com/azure/service-bus-messaging/).
Configure the connection once, register strongly-typed **handlers** (consumers) and **dispatchers** (producers) through one fluent builder — consumption starts automatically with the host.

```csharp
builder.Services
    .AddMessageQueue(connectionString)
    .AddDispatcher<OrderCreated>("orders")
    .AddHandler<OrderCreatedHandler>("orders");
```

## Install

```bash
dotnet add package MessageQueue
```

One package, one project, one namespace. Target framework: **.NET 10**. Everything lives under
`MessageQueue`; `AddMessageQueue` is available without any extra `using`.

## Sending — dispatchers typed by message

Map each message type to its queue once, then inject `IMessageDispatcher<TMessage>` — no marker
classes needed.

```csharp
builder.Services
    .AddMessageQueue(builder.Configuration.GetConnectionString("ServiceBus")!)
    .AddDispatcher<OrderCreated>("orders")
    .AddDispatcher<InvoiceIssued>("invoices");
```

```csharp
public sealed class OrderService(IMessageDispatcher<OrderCreated> dispatcher)
{
    public Task PlaceAsync(OrderCreated order, CancellationToken ct = default)
        => dispatcher.SendAsync(order, ct);
}

public sealed class BillingService(IMessageDispatcher<InvoiceIssued> dispatcher)
{
    public Task IssueAsync(InvoiceIssued invoice, CancellationToken ct = default)
        => dispatcher.SendAsync(invoice, ct);
}
```

Dispatchers also support:

```csharp
await dispatcher.SendAsync(order, messageId: order.Id.ToString(), ct);   // explicit id (de-duplication)
await dispatcher.SendBatchAsync(orders, ct);                             // efficient broker batching
long seq = await dispatcher.ScheduleAsync(order, enqueueAt, ct);         // deliver later
await dispatcher.CancelScheduledAsync(seq, ct);                          // change of plans
```

### Same message type, multiple queues

Every dispatcher is also registered **keyed by its queue name**. Register the type once per
queue and pick the queue at the injection site:

```csharp
builder.Services
    .AddMessageQueue(connectionString)
    .AddDispatcher<WeatherForecast>("queue.1")
    .AddDispatcher<WeatherForecast>("queue.2");
```

```csharp
public sealed class Client1([FromKeyedServices("queue.1")] IMessageDispatcher<WeatherForecast> bus);
public sealed class Client2([FromKeyedServices("queue.2")] IMessageDispatcher<WeatherForecast> bus);
```

Plain `IMessageDispatcher<WeatherForecast>` injection keeps working while the type maps to a
single queue; once it maps to several, resolving it plainly fails with an error telling you
exactly which keys are available — it never silently guesses a queue.

## Receiving — handlers

```csharp
public record OrderCreated(Guid OrderId, decimal Total);

public sealed class OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    : IMessageHandler<OrderCreated>
{
    public Task HandleAsync(OrderCreated message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order {OrderId} for {Total:C}", message.OrderId, message.Total);
        return Task.CompletedTask;
    }
}
```

```csharp
builder.Services
    .AddMessageQueue(connectionString)
    .AddHandler<OrderCreatedHandler>("orders");    // message type inferred from the handler
```

- Each message is handled in a **fresh DI scope** — scoped dependencies (e.g. a `DbContext`) just work.
- **Several message types can share one queue**: register multiple handlers on the same queue name and
  messages are routed to the right handler by type. A queue with a single handler accepts everything,
  including messages from producers that don't stamp type metadata.
- No reflection on the hot path — dispatch is bound once at registration.

```csharp
builder.Services
    .AddMessageQueue(connectionString)
    .AddHandler<OrderCreatedHandler>("events")     // OrderCreated → OrderCreatedHandler
    .AddHandler<InvoiceIssuedHandler>("events");   // InvoiceIssued → InvoiceIssuedHandler
```

## Lifecycle

Consumption **starts automatically** when the host starts and stops gracefully on shutdown.
In an ASP.NET Core app the consumer runs in parallel with the request pipeline; in a Worker Service it is the whole app. Nothing to wire up.

Prefer manual control? Opt out and drive it yourself:

```csharp
builder.Services.AddMessageQueue(o =>
{
    o.ConnectionString = connectionString;
    o.AutoStartProcessing = false;
});

var app = builder.Build();
await app.Services.StartMessageProcessingAsync();
await app.RunAsync();
await app.Services.StopMessageProcessingAsync();
```

## Observability

The library is instrumented out of the box and lights up automatically when the application
configures OpenTelemetry — it has **no OpenTelemetry dependency** itself and costs nearly
nothing when no listener is attached.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource(MessageQueueDiagnostics.ActivitySourceName)   // "MessageQueue"
        .AddSource("Azure.Messaging.ServiceBus"))                // SDK's own spans, optional
    .WithMetrics(m => m
        .AddMeter(MessageQueueDiagnostics.MeterName));           // "MessageQueue"
```

- **Traces** — a `Producer` span per send/schedule/batch and a `Consumer` span per handled
  message. Outgoing messages carry a W3C `Diagnostic-Id`, so the consumer span joins the
  producer's trace end-to-end.
- **Metrics** — `messagequeue.messages.sent`, `messagequeue.messages.processed`,
  `messagequeue.messages.failed` (tagged with `reason`), and the
  `messagequeue.handler.duration` histogram (ms). All tagged with `queue` and `message_type`.
- **Logs** — everything flows through `ILogger` (category `MessageQueue.*`): listener start,
  handler failures, Service Bus errors at `Error`/`Warning`; per-message send/handle at `Debug`.
  If the host has no logging configured, the library quietly no-ops.

## Configuration

Root options (shared by everything registered through the same `AddMessageQueue` call):

```csharp
builder.Services.AddMessageQueue(o =>
{
    o.ConnectionString = connectionString;
    o.JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    o.ClientOptions.RetryOptions.MaxRetries = 5;    // applies to the pooled clients
});
```

Per-queue overrides on each registration:

```csharp
.AddHandler<OrderCreatedHandler>("orders", o =>
{
    o.ProcessorOptions.MaxConcurrentCalls = 10;
    o.ProcessorOptions.PrefetchCount = 20;
})
.AddDispatcher<AuditEvent>("audit", o =>
{
    o.ConnectionString = otherNamespaceConnectionString;   // different namespace for this queue
});
```

| Option | Level | Description |
| --- | --- | --- |
| `ConnectionString` | root, overridable per queue | Service Bus connection string. **Required.** |
| `JsonSerializerOptions` | root, overridable per queue | JSON (de)serialization of message bodies. |
| `ClientOptions` | root | Transport, retry, identity for the pooled `ServiceBusClient`s. |
| `AutoStartProcessing` | root | Start consuming with the host (default `true`). |
| `ProcessorOptions` | handler | Concurrency, prefetch, max delivery, etc. |

Missing required values fail fast at registration with a descriptive `ArgumentException`.

## Behavior

- **Connections are pooled** — one `ServiceBusClient` per connection string and one sender per
  queue, shared by all dispatchers and processors.
- **Serialization** — UTF-8 JSON, `ContentType: application/json`; the CLR type name is stamped on `Subject` and the `MessageType` application property (used for routing).
- **Success** — handler returns → message completed.
- **Failure** — handler throws → message **abandoned** and logged; the broker redelivers it and dead-letters automatically once the queue's max delivery count is exceeded.
- **Unroutable** — on a multi-handler queue, a message whose type has no handler is dead-lettered with reason `NoHandlerRegistered`.

## License

[MIT](LICENSE)
