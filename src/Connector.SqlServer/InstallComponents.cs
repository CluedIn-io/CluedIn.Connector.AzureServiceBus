using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using CluedIn.Connector.AzureServiceBus.Connector;

namespace CluedIn.Connector.AzureServiceBus
{
    public class InstallComponents : IWindsorInstaller
    {
        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            container.Register(Component.For<IMemoryCacheFactory>().ImplementedBy<MemoryCacheFactory>());
        }
    }
}
