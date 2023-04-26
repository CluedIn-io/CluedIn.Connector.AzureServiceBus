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
using CluedIn.Core.Processing;
using CluedIn.Core.Streams.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public class AzureServiceBusConnector : ConnectorBaseV2
    {
        private readonly ILogger<AzureServiceBusConnector> _logger;
        private readonly IApplicationCache _cache;

        public AzureServiceBusConnector(ILogger<AzureServiceBusConnector> logger, IApplicationCache cache) : base(AzureServiceBusConstants.ProviderId)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache;
        }

        public override async Task CreateContainer(ExecutionContext executionContext, Guid connectorProviderDefinitionId, IReadOnlyCreateContainerModelV2 model)
        {
            var config = await GetAuthenticationDetails(executionContext, connectorProviderDefinitionId);
            var data = new AzureServiceBusConnectorJobData(config.Authentication.ToDictionary(x => x.Key, x => x.Value));

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

        public override async Task EmptyContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
        {
            await Task.FromResult(0);
        }

        public override async Task ArchiveContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
        {
            await Task.FromResult(0);
        }

        public override async Task RenameContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel, string oldContainerName)
        {
            await Task.FromResult(0);
        }

        public override async Task RemoveContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
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

        public override async Task<ConnectionVerificationResult> VerifyConnection(ExecutionContext executionContext, IReadOnlyDictionary<string, object> config)
        {
            var data = new AzureServiceBusConnectorJobData(config.ToDictionary(x => x.Key, x => x.Value));

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

        public override async Task<SaveResult> StoreData(ExecutionContext executionContext, IReadOnlyStreamModel streamModel, IReadOnlyConnectorEntityData connectorEntityData)
        {
            var providerDefinitionId = streamModel.ConnectorProviderDefinitionId!.Value;
            var containerName = streamModel.ContainerName;

            // matching output format of previous version of the connector
            var data = connectorEntityData.Properties.ToDictionary(x => x.Name, x => x.Value);
            data.Add("Id", connectorEntityData.EntityId);
            
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
            data.Add("Codes", connectorEntityData.EntityCodes.Select(c => c.ToString()));
            // end match previous version of the connector

            if (connectorEntityData.OutgoingEdges.SafeEnumerate().Any())
            {
                data.Add("OutgoingEdges", connectorEntityData.OutgoingEdges);
            }

            if (connectorEntityData.IncomingEdges.SafeEnumerate().Any())
            {
                data.Add("IncomingEdges", connectorEntityData.IncomingEdges);
            }

            data.Add("ChangeType", connectorEntityData.ChangeType.ToString());

            var details = await GetAuthenticationDetails(executionContext, providerDefinitionId);
            var config = new AzureServiceBusConnectorJobData(details.Authentication.ToDictionary(x => x.Key, x => x.Value));

            await using var client = new ServiceBusClient(config.ConnectionString);

            var properties = ServiceBusConnectionStringProperties.Parse(config.ConnectionString);

            var sender = client.CreateSender(config.Name ?? properties.EntityPath ?? containerName);
            var message =
                new ServiceBusMessage(JsonUtility.Serialize(data,
                    new JsonSerializer() { Formatting = Formatting.Indented }));

            await sender.SendMessageAsync(message);

            return SaveResult.Success;
        }

        public virtual async Task<IConnectorConnectionV2> GetAuthenticationDetails(ExecutionContext executionContext, Guid providerDefinitionId)
        {
            return await AuthenticationDetailsHelper.GetAuthenticationDetails(executionContext, providerDefinitionId);
        }

        public override Task<ConnectorLatestEntityPersistInfo> GetLatestEntityPersistInfo(ExecutionContext executionContext, IReadOnlyStreamModel streamModel, Guid entityId)
        {
            throw new NotSupportedException();
        }

        public override Task<IAsyncEnumerable<ConnectorLatestEntityPersistInfo>> GetLatestEntityPersistInfos(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
        {
            throw new NotSupportedException();
        }

        public override IReadOnlyCollection<StreamMode> GetSupportedModes()
        {
            return new[] { StreamMode.Sync };
        }

        public override Task VerifyExistingContainer(ExecutionContext executionContext, IReadOnlyStreamModel streamModel)
        {
            return Task.FromResult(0);
        }
    }
}
