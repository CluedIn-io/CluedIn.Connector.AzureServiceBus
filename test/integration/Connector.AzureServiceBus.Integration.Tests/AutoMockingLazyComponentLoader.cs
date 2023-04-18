using System;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers;
using Moq;

namespace CluedIn.Connector.AzureServiceBus.Integration.Tests
{
    public class AutoMockingLazyComponentLoader : ILazyComponentLoader
    {
        public IRegistration Load(string name, Type service, Arguments arguments)
        {
            if (service.IsInterface)
            {
                var genericType = typeof(Mock<>).MakeGenericType(service);
                var constructor = genericType.GetConstructor(Type.EmptyTypes);

                var mock = (Mock)constructor.Invoke(Array.Empty<object>());

                return Component.For(service).Instance(mock.Object);
            }

            return Component.For(service);
        }
    }
}
