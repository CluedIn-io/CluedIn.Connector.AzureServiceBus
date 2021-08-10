using System;
using System.Collections.Generic;
using System.Text;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public static class EdgeContainerHelper
    {
        public static string GetName(string containerName) => $"{containerName}Edges";
    }
}
