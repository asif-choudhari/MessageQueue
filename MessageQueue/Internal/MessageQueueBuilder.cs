using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MessageQueue.Internal;

/// <summary>
/// Default <see cref="IMessageQueueBuilder"/>. Merges the shared root options into each
/// registration so the connection string and JSON defaults are configured exactly once.
/// </summary>
internal sealed class MessageQueueBuilder(
    IServiceCollection services,
    MessageQueueOptions root) : IMessageQueueBuilder
{
    private readonly Dictionary<Type, List<string>> _dispatcherQueues = [];

    public IServiceCollection Services => services;

    public IMessageQueueBuilder AddDispatcher<TMessage>(
        string queueName,
        Action<MessageDispatcherOptions>? configure = null)
        where TMessage : class
    {
        var options = Configure(new MessageDispatcherOptions(), queueName, configure);

        // Every dispatcher is resolvable keyed by its queue name, so the same message type can
        // target several queues via [FromKeyedServices("queue-name")].
        services.TryAddKeyedSingleton<IMessageDispatcher<TMessage>>(
            queueName,
            (sp, _) => new MessageDispatcher<TMessage>(
                sp.GetRequiredService<ServiceBusClientProvider>(),
                options,
                sp.GetService<ILoggerFactory>()?.CreateLogger("MessageQueue.Dispatcher")
                    ?? NullLogger.Instance));

        if (_dispatcherQueues.TryGetValue(typeof(TMessage), out var queues))
        {
            if (queues.Contains(queueName)) return this; // duplicate registration, no-op

            queues.Add(queueName);

            // The plain (unkeyed) injection just became ambiguous: replace it with a
            // descriptive failure instead of silently sending to the first queue.
            if (queues.Count == 2)
                services.Replace(ServiceDescriptor.Singleton<IMessageDispatcher<TMessage>>(
                    _ => throw new InvalidOperationException(
                        $"{typeof(TMessage).Name} is dispatched to multiple queues " +
                        $"({string.Join(", ", queues)}), so injecting IMessageDispatcher<{typeof(TMessage).Name}> " +
                        "directly is ambiguous. Use [FromKeyedServices(\"<queue-name>\")] on the " +
                        "constructor parameter to choose the queue.")));

            return this;
        }

        _dispatcherQueues[typeof(TMessage)] = [queueName];

        // While the message type maps to a single queue, plain injection resolves to the same
        // instance as the keyed registration.
        services.TryAddSingleton<IMessageDispatcher<TMessage>>(
            sp => sp.GetRequiredKeyedService<IMessageDispatcher<TMessage>>(queueName));

        return this;
    }

    public IMessageQueueBuilder AddHandler<THandler>(
        string queueName,
        Action<MessageHandlerOptions>? configure = null)
        where THandler : class
    {
        // Resolve TMessage from the handler's IMessageHandler<T> and bind the strongly-typed
        // overload — reflection happens once here, never per message.
        var register = typeof(MessageQueueBuilder)
            .GetMethods()
            .Single(m => m.Name == nameof(AddHandler) && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(THandler), SingleMessageType(typeof(THandler)));

        register.Invoke(this, [queueName, configure]);
        return this;
    }

    public IMessageQueueBuilder AddHandler<THandler, TMessage>(
        string queueName,
        Action<MessageHandlerOptions>? configure = null)
        where THandler : class, IMessageHandler<TMessage>
        where TMessage : class
    {
        var options = Configure(new MessageHandlerOptions(), queueName, configure);

        services.TryAddScoped<THandler>();

        services.AddSingleton(new HandlerRegistration(
            options.ConnectionString,
            options.QueueName,
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            options.ProcessorOptions,
            async (serviceProvider, body, cancellationToken) =>
            {
                var message = JsonSerializer.Deserialize<TMessage>(
                    body.ToMemory().Span,
                    options.JsonSerializerOptions);

                if (message is null)
                    throw new InvalidOperationException(
                        $"Message body deserialized to null for {typeof(TMessage).Name}.");

                var handler = serviceProvider.GetRequiredService<THandler>();
                await handler.HandleAsync(message, cancellationToken);
            }));

        return this;
    }

    private TOptions Configure<TOptions>(
        TOptions options,
        string queueName,
        Action<TOptions>? configure)
        where TOptions : QueueOptions
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        options.QueueName = queueName;
        options.ConnectionString = root.ConnectionString;
        options.JsonSerializerOptions = root.JsonSerializerOptions;

        configure?.Invoke(options);
        options.Validate();
        return options;
    }

    private static Type SingleMessageType(Type handlerType)
    {
        var messageTypes = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToArray();

        return messageTypes.Length switch
        {
            1 => messageTypes[0],
            0 => throw new InvalidOperationException(
                $"{handlerType.Name} does not implement IMessageHandler<TMessage>."),
            _ => throw new InvalidOperationException(
                $"{handlerType.Name} handles {messageTypes.Length} message types; register it once per " +
                "message type using AddHandler<THandler, TMessage>(...)."),
        };
    }
}
