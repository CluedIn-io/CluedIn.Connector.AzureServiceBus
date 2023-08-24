using System;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public class SenderCacheKey
    {
        public AzureServiceBusConnectorJobData ConnectorJobData { get; }
        public string ContainerName { get; }

        public SenderCacheKey(AzureServiceBusConnectorJobData connectorJobData, string containerName)
        {
            ConnectorJobData = connectorJobData;
            ContainerName = containerName;
        }

        protected bool Equals(SenderCacheKey other)
        {
            return Equals(ConnectorJobData, other.ConnectorJobData) && ContainerName == other.ContainerName;
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

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((SenderCacheKey)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ConnectorJobData, ContainerName);
        }
    }
}
