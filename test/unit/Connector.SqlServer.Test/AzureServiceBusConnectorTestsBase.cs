using CluedIn.Connector.AzureServiceBus.Connector;
using CluedIn.Core.DataStore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CluedIn.Connector.AzureServiceBus.Unit.Tests
{
    public class AzureServiceBusConnectorTestsBase
    {
        protected readonly AzureServiceBusConnector Sut;
        protected readonly Mock<IConfigurationRepository> Repo = new Mock<IConfigurationRepository>();
        protected readonly Mock<ILogger<AzureServiceBusConnector>> Logger = new Mock<ILogger<AzureServiceBusConnector>>();
        protected readonly Mock<IAzureServiceBusClient> Client = new Mock<IAzureServiceBusClient>();
        protected readonly TestContext Context = new TestContext();

        public AzureServiceBusConnectorTestsBase()
        {
            Sut = new AzureServiceBusConnector(Repo.Object, Logger.Object, Client.Object);
        }
    }
}
