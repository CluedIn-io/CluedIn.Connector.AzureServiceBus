using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using CluedIn.Core;
using CluedIn.Core.Caching;
using CluedIn.Core.Connectors;
using CluedIn.Core.DataStore;
using CluedIn.Core.Processing;
using CluedIn.Core.Streams.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public class AzureServiceBusConnector : ConnectorBaseV2
    {
        private readonly IConfigurationRepository _configurationRepository;
        private readonly ILogger<AzureServiceBusConnector> _logger;
        private readonly IApplicationCache _cache;

        public AzureServiceBusConnector(IConfigurationRepository repo, ILogger<AzureServiceBusConnector> logger, IApplicationCache cache) : base(AzureServiceBusConstants.ProviderId)
        {
            _configurationRepository = repo;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache;
        }

        public override async Task CreateContainer(ExecutionContext executionContext, Guid providerDefinitionId, CreateContainerModelV2 model)
        {
            var config = await GetAuthenticationDetails(executionContext, providerDefinitionId);
            var data = new AzureServiceBusConnectorJobData(config.Authentication);

            var properties = ServiceBusConnectionStringProperties.Parse(data.ConnectionString);

            if ((data.Name ?? properties.EntityPath) != null)
            {
                return; // if a queue name has been specified via the connection string or the export target config then do not try to create the queue
            }

            var key = $"CreateQueue-{properties.FullyQualifiedNamespace}-{properties.SharedAccessKeyName}-{model.Name}";

            await _cache.GetItem(key, async () =>
                {
                    try
                    {
                        var client = new ServiceBusAdministrationClient(data.ConnectionString);

                        var exists = await client.QueueExistsAsync(model.Name);

                        if (!exists)
                        {
                            await client.CreateQueueAsync(model.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Exception creating queue");
                    }

                    return await Task.FromResult(true);
                },
                true,
                policy => policy.WithAbsoluteExpiration(DateTimeOffset.Now.AddDays(1)) // will never be able to create a queue without manage permissions so lets not try for a long time
            );
        }

        public override async Task EmptyContainer(ExecutionContext executionContext, Guid providerDefinitionId, string id)
        {
            await Task.FromResult(0);
        }

        public override async Task ArchiveContainer(ExecutionContext executionContext, Guid providerDefinitionId, string id)
        {
            await Task.FromResult(0);
        }

        public override async Task RenameContainer(ExecutionContext executionContext, Guid providerDefinitionId, string id, string newName)
        {
            await Task.FromResult(0);
        }

        public override async Task RemoveContainer(ExecutionContext executionContext, Guid providerDefinitionId, string id)
        {
            await Task.FromResult(0);
        }

        public override Task<string> GetValidMappingDestinationPropertyName(ExecutionContext executionContext, Guid providerDefinitionId, string name)
        {
            // Strip non-alpha numeric characters
            var result = Regex.Replace(name, @"[^A-Za-z0-9]+", "");

            return Task.FromResult(result);
        }

        public override async Task<string> GetValidContainerName(ExecutionContext executionContext, Guid providerDefinitionId, string name)
        {
            // Strip non-alpha numeric characters
            Uri uri;
            if (Uri.TryCreate(name, UriKind.Absolute, out uri))
            {
                return await Task.FromResult(uri.AbsolutePath);
            }
            else
            {
                return await Task.FromResult(name);
            }
        }

        public override async Task<IEnumerable<IConnectorContainer>> GetContainers(ExecutionContext executionContext, Guid providerDefinitionId)
        {
            return await Task.FromResult(new List<IConnectorContainer>());
        }

        public override async Task<ConnectionVerificationResult> VerifyConnection(ExecutionContext executionContext, IDictionary<string, object> config)
        {
            var data = new AzureServiceBusConnectorJobData(config);

            ServiceBusConnectionStringProperties properties;

            try
            {
                properties = ServiceBusConnectionStringProperties.Parse(data.ConnectionString);
            }
            catch
            {
                return new ConnectionVerificationResult(false, "Invalid connection string");
            }

            var queueName = data.Name ?? properties.EntityPath;

            if (queueName == null)  // no queueName, lets use the admin client to verify the connection string
            {
                var client = new ServiceBusAdministrationClient(data.ConnectionString);

                try
                {
                    var queuesListingResult = client.GetQueuesAsync();
                    await queuesListingResult.GetAsyncEnumerator().MoveNextAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Exception response when enumerating queues.");

                    if (!ex.Message.Contains("claims required")) // token's in the connection string must be valid if we get a claims required message
                    {
                        return new ConnectionVerificationResult(false);
                    }
                }
            }
            else
            {
                try
                {
                    await using var client = new ServiceBusClient(data.ConnectionString);
                    var sender = client.CreateSender(data.Name ?? properties.EntityPath);

                    await sender.CreateMessageBatchAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, $"{nameof(VerifyConnection)} failed for {nameof(AzureServiceBusConnector)}");
                    return new ConnectionVerificationResult(false);
                }
            }

            return new ConnectionVerificationResult(true);
        }

        public override async Task<SaveResult> StoreData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, ConnectorEntityData connectorEntityData)
        {
            var d = new Dictionary<string, object> { { "a", "B" } };
            // matching output format of previous version of the connector
            var data = connectorEntityData.Properties.ToDictionary(x => GetValidMappingDestinationPropertyName(executionContext, providerDefinitionId, x.Name).Result, x => x.Value);
            data.Add("Id", connectorEntityData.EntityId);
            data.Add("Codes",
                new Dictionary<string, object>
                {
                    {
                        "$type",
                        "System.Collections.Generic.List`1[[System.Object, System.Private.CoreLib]], System.Private.CoreLib"
                    },
                    { "$values", connectorEntityData.EntityCodes.Select(c => c.ToString()) }
                });
            if (connectorEntityData.PersistInfo != null)
            {
                data.Add("PersistHash", connectorEntityData.PersistInfo.PersistHash);
            }

            if (connectorEntityData.OriginEntityCode != null)
            {
                data.Add("OriginEntityCode", connectorEntityData.OriginEntityCode.ToString());
            }

            if (connectorEntityData.EntityType != null)
            {
                data.Add("EntityType", connectorEntityData.EntityType.ToString());
            }
            // end match previous version of the connector

            var jsonSerializer = new JsonSerializer { TypeNameHandling = TypeNameHandling.None };

            data.Add("OutgoingEdges", connectorEntityData.OutgoingEdges);
            data.Add("IncomingEdges", connectorEntityData.IncomingEdges);

            var details = await GetAuthenticationDetails(executionContext, providerDefinitionId);
            var config = new AzureServiceBusConnectorJobData(details.Authentication);

            await using var client = new ServiceBusClient(config.ConnectionString);

            var properties = ServiceBusConnectionStringProperties.Parse(config.ConnectionString);

            var sender = client.CreateSender(config.Name ?? properties.EntityPath ?? containerName);
            var message = new ServiceBusMessage(JsonUtility.Serialize(data, jsonSerializer));

            await ActionExtensions.ExecuteWithRetryAsync(() => sender.SendMessageAsync(message));

            return SaveResult.Success;
        }

        public virtual Task<IConnectorConnection> GetAuthenticationDetails(ExecutionContext executionContext, Guid providerDefinitionId)
        {
            var key = $"AuthenticationDetails_{providerDefinitionId}";
            ICachePolicy GetPolicy(ICachePolicy cachePolicy) => new CachePolicy { SlidingExpiration = new TimeSpan(0, 0, 1, 0) };

            var result = executionContext.ApplicationContext.System.Cache.GetItem(key, () =>
            {
                var dictionary = _configurationRepository.GetConfigurationById(executionContext, providerDefinitionId);

                return new ConnectorConnectionBase { Authentication = dictionary };
            }, cachePolicy: GetPolicy);

            return Task.FromResult(result as IConnectorConnection);
        }

        public override Task<ConnectorLatestEntityPersistInfo> GetLatestEntityPersistInfo(ExecutionContext executionContext, Guid providerDefinitionId, string containerName,
            Guid entityId)
        {
            throw new NotSupportedException();
        }

        public override Task<IAsyncEnumerable<ConnectorLatestEntityPersistInfo>> GetLatestEntityPersistInfos(ExecutionContext executionContext, Guid providerDefinitionId, string containerName)
        {
            throw new NotSupportedException();
        }

        public override IReadOnlyCollection<StreamMode> GetSupportedModes()
        {
            return new[] { StreamMode.Sync };
        }

        public override Task VerifyExistingContainer(ExecutionContext executionContext, StreamModel stream)
        {
            return Task.FromResult(0);
        }
    }
}
