using MessageQueue;
using MessageQueue.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

// Deliberately in the Microsoft.Extensions.DependencyInjection namespace (the convention for DI
// registration extensions) so AddMessageQueue is discoverable without any extra using directive.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Entry points for wiring MessageQueue into an application.
/// </summary>
public static class MessageQueueServiceCollectionExtensions
{
    /// <summary>
    /// Adds MessageQueue with a shared Service Bus connection string and returns a builder for
    /// registering handlers and dispatchers. Consumption starts automatically with the host;
    /// use the options overload to opt out.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The Azure Service Bus connection string shared by all registrations.</param>
    /// <returns>A builder used to register handlers and dispatchers.</returns>
    public static IMessageQueueBuilder AddMessageQueue(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddMessageQueue(options => options.ConnectionString = connectionString);
    }

    /// <summary>
    /// Adds MessageQueue using the supplied root options and returns a builder for registering
    /// handlers and dispatchers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Configures the shared connection string, JSON defaults, Service Bus client options and
    /// whether consumption auto-starts (<see cref="MessageQueueOptions.AutoStartProcessing"/>).
    /// </param>
    /// <returns>A builder used to register handlers and dispatchers.</returns>
    public static IMessageQueueBuilder AddMessageQueue(
        this IServiceCollection services,
        Action<MessageQueueOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MessageQueueOptions();
        configure(options);

        services.TryAddSingleton(_ => new ServiceBusClientProvider(options.ClientOptions));
        services.TryAddSingleton<IMessageProcessor, MessageProcessor>();

        if (options.AutoStartProcessing)
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, MessageProcessingService>());

        return new MessageQueueBuilder(services, options);
    }
}
