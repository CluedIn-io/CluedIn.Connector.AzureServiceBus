using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CluedIn.Core;
using CluedIn.Core.Configuration;
using CluedIn.Core.Connectors;
using CluedIn.Core.DataStore;
using CluedIn.Core.Processing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public class AzureServiceBusConnector : ConnectorBase
    {
        private readonly ILogger<AzureServiceBusConnector> _logger;
        private readonly IAzureServiceBusClient _client;

     
        public AzureServiceBusConnector(IConfigurationRepository repo, ILogger<AzureServiceBusConnector> logger, IAzureServiceBusClient client) : base(repo)
        {
            ProviderId = AzureServiceBusConstants.ProviderId;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public override async Task CreateContainer(ExecutionContext executionContext, Guid providerDefinitionId, CreateContainerModel model)
        {
            await Task.FromResult(0);
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
            return await Task.FromResult(true);
        }

        public override async Task<bool> VerifyConnection(ExecutionContext executionContext, IDictionary<string, object> config)
        {
            return await Task.FromResult(true);
        }

        public override async Task StoreData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, IDictionary<string, object> data)
        {
            var config = await base.GetAuthenticationDetails(executionContext, providerDefinitionId);

            await using var client = new ServiceBusClient((string)config.Authentication[AzureServiceBusConstants.KeyName.ConnectionString]);
          
            ServiceBusSender sender = client.CreateSender((string)config.Authentication[AzureServiceBusConstants.KeyName.Name]);
            ServiceBusMessage message = new ServiceBusMessage(JsonUtility.Serialize(data));

            try
            {
                await sender.SendMessageAsync(message);
            }
            catch (Exception exc)
            {
                executionContext.Log.LogError("Could not send event to Azure Service Bus.", exc);
            }
            finally
            {
                await client.DisposeAsync();
            }
        }

        public override async Task StoreEdgeData(ExecutionContext executionContext, Guid providerDefinitionId, string containerName, string originEntityCode, IEnumerable<string> edges)
        {
            await Task.FromResult(0);
        }
    }
}
