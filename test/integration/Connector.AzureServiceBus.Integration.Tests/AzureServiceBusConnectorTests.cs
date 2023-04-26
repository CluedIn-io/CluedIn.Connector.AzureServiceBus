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
using CluedIn.Core.Streams.Models;
using FluentAssertions;
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

            var model = new CreateContainerModelV2("TEST_" + Guid.NewGuid(), null, ExistingContainerActionEnum.Overwrite, false, false, StreamMode.Sync);

            var connectionMock = new Mock<IConnectorConnectionV2>();
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

            container.Register(Component.For<AzureServiceBusConnector>());

            var executionContext = container.Resolve<ExecutionContext>();
            
            var connector = container.Resolve<AzureServiceBusConnector>();

            // act
            var valid = await connector.VerifyConnection(executionContext, new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, RootConnectionString }
            });

            // assert
            Assert.True(valid.Success);
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

            container.Register(Component.For<AzureServiceBusConnector>());

            var executionContext = container.Resolve<ExecutionContext>();
 
            var connector = container.Resolve<AzureServiceBusConnector>();

            // act
            var valid = await connector.VerifyConnection(executionContext, new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, connectionString }
            });

            // assert
            Assert.False(valid.Success);
        }

        [Fact]
        public async Task VerifyConnectionReturnsTrueForValidQueueAccessKey()
        {
            // arrange
            var container = new WindsorContainer();
            container.Register(Component.For<IWindsorContainer>().Instance(container));
            container.Register(Component.For<IApplicationCache>().ImplementedBy<InMemoryApplicationCache>());
            container.Register(Component.For<ILazyComponentLoader>().ImplementedBy<AutoMockingLazyComponentLoader>());

            container.Register(Component.For<AzureServiceBusConnector>());

            var executionContext = container.Resolve<ExecutionContext>();
            
            var connector = container.Resolve<AzureServiceBusConnector>();

            // act
            var valid = await connector.VerifyConnection(executionContext, new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, TestQueueConnectionString }
            });

            // assert
            Assert.True(valid.Success);
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

            var connectionMock = new Mock<IConnectorConnectionV2>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, TestQueueConnectionString }
            });

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(executionContext, Guid.Empty))
                .ReturnsAsync(connectionMock.Object);

            var connector = connectorMock.Object;

            var id = Guid.Parse("69e26b81-bcbf-54f7-af97-be056f73bf9a").ToString();


            var data = new ConnectorEntityData(VersionChangeType.Added, StreamMode.EventStream,
                Guid.Parse("69e26b81-bcbf-54f7-af97-be056f73bf9a"),
                new ConnectorEntityPersistInfo("1lzghdhhgqlnucj078/77q==", 1), null,
                EntityCode.FromKey("/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0"),
                "/Person",
                new[]
                {
                    new ConnectorPropertyData("user.lastName", "Picard",
                        new VocabularyKeyConnectorPropertyDataType(new VocabularyKey("user.lastName"))),
                    new ConnectorPropertyData("Name", "Jean Luc Picard",
                        new EntityPropertyConnectorPropertyDataType(typeof(string))),
                },
                new IEntityCode[] { EntityCode.FromKey("/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0") },
                null, null);

            var streamModel = new Mock<IReadOnlyStreamModel>();
            streamModel.Setup(x => x.ConnectorProviderDefinitionId).Returns(Guid.Empty);
            streamModel.Setup(x => x.ContainerName).Returns("test_container");

            // act
            await connector.StoreData(executionContext, streamModel.Object, data);

            // assert
            var client = new ServiceBusClient(RootConnectionString);
            var processor = client.CreateProcessor(TestQueueName);

            var evt = new AutoResetEvent(false);

            string receivedId = null;
            string receivedBody = null;

            processor.ProcessMessageAsync += async arg =>
            {
                receivedBody = arg.Message.Body.ToString();
                dynamic msg = JsonConvert.DeserializeObject(receivedBody);
                receivedId = msg.Id;
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

            receivedBody.Should().Be(@"{
  ""user.lastName"": ""Picard"",
  ""Name"": ""Jean Luc Picard"",
  ""Id"": ""69e26b81-bcbf-54f7-af97-be056f73bf9a"",
  ""PersistHash"": ""1lzghdhhgqlnucj078/77q=="",
  ""OriginEntityCode"": ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0"",
  ""EntityType"": ""/Person"",
  ""Codes"": [
    ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0""
  ],
  ""ChangeType"": ""Added""
}");
        }

        [Fact]
        public async Task VerifyCanStoreDataWithEdges()
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

            var connectionMock = new Mock<IConnectorConnectionV2>();
            connectionMock.Setup(x => x.Authentication).Returns(new Dictionary<string, object>
            {
                { AzureServiceBusConstants.KeyName.ConnectionString, TestQueueConnectionString }
            });

            connectorMock.CallBase = true;
            connectorMock.Setup(x => x.GetAuthenticationDetails(executionContext, Guid.Empty))
                .ReturnsAsync(connectionMock.Object);

            var connector = connectorMock.Object;

            var id = Guid.Parse("69e26b81-bcbf-54f7-af97-be056f73bf9a").ToString();


            var data = new ConnectorEntityData(VersionChangeType.Added, StreamMode.EventStream,
                Guid.Parse("69e26b81-bcbf-54f7-af97-be056f73bf9a"),
                new ConnectorEntityPersistInfo("1lzghdhhgqlnucj078/77q==", 1), null,
                EntityCode.FromKey("/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0"),
                "/Person",
                new[]
                {
                    new ConnectorPropertyData("user.lastName", "Picard",
                        new VocabularyKeyConnectorPropertyDataType(new VocabularyKey("user.lastName"))),
                    new ConnectorPropertyData("Name", "Jean Luc Picard",
                        new EntityPropertyConnectorPropertyDataType(typeof(string))),
                },
                new IEntityCode[] { EntityCode.FromKey("/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0") },
                new[]
                {
                    new EntityEdge(
                        new EntityReference(
                            EntityCode.FromKey("/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0")),
                        new EntityReference(EntityCode.FromKey("/EntityA#Somewhere:1234")), "/EntityA")
                },
                new[]
                {
                    new EntityEdge(new EntityReference(EntityCode.FromKey("/EntityB#Somewhere:5678")),
                        new EntityReference(
                            EntityCode.FromKey("/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0")),
                        "/EntityB")
                });

            var streamModel = new Mock<IReadOnlyStreamModel>();
            streamModel.Setup(x => x.ConnectorProviderDefinitionId).Returns(Guid.Empty);
            streamModel.Setup(x => x.ContainerName).Returns("test_container");

            // act
            await connector.StoreData(executionContext, streamModel.Object, data);

            // assert
            var client = new ServiceBusClient(RootConnectionString);
            var processor = client.CreateProcessor(TestQueueName);

            var evt = new AutoResetEvent(false);

            string receivedId = null;
            string receivedBody = null;

            processor.ProcessMessageAsync += async arg =>
            {
                receivedBody = arg.Message.Body.ToString();
                dynamic msg = JsonConvert.DeserializeObject(receivedBody);
                receivedId = msg.Id;
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

            receivedBody.Should().Be(@"{
  ""user.lastName"": ""Picard"",
  ""Name"": ""Jean Luc Picard"",
  ""Id"": ""69e26b81-bcbf-54f7-af97-be056f73bf9a"",
  ""PersistHash"": ""1lzghdhhgqlnucj078/77q=="",
  ""OriginEntityCode"": ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0"",
  ""EntityType"": ""/Person"",
  ""Codes"": [
    ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0""
  ],
  ""OutgoingEdges"": [
    {
      ""FromReference"": {
        ""Code"": {
          ""Origin"": {
            ""Code"": ""Somewhere"",
            ""Id"": null
          },
          ""Value"": ""5678"",
          ""Key"": ""/EntityB#Somewhere:5678"",
          ""Type"": {
            ""IsEntityContainer"": false,
            ""Root"": null,
            ""Code"": ""/EntityB""
          }
        },
        ""Type"": {
          ""IsEntityContainer"": false,
          ""Root"": null,
          ""Code"": ""/EntityB""
        },
        ""Name"": null,
        ""Properties"": null,
        ""PropertyCount"": null,
        ""EntityId"": null,
        ""IsEmpty"": false
      },
      ""ToReference"": {
        ""Code"": {
          ""Origin"": {
            ""Code"": ""Acceptance"",
            ""Id"": null
          },
          ""Value"": ""7c5591cf-861a-4642-861d-3b02485854a0"",
          ""Key"": ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0"",
          ""Type"": {
            ""IsEntityContainer"": false,
            ""Root"": null,
            ""Code"": ""/Person""
          }
        },
        ""Type"": {
          ""IsEntityContainer"": false,
          ""Root"": null,
          ""Code"": ""/Person""
        },
        ""Name"": null,
        ""Properties"": null,
        ""PropertyCount"": null,
        ""EntityId"": null,
        ""IsEmpty"": false
      },
      ""EdgeType"": {
        ""Root"": null,
        ""Code"": ""/EntityB""
      },
      ""HasProperties"": false,
      ""Properties"": {},
      ""CreationOptions"": 0,
      ""Weight"": null,
      ""Version"": 0
    }
  ],
  ""IncomingEdges"": [
    {
      ""FromReference"": {
        ""Code"": {
          ""Origin"": {
            ""Code"": ""Acceptance"",
            ""Id"": null
          },
          ""Value"": ""7c5591cf-861a-4642-861d-3b02485854a0"",
          ""Key"": ""/Person#Acceptance:7c5591cf-861a-4642-861d-3b02485854a0"",
          ""Type"": {
            ""IsEntityContainer"": false,
            ""Root"": null,
            ""Code"": ""/Person""
          }
        },
        ""Type"": {
          ""IsEntityContainer"": false,
          ""Root"": null,
          ""Code"": ""/Person""
        },
        ""Name"": null,
        ""Properties"": null,
        ""PropertyCount"": null,
        ""EntityId"": null,
        ""IsEmpty"": false
      },
      ""ToReference"": {
        ""Code"": {
          ""Origin"": {
            ""Code"": ""Somewhere"",
            ""Id"": null
          },
          ""Value"": ""1234"",
          ""Key"": ""/EntityA#Somewhere:1234"",
          ""Type"": {
            ""IsEntityContainer"": false,
            ""Root"": null,
            ""Code"": ""/EntityA""
          }
        },
        ""Type"": {
          ""IsEntityContainer"": false,
          ""Root"": null,
          ""Code"": ""/EntityA""
        },
        ""Name"": null,
        ""Properties"": null,
        ""PropertyCount"": null,
        ""EntityId"": null,
        ""IsEmpty"": false
      },
      ""EdgeType"": {
        ""Root"": null,
        ""Code"": ""/EntityA""
      },
      ""HasProperties"": false,
      ""Properties"": {},
      ""CreationOptions"": 0,
      ""Weight"": null,
      ""Version"": 0
    }
  ],
  ""ChangeType"": ""Added""
}");
        }
    }
}
