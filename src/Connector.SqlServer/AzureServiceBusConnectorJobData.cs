using System.Collections.Generic;
using CluedIn.Core.Crawling;

namespace CluedIn.Connector.AzureServiceBus
{
    public class AzureServiceBusConnectorJobData : CrawlJobData
    {
        public AzureServiceBusConnectorJobData(IDictionary<string, object> configuration)
        {
            if (configuration == null)
            {
                return;
            }

            ConnectionString = GetValue<string>(configuration, AzureServiceBusConstants.KeyName.ConnectionString);
            Name = GetValue<string>(configuration, AzureServiceBusConstants.KeyName.Name);
        }

        public IDictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object> {
                { AzureServiceBusConstants.KeyName.ConnectionString, ConnectionString },
                { AzureServiceBusConstants.KeyName.Name, Name }
            };
        }

        public string ConnectionString { get; set; }

        public string Name { get; set; }
    }
}
