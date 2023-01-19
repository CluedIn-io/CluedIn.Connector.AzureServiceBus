using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using CluedIn.Core;
using Microsoft.Extensions.Caching.Memory;
using CluedIn.Core.Connectors;
using CluedIn.Core.DataStore;
using Microsoft.Extensions.Logging;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public class AzureServiceBusConnector : ConnectorBase, IDisposable
    {
        private readonly ILogger<AzureServiceBusConnector> _logger;
        private readonly IMemoryCache _memoryCache;

        public AzureServiceBusConnector(IConfigurationRepository repo, ILogger<AzureServiceBusConnector> logger, IMemoryCacheFactory memoryCacheFactory) : base(repo)
        {
            ProviderId = AzureServiceBusConstants.ProviderId;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCacheFactory.Create(new MemoryCacheOptions());
        }

        public override async Task CreateContainer(ExecutionContext executionContext, Guid providerDefinitionId, CreateContainerModel model)
        {
            var config = await GetAuthenticationDetails(executionContext, providerDefinitionId);
            var data = new AzureServiceBusConnectorJobData(config.Authentication);

            var properties = ServiceBusConnectionStringProperties.Parse(data.ConnectionString);

            if ((data.Name ?? properties.EntityPath) != null)
            {
                return; // if a queue name has been specified via the connection string or the export target config then do not try to create the queue
            }

            var key = $"CreateQueue-{properties.FullyQualifiedNamespace}-{properties.SharedAccessKeyName}-{model.Name}";

            await _memoryCache.GetOrCreate(key, async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);   // will never be able to create a queue without manage permissions so lets not try for a long time
                cacheEntry.Value = true;

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
            });
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

        public override Task<string> GetValidDataTypeName(ExecutionContext executionContext, Guid providerDefinitionId, string name)
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

        public override async Task<IEnumerable<IConnectionDataType>> GetDataTypes(ExecutionContext executionContext, Guid providerDefinitionId, string containerId)
        {
            return await Task.FromResult(new List<IConnectionDataType>());
        }

        public override async Task<bool> VerifyConnection(ExecutionContext executionContext, Guid providerDefinitionId)
        {
            var config = await GetAuthenticationDetails(executionContext, providerDefinitionId);

            return await VerifyConnection(executionContext, config.Authentication);
        }

        public override async Task<bool> VerifyConnection(ExecutionContext executionContext, IDictionary<string, object> config)
        {
            var data = new AzureServiceBusConnectorJobData(config);

            ServiceBusConnectionStringProperties properties;

            try
            {
                properties = ServiceBusConnectionStringProperties.Parse(data.ConnectionString);
            }
            catch
            {
                return false;
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
                        return false;
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
                    return false;
                }
            }

            return true;
        }

        public override async Task StoreData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, IDictionary<string, object> data)
        {
            var sender = await GetSender(executionContext, providerDefinitionId, containerName);
            var message = new ServiceBusMessage(JsonUtility.Serialize(data));

            try
            {
                await ActionExtensions.ExecuteWithRetryAsync(() => sender.SendMessageAsync(message));
            }
            catch (Exception exc)
            {
                executionContext.Log.LogError(exc, "Could not send event to Azure Service Bus.");
            }
        }

        public override async Task StoreEdgeData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, string originEntityCode, IEnumerable<string> edges)
        {
            await Task.FromResult(0);
        }

        private async Task<ServiceBusSender> GetSender(ExecutionContext executionContext, Guid providerDefinitionId, string containerName)
        {
            var authDetails = await GetAuthenticationDetails(executionContext, providerDefinitionId);
            var config = new AzureServiceBusConnectorJobData(authDetails.Authentication);

            var key = $"GetSender-{config.ConnectionString}-{config.Name}-{containerName}";

            return _memoryCache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.RegisterPostEvictionCallback((_, value, reason, state) =>
                {
                    if (value is (ServiceBusClient client, ServiceBusSender sender))
                    {
                        sender.DisposeAsync().GetAwaiter().GetResult();
                        client.DisposeAsync().GetAwaiter().GetResult();
                    }
                });

                try
                {
                    var client = new ServiceBusClient(config.ConnectionString);

                    var properties = ServiceBusConnectionStringProperties.Parse(config.ConnectionString);

                    var sender = client.CreateSender(config.Name ?? properties.EntityPath ?? containerName);

                    return (client, sender);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Exception creating sender");
                    throw;
                }
            }).sender;
        }

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }
    }
}
