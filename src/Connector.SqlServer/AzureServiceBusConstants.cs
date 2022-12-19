using System;
using System.Collections.Generic;
using CluedIn.Core.Net.Mail;
using CluedIn.Core.Providers;

namespace CluedIn.Connector.AzureServiceBus
{
    public class AzureServiceBusConstants
    {
        public struct KeyName
        {
            public const string ConnectionString = "connectinString";
            public const string Name = "name";
        }

        public const string ConnectorName = "AzureServiceBusConnector";
        public const string ConnectorComponentName = "AzureServiceBusConnector";
        public const string ConnectorDescription = "Supports publishing of data to Azure Service Bus.";
        public const string Uri = "https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview";

        public static readonly Guid ProviderId = Guid.Parse("{3AFFA8BB-D16E-487F-8291-6300DF5D4C25}");
        public const string ProviderName = "Azure Service Bus Connector";
        public const bool SupportsConfiguration = false;
        public const bool SupportsWebHooks = false;
        public const bool SupportsAutomaticWebhookCreation = false;
        public const bool RequiresAppInstall = false;
        public const string AppInstallUrl = null;
        public const string ReAuthEndpoint = null;

        public static IList<string> ServiceType = new List<string> { "Connector" };
        public static IList<string> Aliases = new List<string> { "AzureServiceBusConnector" };
        public const string IconResourceName = "Resources.service-bus.svg";
        public const string Instructions = "Provide authentication instructions here, if applicable";
        public const IntegrationType Type = IntegrationType.Connector;
        public const string Category = "Connectivity";
        public const string Details = "Supports publishing of data to Azure Service Bus.";

        public static AuthMethods AuthMethods = new AuthMethods
        {
            token = new Control[]
            {
                new Control
                {
                    name = KeyName.ConnectionString,
                    displayName = "Connection String",
                    type = "password",
                    isRequired = true
                },
                new Control
                {
                    name = KeyName.Name,
                    displayName = "Queue Name (default: EntityPath from connection string, else the target name of the stream)",
                    type = "input",
                    isRequired = false,
                },
            }
        };

        public static IEnumerable<Control> Properties = new List<Control>
        {

        };

        public static readonly ComponentEmailDetails ComponentEmailDetails = new ComponentEmailDetails {
            Features = new Dictionary<string, string>
            {
                                       { "Connectivity",        "Expenses and Invoices against customers" }
                                   },
            Icon = ProviderIconFactory.CreateConnectorUri(ProviderId),
            ProviderName = ProviderName,
            ProviderId = ProviderId,
            Webhooks = SupportsWebHooks
        };

        public static IProviderMetadata CreateProviderMetadata()
        {
            return new ProviderMetadata {
                Id = ProviderId,
                ComponentName = ConnectorName,
                Name = ProviderName,
                Type = "Connector",
                SupportsConfiguration = SupportsConfiguration,
                SupportsWebHooks = SupportsWebHooks,
                SupportsAutomaticWebhookCreation = SupportsAutomaticWebhookCreation,
                RequiresAppInstall = RequiresAppInstall,
                AppInstallUrl = AppInstallUrl,
                ReAuthEndpoint = ReAuthEndpoint,
                ComponentEmailDetails = ComponentEmailDetails
            };
        }
    }
}
