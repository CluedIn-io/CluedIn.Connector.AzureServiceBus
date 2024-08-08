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
using CluedIn.Core.Data;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.Data.Vocabularies;
using CluedIn.Core.Processing;
using CluedIn.Core.Streams.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureServiceBus.Unit.Tests
{
    public class TestException : Exception { }

    public class AzureServiceBusConnectorTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public AzureServiceBusConnectorTests(ITestOutputHelper testOutputHelper)
        {
            this._testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData("connection1", "container1", "connection1", "container1", 1)]
        [InlineData("connection1", "container1", "connection1", "container2", 2)]   //    same   connection different container
        [InlineData("connection1", "container1", "connection2", "container1", 2)]   // different connection    same   container
        [InlineData("connection1", "container1", "connection2", "container2", 2)]   // different connection different container
        public async void BatchesPartitionedByConnectionAndContainer(string connectionString1, string containerName1, string connectionString2, string containerName2, int expectedBatchCount)
        {
            /*
             * arrange
             */
            var container = new WindsorContainer();
            container.Register(Component.For<ExecutionContext>().LifestyleTransient());
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<ITestOutputHelper>().Instance(_testOutputHelper));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());
            
            var batches = new Dictionary<Mock<IServiceBusSenderWrapper>, IServiceBusMessageBatchWrapper>();
            var senders = new List<Mock<IServiceBusSenderWrapper>>();

            var serviceBusSenderFactoryMock = new Mock<IServiceBusSenderFactory>();
            serviceBusSenderFactoryMock
                .Setup(x => x.CreateSender(
                    It.IsAny<AzureServiceBusConnectorJobData>(),
                    It.IsAny<string>()))
                .Returns<AzureServiceBusConnectorJobData, string>((config, containerName) =>
                {
                    var serviceBusMessageBatchMock = new Mock<IServiceBusMessageBatchWrapper>();
                    serviceBusMessageBatchMock.Setup(x => x.TryAddMessage(It.IsAny<ServiceBusMessage>())).Returns(true);


                    var serviceBusSenderMock = new Mock<IServiceBusSenderWrapper>();
                    serviceBusSenderMock.Setup(x => x.CreateMessageBatchAsync(It.IsAny<CancellationToken>())).Returns(new ValueTask<IServiceBusMessageBatchWrapper>(serviceBusMessageBatchMock.Object));
                    serviceBusSenderMock.Setup(x => x.SendMessagesAsync(serviceBusMessageBatchMock.Object, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

                    senders.Add(serviceBusSenderMock);
                    batches.Add(serviceBusSenderMock, serviceBusMessageBatchMock.Object);

                    return serviceBusSenderMock.Object;
                });

            container.Register(Component.For<IServiceBusSenderFactory>().Instance(serviceBusSenderFactoryMock.Object));

            var connectorMock = new Mock<AzureServiceBusConnector>(MockBehavior.Default,
                typeof(AzureServiceBusConnector).GetConstructors().First().GetParameters()
                    .Select(p => container.Resolve(p.ParameterType)).ToArray());

            var connectionMock1 = new Mock<IConnectorConnectionV2>();
            connectionMock1.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, connectionString1 }
            });

            var connectionMock2 = new Mock<IConnectorConnectionV2>();
            connectionMock2.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, connectionString2 }
            });

            var executionContext1 = container.Resolve<ExecutionContext>();
            var executionContext2 = container.Resolve<ExecutionContext>();

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(It.IsAny<ExecutionContext>(), Guid.Empty))
                .Returns<ExecutionContext, Guid>((executionContext, providerId) =>
                {
                    var connectionMock = new Mock<IConnectorConnectionV2>();
                    connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
                    {
                        { AzureServiceBusConstants.KeyName.ConnectionString, executionContext == executionContext1 ? connectionString1 : connectionString2 }
                    });

                    return Task.FromResult(connectionMock.Object);
                });

            var connector = connectorMock.Object;

            Task createSaveDataTask(ExecutionContext executionContext, string containerName)
            {
                var entityId = Guid.NewGuid().ToString();
                var lastName = $"LastName_{entityId}";

                var data = new ConnectorEntityData(VersionChangeType.Added, StreamMode.EventStream,
                    Guid.NewGuid(),
                    new ConnectorEntityPersistInfo(Guid.NewGuid().ToString(), 1), null,
                    EntityCode.FromKey($"/Person#Acceptance:{entityId}"),
                    "/Person",
                    new[]
                    {
                        new ConnectorPropertyData("user.lastName", lastName,
                            new VocabularyKeyConnectorPropertyDataType(new VocabularyKey("user.lastName"))),
                        new ConnectorPropertyData("Name", lastName,
                            new EntityPropertyConnectorPropertyDataType(typeof(string))),
                    },
                    new IEntityCode[] { EntityCode.FromKey($"/Person#Acceptance:{entityId}") },
                    null, null);

                var streamModel = new Mock<IReadOnlyStreamModel>();
                streamModel.Setup(x => x.ConnectorProviderDefinitionId).Returns(Guid.Empty);
                streamModel.Setup(x => x.ContainerName).Returns(containerName);

                return connector.StoreData(executionContext, streamModel.Object, data);
            }

            var saveDataTask1 = createSaveDataTask(executionContext1, containerName1);
            var saveDataTask2 = createSaveDataTask(executionContext2, containerName2);

            /*
             * act
             */
            await Task.WhenAll(saveDataTask1, saveDataTask2);

            /*
             * assert
             */
            Assert.Equal(expectedBatchCount, batches.Count);
            Assert.Equal(senders.Count, batches.Count);
            foreach (var sender in senders)
            {
                // verify send was only called once for each sender and that the correct batch was sent
                sender.Verify(
                    x => x.SendMessagesAsync(batches[sender], It.IsAny<CancellationToken>()),
                    Times.Once);
            }
        }

        [Fact]
        public async void ExceptionOnBatchSendIsRequeuedForEachStoreDataTask()
        {
            /*
             * arrange
             */
            var container = new WindsorContainer();
            container.Register(Component.For<ExecutionContext>().LifestyleTransient());
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());

            var serviceBusMessageBatchMock = new Mock<IServiceBusMessageBatchWrapper>();
            serviceBusMessageBatchMock.Setup(x => x.TryAddMessage(It.IsAny<ServiceBusMessage>())).Returns(true);

            var serviceBusSenderMock = new Mock<IServiceBusSenderWrapper>();
            serviceBusSenderMock.Setup(x => x.CreateMessageBatchAsync(It.IsAny<CancellationToken>())).Returns(new ValueTask<IServiceBusMessageBatchWrapper>(serviceBusMessageBatchMock.Object));
            serviceBusSenderMock.Setup(x => x.SendMessagesAsync(serviceBusMessageBatchMock.Object, It.IsAny<CancellationToken>())).Throws<TestException>();

            var serviceBusSenderFactoryMock = new Mock<IServiceBusSenderFactory>();
            serviceBusSenderFactoryMock.Setup(x => x.CreateSender(It.IsAny<AzureServiceBusConnectorJobData>(), It.IsAny<string>())).Returns(serviceBusSenderMock.Object);
            container.Register(Component.For<IServiceBusSenderFactory>().Instance(serviceBusSenderFactoryMock.Object));

            var connectorMock = new Mock<AzureServiceBusConnector>(MockBehavior.Default,
                typeof(AzureServiceBusConnector).GetConstructors().First().GetParameters()
                    .Select(p => container.Resolve(p.ParameterType)).ToArray());

            var connectionMock = new Mock<IConnectorConnectionV2>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=pol1;SharedAccessKey=XXXXX=;EntityPath=test1" }
            });

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(It.IsAny<ExecutionContext>(), Guid.Empty))
                .ReturnsAsync(connectionMock.Object);

            var connector = connectorMock.Object;

            Task<SaveResult> createSaveDataTask()
            {
                var executionContext = container.Resolve<ExecutionContext>();

                var entityId = Guid.NewGuid().ToString();
                var lastName = $"LastName_{entityId}";

                var data = new ConnectorEntityData(VersionChangeType.Added, StreamMode.EventStream,
                    Guid.NewGuid(),
                    new ConnectorEntityPersistInfo(Guid.NewGuid().ToString(), 1), null,
                    EntityCode.FromKey($"/Person#Acceptance:{entityId}"),
                    "/Person",
                    new[]
                    {
                        new ConnectorPropertyData("user.lastName", lastName,
                            new VocabularyKeyConnectorPropertyDataType(new VocabularyKey("user.lastName"))),
                        new ConnectorPropertyData("Name", lastName,
                            new EntityPropertyConnectorPropertyDataType(typeof(string))),
                    },
                    new IEntityCode[] { EntityCode.FromKey($"/Person#Acceptance:{entityId}") },
                    null, null);

                var streamModel = new Mock<IReadOnlyStreamModel>();
                streamModel.Setup(x => x.ConnectorProviderDefinitionId).Returns(Guid.Empty);
                streamModel.Setup(x => x.ContainerName).Returns("test_container");

                return connector.StoreData(executionContext, streamModel.Object, data);
            }

            var saveDataTask1 = createSaveDataTask();
            var saveDataTask2 = createSaveDataTask();

            /*
             * act
             */
            await Task.WhenAll(saveDataTask1, saveDataTask2);

            /*
             * assert
             */
            // both save tasks should have resulted in exceptions
            Assert.Null(saveDataTask1.Exception);
            Assert.Null(saveDataTask2.Exception);

            // they should both have been TestException
            Assert.Equal(saveDataTask1.Result.State, SaveResultState.ReQueue);
            Assert.Equal(saveDataTask2.Result.State, SaveResultState.ReQueue);

            // not really related to the test but SendMessagesAsync should have only been called once
            serviceBusSenderMock.Verify(x =>
                x.SendMessagesAsync(serviceBusMessageBatchMock.Object, It.IsAny<CancellationToken>()), Times.Once);

            Mock.VerifyAll();
        }
    }
}
