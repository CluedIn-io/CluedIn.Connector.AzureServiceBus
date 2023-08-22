using System;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace CluedIn.Connector.AzureServiceBus.Integration.Tests
{
    public class TestOutputLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TestOutputLogger(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _testOutputHelper.WriteLine(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new Mock<IDisposable>().Object;
        }
    }
}
