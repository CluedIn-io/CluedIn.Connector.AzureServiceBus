using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using CluedIn.Core.Connectors;
using Microsoft.Data.SqlClient;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public interface IAzureServiceBusClient
    {
        Task ExecuteCommandAsync(IConnectorConnection config, string commandText, IList<SqlParameter> param = null);
    }
}
