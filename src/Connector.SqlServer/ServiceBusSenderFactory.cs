using System.Threading.Tasks;
using System.Threading;
using Azure.Messaging.ServiceBus;

namespace CluedIn.Connector.AzureServiceBus
{
    public interface IServiceBusSenderFactory
    {
        IServiceBusSenderWrapper CreateSender(AzureServiceBusConnectorJobData config, string containerName);
    }

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

    public interface IServiceBusMessageBatchWrapper
    {
        bool TryAddMessage(ServiceBusMessage message);
        int Count { get; }
    }

    internal class ServiceBusMessageBatchWrapper : IServiceBusMessageBatchWrapper
    {
        private readonly ServiceBusMessageBatch _inner;

        protected ServiceBusMessageBatchWrapper(ServiceBusMessageBatch inner)
        {
            _inner = inner;
        }

        public virtual bool TryAddMessage(ServiceBusMessage message) => _inner.TryAddMessage(message);

        public int Count => _inner.Count;
    }


    internal class ServiceBusSenderFactory : IServiceBusSenderFactory
    {
        public IServiceBusSenderWrapper CreateSender(AzureServiceBusConnectorJobData config, string containerName)
        {
            var client = new ServiceBusClient(config.ConnectionString);

            var properties = ServiceBusConnectionStringProperties.Parse(config.ConnectionString);

            var sender = client.CreateSender(config.Name ?? properties.EntityPath ?? containerName);

            return new ServiceBusSenderWrapper(sender);
        }
    }
}
