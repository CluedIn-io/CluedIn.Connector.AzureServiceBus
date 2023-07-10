using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using CluedIn.Core.Connectors;
using Microsoft.Data.SqlClient;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public class AzureServiceBusClient : IAzureServiceBusClient
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public async Task ExecuteCommandAsync(IConnectorConnection config, string commandText, IList<SqlParameter> param = null)
        {
            await Task.FromResult(0);
        }
    }
}
