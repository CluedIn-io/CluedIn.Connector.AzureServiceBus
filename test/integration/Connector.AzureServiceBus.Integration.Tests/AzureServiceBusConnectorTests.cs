using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers;
using Castle.Windsor;
using CluedIn.Connector.AzureServiceBus.Connector;
using CluedIn.Core.Caching;
using CluedIn.Core.Connectors;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureServiceBus.Integration.Tests
{
    public class AzureServiceBusConnectorTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public AzureServiceBusConnectorTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        private string RootConnectionString => Environment.GetEnvironmentVariable("SERVICEBUS_ROOTMANAGESHAREDACCESSKEY_CONNECTIONSTRING");

        private string TestQueueConnectionString => Environment.GetEnvironmentVariable("SERVICEBUS_TESTQUEUE_CONNECTIONSTRING");

        private string TestQueueName => ServiceBusConnectionStringProperties.Parse(TestQueueConnectionString).EntityPath;

        [Fact]
        public async Task CanCreateContainer()
        {
            // arrange
            var container = new WindsorContainer();
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());

            var executionContext = container.Resolve<ExecutionContext>();

            var connectorMock = new Mock<AzureServiceBusConnector>(MockBehavior.Default,
                typeof(AzureServiceBusConnector).GetConstructors().First().GetParameters()
                    .Select(p => container.Resolve(p.ParameterType)).ToArray());

            var model = new CreateContainerModel();
            model.Name = "TEST_" + Guid.NewGuid();

            var connectionMock = new Mock<IConnectorConnection>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, RootConnectionString }
            });

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(executionContext, Guid.Empty))
                .ReturnsAsync(connectionMock.Object);

            var connector = connectorMock.Object;

            try
            {
                // act
                await connector.CreateContainer(executionContext, Guid.Empty, model);

                // assert
                var client =
                    new Azure.Messaging.ServiceBus.Administration.ServiceBusAdministrationClient(RootConnectionString);
                var q = await client.GetQueueAsync(model.Name);
                Assert.NotNull(q);
                Assert.Equal(model.Name, q.Value.Name);
            }
            finally
            {
                // cleanup
                try
                {
                    var client = new Azure.Messaging.ServiceBus.Administration.ServiceBusAdministrationClient(RootConnectionString);
                    await client.DeleteQueueAsync(model.Name);
                }
                catch (Exception ex)
                {
                    _testOutputHelper.WriteLine("Error in cleanup: " + ex);
                }
            }
        }

        [Fact]
        public async Task VerifyConnectionReturnsTrueForValidRootKey()
        {
            // arrange
            var container = new WindsorContainer();
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());

            var executionContext = container.Resolve<ExecutionContext>();

            var connectorMock = new Mock<AzureServiceBusConnector>(MockBehavior.Default,
                typeof(AzureServiceBusConnector).GetConstructors().First().GetParameters()
                    .Select(p => container.Resolve(p.ParameterType)).ToArray());

            var connectionMock = new Mock<IConnectorConnection>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, RootConnectionString }
            });

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(executionContext, Guid.Empty))
                .ReturnsAsync(connectionMock.Object);

            var connector = connectorMock.Object;

            // act
            var valid = await connector.VerifyConnection(executionContext, Guid.Empty);

            // assert
            Assert.True(valid);
        }

        [Theory]
        [InlineData("XXX")]
        [InlineData("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=XXXX")]
        public async Task VerifyConnectionReturnsFalseForInValidConnectionString(string connectionString)
        {
            // arrange
            var container = new WindsorContainer();
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());

            var executionContext = container.Resolve<ExecutionContext>();

            var connectorMock = new Mock<AzureServiceBusConnector>(MockBehavior.Default,
                typeof(AzureServiceBusConnector).GetConstructors().First().GetParameters()
                    .Select(p => container.Resolve(p.ParameterType)).ToArray());

            var connectionMock = new Mock<IConnectorConnection>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, connectionString }
            });

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(executionContext, Guid.Empty))
                .ReturnsAsync(connectionMock.Object);

            var connector = connectorMock.Object;

            // act
            var valid = await connector.VerifyConnection(executionContext, Guid.Empty);

            // assert
            Assert.False(valid);
        }

        [Fact]
        public async Task VerifyConnectionReturnsTrueForValidQueueAccessKey()
        {
            // arrange
            var container = new WindsorContainer();
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());

            var executionContext = container.Resolve<ExecutionContext>();

            var connectorMock = new Mock<AzureServiceBusConnector>(MockBehavior.Default,
                typeof(AzureServiceBusConnector).GetConstructors().First().GetParameters()
                    .Select(p => container.Resolve(p.ParameterType)).ToArray());

            var connectionMock = new Mock<IConnectorConnection>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, TestQueueConnectionString }
            });

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(executionContext, Guid.Empty))
                .ReturnsAsync(connectionMock.Object);

            var connector = connectorMock.Object;

            // act
            var valid = await connector.VerifyConnection(executionContext, Guid.Empty);

            // assert
            Assert.True(valid);
        }

        [Fact]
        public async Task VerifyCanStoreData()
        {
            // arrange
            var container = new WindsorContainer();
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());

            var executionContext = container.Resolve<ExecutionContext>();

            var connectorMock = new Mock<AzureServiceBusConnector>(MockBehavior.Default,
                typeof(AzureServiceBusConnector).GetConstructors().First().GetParameters()
                    .Select(p => container.Resolve(p.ParameterType)).ToArray());

            var connectionMock = new Mock<IConnectorConnection>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, TestQueueConnectionString }
            });

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(executionContext, Guid.Empty))
                .ReturnsAsync(connectionMock.Object);

            var connector = connectorMock.Object;

            var id = Guid.NewGuid().ToString();

            // act
            await connector.StoreData(executionContext, Guid.Empty, null,
                new Dictionary<string, object> { { "id", id } });

            // assert
            var client = new ServiceBusClient(RootConnectionString);
            var processor = client.CreateProcessor(TestQueueName);

            var evt = new AutoResetEvent(false);

            string receivedId = null;

            processor.ProcessMessageAsync += async arg =>
            {
                dynamic msg = JsonConvert.DeserializeObject(arg.Message.Body.ToString());
                receivedId = msg.id;
                if (receivedId == id)
                {
                    evt.Set();
                }

                await arg.CompleteMessageAsync(arg.Message);
            };

            processor.ProcessErrorAsync += arg => Task.CompletedTask;

            await processor.StartProcessingAsync();

            Assert.True(evt.WaitOne(10000), "Failed to received expected message");

            await processor.StopProcessingAsync();

            Assert.Equal(id, receivedId);
        }
    }
}