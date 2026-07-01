using Azure.Messaging.ServiceBus;
using MessageQueue.Abstraction.Dispatcher;
using MessageQueue.Abstraction.Handler;
using MessageQueue.Dispatcher;
using MessageQueue.Handler;
using MessageQueue.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MessageQueue.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services) 
    {
        public IServiceCollection RegisterMessageHandler<THandler, TMessage>(Action<Options.Options> configure)
            where THandler : class, IMessageHandler<TMessage>
            where TMessage : class
        {
            var options = new Options.Options();
            configure(options);

            services.AddScoped<THandler>();
            services.AddScoped<IMessageHandler<TMessage>, THandler>();

            services.AddSingleton(new HandlerRegistration(
                typeof(THandler),
                typeof(TMessage),
                options));

            services.TryAddSingleton<IHandlerCollection, HandlerCollection>();

            return services;
        }

        public IServiceCollection RegisterMessageDispatcher(Action<Options.Options> configure)
        {
            services.AddAzureServiceBusCore(configure);

            return services;
        }

        private void AddAzureServiceBusCore(Action<Options.Options> configure)
        {
            services.Configure(configure);

            services.TryAddSingleton<ServiceBusClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<Options.Options>>().Value;

                return new ServiceBusClient(options.ConnectionString);
            });

            services.TryAddSingleton<IMessageDispatcher, MessageDispatcher>();
        }
    }
}
