using Azure.Messaging.ServiceBus;

namespace MessageQueue.Options;

public class MessageHandlerOptions : BaseOptions
{
    /// <summary>
    ///  Options to configure Service Bus Message Processor
    /// </summary>
    public ServiceBusProcessorOptions ServiceBusProcessorOptions { get; set; } = new ServiceBusProcessorOptions();
}