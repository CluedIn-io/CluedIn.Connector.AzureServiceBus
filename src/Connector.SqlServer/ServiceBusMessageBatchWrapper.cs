using Azure.Messaging.ServiceBus;

namespace CluedIn.Connector.AzureServiceBus
{
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
}
