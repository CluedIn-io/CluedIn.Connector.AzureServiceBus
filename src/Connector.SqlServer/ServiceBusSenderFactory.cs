using Azure.Messaging.ServiceBus;

namespace CluedIn.Connector.AzureServiceBus
{
    public interface IServiceBusSenderFactory
    {
        IServiceBusSenderWrapper CreateSender(AzureServiceBusConnectorJobData config, string containerName);
    }

    internal class ServiceBusSenderFactory : IServiceBusSenderFactory
    {
        public IServiceBusSenderWrapper CreateSender(AzureServiceBusConnectorJobData config, string containerName)
        {
            var client = new ServiceBusClient(config.ConnectionString);

            var properties = ServiceBusConnectionStringProperties.Parse(config.ConnectionString);

            var sender = client.CreateSender(config.Name ?? properties.EntityPath ?? containerName);

            return new ServiceBusSenderWrapper(sender);
        }
    }
}
