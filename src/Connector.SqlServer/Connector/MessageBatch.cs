using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public class MessageBatch
    {
        public Guid Id { get; }
        public IServiceBusSenderWrapper Sender { get; }
        private readonly IServiceBusMessageBatchWrapper _serviceBusMessageBatch;
        private readonly ILogger _logger;
        private DateTime _lastMessageAddedAt;
        public Task FlushingTask { get; }
        private bool _available = true;

        private MessageBatch(IServiceBusSenderWrapper sender, IServiceBusMessageBatchWrapper serviceBusMessageBatch, ILogger logger)
        {
            Sender = sender;
            _serviceBusMessageBatch = serviceBusMessageBatch;
            _logger = logger;
            Id = Guid.NewGuid();
            FlushingTask = FlushAsync();
        }

        public static async Task<MessageBatch> CreateAsync(IServiceBusSenderWrapper sender, ILogger logger)
        {
            var b = await sender.CreateMessageBatchAsync();

            return new MessageBatch(sender, b, logger);
        }

        public bool TryAddMessage(ServiceBusMessage message)
        {
            lock (this)
            {
                if (!_available)
                {
                    return false;
                }

                if (_serviceBusMessageBatch.TryAddMessage(message))
                {
                    _lastMessageAddedAt = DateTime.Now;

                    return true;
                }

                // TODO do we signal FlushAsync so that we don't have to wait for the next poll interval? just because this message didn't fit doesn't mean others wont

                return false;
            }
        }

        private async Task FlushAsync()
        {
            while (true)
            {
                await Task.Delay(10);

                lock (this)
                {
                    if (DateTime.Now.Subtract(_lastMessageAddedAt).TotalMilliseconds > 10)
                    {
                        _available = false;
                    }
                }

                if (!_available)
                {
                    _logger.Log(LogLevel.Debug, $"[{AzureServiceBusConstants.ConnectorName}] Sending {_serviceBusMessageBatch.Count} messages from batch ({Id})");
                    await Sender.SendMessagesAsync(_serviceBusMessageBatch);
                    _logger.Log(LogLevel.Debug, $"[{AzureServiceBusConstants.ConnectorName}] Sent {_serviceBusMessageBatch.Count} messages from batch ({Id})");
                    return;
                }
            }
        }
    }
}
