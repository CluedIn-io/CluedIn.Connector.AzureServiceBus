using CluedIn.Connector.AzureServiceBus.Connector;
using CluedIn.Core.Caching;
using Microsoft.Extensions.Logging;
using Moq;

namespace CluedIn.Connector.AzureServiceBus.Unit.Tests
{
    public class AzureServiceBusConnectorTestsBase
    {
        protected readonly AzureServiceBusConnector Sut;
        protected readonly Mock<ILogger<AzureServiceBusConnector>> Logger = new Mock<ILogger<AzureServiceBusConnector>>();
        protected readonly Mock<IApplicationCache> Cache = new Mock<IApplicationCache>();
        protected readonly TestContext Context = new TestContext();

        public AzureServiceBusConnectorTestsBase()
        {
            Sut = new AzureServiceBusConnector(Logger.Object, Cache.Object);
        }
    }
}
