using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CluedIn.Core;
using CluedIn.Core.Crawling;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.Providers;
using CluedIn.Core.Webhooks;
using CluedIn.Providers.Models;
using Newtonsoft.Json;

namespace CluedIn.Connector.AzureServiceBus
{
    public class AzureServiceBusConnectorProvider : ProviderBase, IExtendedProviderMetadata
    {
        /**********************************************************************************************************
         * CONSTRUCTORS
         **********************************************************************************************************/

        public AzureServiceBusConnectorProvider([NotNull] ApplicationContext appContext)
            : base(appContext, AzureServiceBusConstants.CreateProviderMetadata())
        {
           
        }

        /**********************************************************************************************************
         * METHODS
         **********************************************************************************************************/

        public override async Task<CrawlJobData> GetCrawlJobData(
            ProviderUpdateContext context,
            IDictionary<string, object> configuration,
            Guid organizationId,
            Guid userId,
            Guid providerDefinitionId)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var result = new AzureServiceBusConnectorJobData(configuration);

            return await Task.FromResult(result);
        }

        public override Task<bool> TestAuthentication(
            ProviderUpdateContext context,
            IDictionary<string, object> configuration,
            Guid organizationId,
            Guid userId,
            Guid providerDefinitionId)
        {
            throw new NotImplementedException();
        }

        public override Task<ExpectedStatistics> FetchUnSyncedEntityStatistics(ExecutionContext context, IDictionary<string, object> configuration, Guid organizationId, Guid userId, Guid providerDefinitionId)
        {
            throw new NotImplementedException();
        }

        public override Task<IDictionary<string, object>> GetHelperConfiguration(
            ProviderUpdateContext context,
            [NotNull] CrawlJobData jobData,
            Guid organizationId,
            Guid userId,
            Guid providerDefinitionId)
        {
            if (jobData == null)
                throw new ArgumentNullException(nameof(jobData));

            if (jobData is AzureServiceBusConnectorJobData result)
            {
                return Task.FromResult(result.ToDictionary());
            }

            throw new InvalidOperationException($"Unexpected data type for {nameof(AzureServiceBusConnectorJobData)}, {jobData.GetType()}");
        }

        public override Task<IDictionary<string, object>> GetHelperConfiguration(
            ProviderUpdateContext context,
            CrawlJobData jobData,
            Guid organizationId,
            Guid userId,
            Guid providerDefinitionId,
            string folderId)
        {
            return GetHelperConfiguration(context, jobData, organizationId, userId, providerDefinitionId);
        }

        public override Task<AccountInformation> GetAccountInformation(ExecutionContext context, [NotNull] CrawlJobData jobData, Guid organizationId, Guid userId, Guid providerDefinitionId)
        {
            if (jobData == null)
            {
                throw new ArgumentNullException(nameof(jobData));
            }

            if (!(jobData is AzureServiceBusConnectorJobData result))
            {
                throw new ArgumentException(
                    "Wrong CrawlJobData type", nameof(jobData));
            }

            var parts = new List<string>();

            var properties = ServiceBusConnectionStringProperties.Parse(result.ConnectionString);
            parts.Add(properties.FullyQualifiedNamespace);
            if (!string.IsNullOrWhiteSpace(properties.SharedAccessKeyName))
            {
                parts.Add(properties.SharedAccessKeyName);
            }

            if (!string.IsNullOrWhiteSpace(result.Name))
            {
                parts.Add(result.Name);
            }
            else if (!string.IsNullOrWhiteSpace(properties.EntityPath))
            {
                parts.Add(properties.EntityPath);
            }

            var accountId = string.Join(" - ", parts);

            return Task.FromResult(new AccountInformation(accountId, $"{accountId}"));
        }

        public override string Schedule(DateTimeOffset relativeDateTime, bool webHooksEnabled)
        {
            return $"{relativeDateTime.Minute} 0/23 * * *";
        }

        public override Task<IEnumerable<WebHookSignature>> CreateWebHook(ExecutionContext context, [NotNull] CrawlJobData jobData, [NotNull] IWebhookDefinition webhookDefinition, [NotNull] IDictionary<string, object> config)
        {
            if (jobData == null) throw new ArgumentNullException(nameof(jobData));
            if (webhookDefinition == null) throw new ArgumentNullException(nameof(webhookDefinition));
            if (config == null) throw new ArgumentNullException(nameof(config));

            throw new NotImplementedException();
        }

        public override Task<IEnumerable<WebhookDefinition>> GetWebHooks(ExecutionContext context)
        {
            throw new NotImplementedException();
        }

        public override Task DeleteWebHook(ExecutionContext context, [NotNull] CrawlJobData jobData, [NotNull] IWebhookDefinition webhookDefinition)
        {
            if (jobData == null) throw new ArgumentNullException(nameof(jobData));
            if (webhookDefinition == null) throw new ArgumentNullException(nameof(webhookDefinition));

            throw new NotImplementedException();
        }

        public override IEnumerable<string> WebhookManagementEndpoints([NotNull] IEnumerable<string> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));

            // TODO should ids also be checked for being empty ?

            throw new NotImplementedException();
        }

        public override Task<CrawlLimit> GetRemainingApiAllowance(ExecutionContext context, [NotNull] CrawlJobData jobData, Guid organizationId, Guid userId, Guid providerDefinitionId)
        {
            if (jobData == null) throw new ArgumentNullException(nameof(jobData));

            //TODO what the hell is this?
            //There is no limit set, so you can pull as often and as much as you want.
            return Task.FromResult(new CrawlLimit(-1, TimeSpan.Zero));
        }

        public string Icon => AzureServiceBusConstants.IconResourceName;
        public string Domain { get; } = AzureServiceBusConstants.Uri;
        public string About { get; } = AzureServiceBusConstants.ConnectorDescription;
        public AuthMethods AuthMethods => AzureServiceBusConstants.AuthMethods;
        public IEnumerable<Control> Properties { get; } = AzureServiceBusConstants.Properties;
        public string ServiceType { get; } = JsonConvert.SerializeObject(AzureServiceBusConstants.ServiceType);
        public string Aliases { get; } = JsonConvert.SerializeObject(AzureServiceBusConstants.Aliases);
        public Guide Guide { get; set; } = new Guide {
            Instructions = AzureServiceBusConstants.Instructions,
            Value = new List<string> { AzureServiceBusConstants.ConnectorDescription },
            Details = AzureServiceBusConstants.Details

        };

        public string Details { get; set; } = AzureServiceBusConstants.Details;
        public string Category { get; set; } = AzureServiceBusConstants.Category;
        public new IntegrationType Type { get; set; } = AzureServiceBusConstants.Type;
    }
}
