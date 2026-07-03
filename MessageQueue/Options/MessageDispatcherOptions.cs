using Azure.Messaging.ServiceBus;

namespace MessageQueue.Options;

public class MessageDispatcherOptions : BaseOptions
{
    /// <summary>
    ///  Options to configure Service Bus Client
    /// </summary>
    public ServiceBusClientOptions ServiceBusProcessorOptions { get; set; } = new ServiceBusClientOptions();
}