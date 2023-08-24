using System;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace CluedIn.Connector.AzureServiceBus.Integration.Tests
{
    public class AutoMockingLazyComponentLoader : ILazyComponentLoader
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public AutoMockingLazyComponentLoader(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public IRegistration Load(string name, Type service, Arguments arguments)
        {
            if (service.IsInterface)
            {
                if (service.IsGenericType && service.GetGenericTypeDefinition() == typeof(ILogger<>))
                {
                    var genericLoggerType = typeof(TestOutputLogger<>).MakeGenericType(service.GetGenericArguments()[0]);
                    var loggerConstructor = genericLoggerType.GetConstructor(new[] { typeof(ITestOutputHelper) });
                    var logger = loggerConstructor.Invoke(new object[] { _testOutputHelper });
                    return Component.For(service).Instance(logger);
                }

                var genericType = typeof(Mock<>).MakeGenericType(service);
                var constructor = genericType.GetConstructor(Type.EmptyTypes);

                var mock = (Mock)constructor.Invoke(Array.Empty<object>());

                return Component.For(service).Instance(mock.Object);
            }

            return Component.For(service);
        }
    }
}
