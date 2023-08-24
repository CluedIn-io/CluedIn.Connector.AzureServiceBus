using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace CluedIn.Connector.AzureServiceBus
{
    public interface IServiceBusSenderWrapper
    {
        ValueTask<IServiceBusMessageBatchWrapper> CreateMessageBatchAsync(
            CancellationToken cancellationToken = default);

        Task SendMessagesAsync(
            IServiceBusMessageBatchWrapper messageBatch,
            CancellationToken cancellationToken = default);
    }

    public class ServiceBusSenderWrapper : IServiceBusSenderWrapper
    {
        private readonly ServiceBusSender _inner;

        public ServiceBusSenderWrapper(ServiceBusSender inner)
        {
            _inner = inner;
        }

        public virtual async ValueTask<IServiceBusMessageBatchWrapper> CreateMessageBatchAsync(
            CancellationToken cancellationToken = default)
        {
            var batch = await _inner.CreateMessageBatchAsync(cancellationToken);
            return new ServiceBusMessageBatchWrapperInstance(batch);
        }

        public virtual Task SendMessagesAsync(
            IServiceBusMessageBatchWrapper messageBatch,
            CancellationToken cancellationToken = default)
        {
            var instance = (ServiceBusMessageBatchWrapperInstance)messageBatch;
            return _inner.SendMessagesAsync(instance.Inner, cancellationToken);
        }

        private class ServiceBusMessageBatchWrapperInstance : ServiceBusMessageBatchWrapper
        {
            public readonly ServiceBusMessageBatch Inner;

            public ServiceBusMessageBatchWrapperInstance(ServiceBusMessageBatch inner) : base(inner)
            {
                Inner = inner;
            }
        }
    }
}
