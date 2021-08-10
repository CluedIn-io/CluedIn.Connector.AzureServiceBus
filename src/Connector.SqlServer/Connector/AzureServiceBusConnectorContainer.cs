using CluedIn.Core.Connectors;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public class AzureServiceBusConnectorContainer : IConnectorContainer
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string FullyQualifiedName { get; set; }
    }
}
