using CluedIn.Core.Connectors;
using CluedIn.Core.Data.Vocabularies;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public class AzureServiceBusConnectorDataType : IConnectionDataType {
        public string Name { get; set; }
        public VocabularyKeyDataType Type { get; set; }
        public string RawDataType { get; set; }
    }
}
