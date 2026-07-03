using Azure.Messaging.ServiceBus;
using MessageQueue.Abstraction.Dispatcher;
using MessageQueue.Abstraction.Handler;
using MessageQueue.Abstraction.Processor;
using MessageQueue.Dispatcher;
using MessageQueue.Handler;
using MessageQueue.Options;
using MessageQueue.Processor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MessageQueue.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services) 
    {
        public IServiceCollection RegisterMessageHandler<THandler, TMessage>(Action<MessageHandlerOptions> configure)
            where THandler : class, IMessageHandler<TMessage>
            where TMessage : class
        {
            var options = new MessageHandlerOptions();
            configure(options);

            services.AddScoped<IMessageHandler<TMessage>, THandler>();

            services.AddSingleton(new HandlerRecord(
                typeof(THandler),
                typeof(TMessage),
                options));

            services.TryAddSingleton<IMessageProcessor, MessageProcessor>();

            return services;
        }

        public IServiceCollection RegisterMessageDispatcher(Action<MessageDispatcherOptions> configure)
        { 
            services.Configure(configure);
            
            services.TryAddSingleton<ServiceBusClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<MessageDispatcherOptions>>().Value;

                return new ServiceBusClient(options.ConnectionString);
            });

            services.TryAddSingleton<IMessageDispatcher, MessageDispatcher>();

            return services;
        }
    }
}
