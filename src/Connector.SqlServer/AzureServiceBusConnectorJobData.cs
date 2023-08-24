using System;
using System.Collections.Generic;
using CluedIn.Core.Crawling;

namespace CluedIn.Connector.AzureServiceBus
{
    public class AzureServiceBusConnectorJobData : CrawlJobData
    {
        public string ConnectionString { get; set; }

        public string Name { get; set; }

        public AzureServiceBusConnectorJobData(IDictionary<string, object> configuration)
        {
            if (configuration == null)
            {
                return;
            }

            ConnectionString = GetValue<string>(configuration, AzureServiceBusConstants.KeyName.ConnectionString);
            Name = GetValue<string>(configuration, AzureServiceBusConstants.KeyName.Name);
        }

        protected bool Equals(AzureServiceBusConnectorJobData other)
        {
            return ConnectionString == other.ConnectionString && Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((AzureServiceBusConnectorJobData)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ConnectionString, Name);
        }

        public IDictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object> {
                { AzureServiceBusConstants.KeyName.ConnectionString, ConnectionString },
                { AzureServiceBusConstants.KeyName.Name, Name }
            };
        }
    }
}
