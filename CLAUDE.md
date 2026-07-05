# CLAUDE.md

Guidance for Claude Code (and other AI assistants) working in this repository.

## ⚠️ Standing rule: keep docs in sync

**Whenever you change public API, behavior, project structure, or configuration, update both
[`README.md`](README.md) and this `CLAUDE.md` in the same change.** They are part of the
package's developer experience and must never drift from the code. If a change makes an example
wrong, fix the example.

## What this is

A lightweight, DI-first wrapper over **Azure Service Bus**, shipped as a **single** NuGet package
(`MessageQueue`) targeting **.NET 10**. One project, one assembly, one namespace (`MessageQueue`,
set via `RootNamespace`) — consumers need one `using MessageQueue;`. The `AddMessageQueue`
extensions deliberately live in the `Microsoft.Extensions.DependencyInjection` namespace (the
standard convention) so they are discoverable with zero usings.

There is intentionally no separate abstractions/contracts package. An earlier iteration split
`IMessageHandler`/`IMessageDispatcher`/`IMessageProcessor` into a `MessageQueue.Abstraction`
project; the maintainer asked to fold it back into one project because the split added
packaging overhead without a consumer who needed the separation. Do not reintroduce a second
project unless asked.

## Layout

- **Solution-level packaging** is centralized in [`Directory.Build.props`](Directory.Build.props):
  TFM, metadata, license (MIT), symbols, README/LICENSE packing (with `Visible="false"` so they
  don't appear inside the project tree in the IDE). Do not duplicate these in the csproj — it
  carries only `PackageId`, `Description`, `PackageTags`, and references.
- `MessageQueue/Contracts/` — the public contracts: `IMessageHandler.cs`, `IMessageDispatcher.cs`
  (`IMessageDispatcher<TMessage>`, typed by the message), `IMessageProcessor.cs`.
- `MessageQueue/`
  - `IMessageQueueBuilder.cs` — the public fluent builder contract.
  - `MessageQueueDiagnostics.cs` — public constants for the ActivitySource/Meter names
    (both `"MessageQueue"`), used by consumers to register OTel sources.
  - `Options/` — `MessageQueueOptions` (root), `QueueOptions` (base, validates fail-fast),
    `MessageHandlerOptions`, `MessageDispatcherOptions`.
  - `Extensions/` — `ServiceCollectionExtensions.cs` (`AddMessageQueue`, namespace
    `Microsoft.Extensions.DependencyInjection`), `ServiceProviderExtensions.cs`
    (`StartMessageProcessingAsync` / `StopMessageProcessingAsync`, namespace `MessageQueue`).
  - `Internal/` — everything non-public: `MessageQueueBuilder`, `ServiceBusClientProvider`
    (pools one `ServiceBusClient` per connection string and one sender per queue),
    `MessageDispatcher<TMessage>`, `MessageProcessor` (+ nested `QueueSubscription` for
    routing), `MessageProcessingService` (hosted service), `HandlerRegistration`
    (+ `HandlerInvoker` delegate), `MessageHeaders`, `Instrumentation` (ActivitySource, Meter,
    counters/histogram, trace-context propagation via `Diagnostic-Id`).

## Public API shape (README examples must match)

```csharp
services.AddMessageQueue(connectionString)          // or AddMessageQueue(o => ...)
    .AddDispatcher<OrderCreated>("orders")          // inject IMessageDispatcher<OrderCreated>
    .AddHandler<OrderCreatedHandler>("orders")      // TMessage inferred from IMessageHandler<T>
    .AddHandler<H, M>("q", o => ...);               // explicit overload

// handler contract
Task HandleAsync(TMessage message, CancellationToken ct);

// dispatcher surface: SendAsync (x2), SendBatchAsync, ScheduleAsync, CancelScheduledAsync
// lifecycle: auto-start by default (MessageQueueOptions.AutoStartProcessing), manual via
// provider.StartMessageProcessingAsync() / StopMessageProcessingAsync()
// observability: ActivitySource + Meter named "MessageQueue" (MessageQueueDiagnostics)
```

## Design decisions (do not silently reverse)

- **Single package, no abstractions split** (see above).
- **Dispatchers are typed by the message** (`IMessageDispatcher<TMessage>`), not by marker
  classes. Every dispatcher is additionally registered **keyed by its queue name**. One queue
  per type → plain injection resolves to the keyed instance. Several queues for the same type →
  plain injection throws a descriptive error (never silently picks a queue) and callers use
  `[FromKeyedServices("queue-name")]`. The builder tracks type→queues in
  `MessageQueueBuilder._dispatcherQueues`.
- **Internal disposables implement both `IAsyncDisposable` and `IDisposable`**
  (`ServiceBusClientProvider`, `MessageProcessor`) so synchronously-disposed containers
  (tests, console apps) don't throw.
- **Observability is built in, dependency-free.** `Internal/Instrumentation.cs` owns an
  ActivitySource and Meter (both named `"MessageQueue"`, constants on the public
  `MessageQueueDiagnostics`). Producer/Consumer spans per message, trace context propagated via
  the `Diagnostic-Id` application property, counters + duration histogram. Do not add
  OpenTelemetry package references — `System.Diagnostics` APIs only. Dispatchers fall back to
  `NullLogger` when the host has no logging registered.
- **One `ServiceBusProcessor` per queue, not per handler.** Multiple handlers on one queue are
  multiplexed by the `MessageType` application property (fallback: `Subject`); a
  single-handler queue accepts any message. Unroutable messages are dead-lettered
  (`NoHandlerRegistered`). Duplicate handler for the same (queue, message type) throws at start.
- **No per-message reflection.** Deserialize+invoke is captured as a `HandlerInvoker` delegate at
  registration. The inferred `AddHandler<THandler>` overload uses reflection once, at startup.
- **Settlement**: explicit. Complete on success; **abandon** on handler failure (broker
  redelivery → automatic dead-letter at max delivery count). `AutoCompleteMessages` is forced off.
- **Pooled clients and senders**: one `ServiceBusClient` per connection string and one
  `ServiceBusSender` per (connection, queue) via `ServiceBusClientProvider`, which owns and
  disposes all of them; dispatchers own nothing disposable.
- **Auto-start default ON** with `AutoStartProcessing = false` opt-out.
- **Fail fast**: options validated at registration time with descriptive messages.
- Depends only on *Abstractions* packages (`DependencyInjection.Abstractions`,
  `Hosting.Abstractions`, `Logging.Abstractions`) + `Azure.Messaging.ServiceBus`.

## Build, verify, pack

```bash
dotnet build --configuration Release
dotnet pack --configuration Release --output ./nupkg -p:PackageVersion=<x.y.z>
```

After packaging changes, verify with `unzip -l ./nupkg/*.nupkg`: the package must contain the
dll, its XML docs, `README.md` and `LICENSE` at the root.

There is **no test project yet**; verify with a throwaway console app in the scratchpad
referencing `MessageQueue/MessageQueue.csproj` (a well-formed fake connection string like
`Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v` lets the
container build and validate without network I/O).

## CI / release

- `Build.yaml` — build + test on push/PR to `main`.
- `Release.yaml` — on push to a `x.y` / `x.y.z` branch: `dotnet pack` (version = branch name) +
  GitHub release.
- `Publish.yaml` — pushes the produced `.nupkg` to NuGet.org after a successful release run.

Versions come from branch names, so **breaking API changes need a new `major.minor` branch**.
