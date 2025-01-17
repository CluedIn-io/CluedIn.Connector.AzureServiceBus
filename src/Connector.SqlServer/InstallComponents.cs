using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using CluedIn.Connector.AzureServiceBus.Services;

namespace CluedIn.Connector.AzureServiceBus
{
    public class InstallComponents : IWindsorInstaller
    {
        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            container.Register(Component.For<IServiceBusSenderFactory>().ImplementedBy<ServiceBusSenderFactory>().LifestyleSingleton());
            container.Register(Component.For<IClockService>().ImplementedBy<ClockService>().LifestyleSingleton());
        }
    }
}
