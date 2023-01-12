using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace CluedIn.Connector.AzureServiceBus
{
    internal sealed class ServiceBusResourcePair : IAsyncDisposable
    {
        internal ServiceBusResourcePair(string connectionString, string queueName, string containerName)
        {
            ServiceBusClient = new ServiceBusClient(connectionString);
            var properties = ServiceBusConnectionStringProperties.Parse(connectionString);
            ServiceBusSender = ServiceBusClient.CreateSender(queueName ?? properties.EntityPath ?? containerName);
        }

        public ServiceBusClient ServiceBusClient { get; private set; }
        public ServiceBusSender ServiceBusSender { get; private set; }

        public async ValueTask DisposeAsync()
        {
            await ServiceBusSender.DisposeAsync();
            await ServiceBusClient.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }
}
