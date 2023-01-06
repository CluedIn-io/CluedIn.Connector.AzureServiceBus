using Castle.MicroKernel.Registration;
using CluedIn.Core;
using ComponentHost;
using Connector.Common;

namespace CluedIn.Connector.AzureServiceBus
{
    [Component(AzureServiceBusConstants.ProviderName, "Providers", ComponentType.Service, ServerComponents.ProviderWebApi, Components.Server, Components.DataStores, Isolation = ComponentIsolation.NotIsolated)]
    public sealed class AzureServiceBusConnectorComponent : ComponentBase<InstallComponents>
    {
        public AzureServiceBusConnectorComponent(ComponentInfo componentInfo) : base(componentInfo)
        {
        }
    }
}
