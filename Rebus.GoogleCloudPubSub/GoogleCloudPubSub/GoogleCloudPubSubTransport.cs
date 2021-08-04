using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Transport;

namespace Rebus.GoogleCloudPubSub
{
    public class GoogleCloudPubSubTransport : AbstractRebusTransport, IInitializable
    {
        readonly ConcurrentDictionary<string, Lazy<Task<PublisherClient>>> _clients = new ConcurrentDictionary<string, Lazy<Task<PublisherClient>>>();
        readonly string _projectId;

        public GoogleCloudPubSubTransport(string projectId, string inputQueueName) : base(inputQueueName)
        {
            _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        }

        public override void CreateQueue(string address)
        {
        }

        public void Initialize()
        {
            CreateQueue(Address);
        }

        public override async Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            return null;
        }

        protected override async Task SendOutgoingMessages(IEnumerable<OutgoingMessage> outgoingMessages, ITransactionContext context)
        {
            var messagesByDestinationQueues = outgoingMessages.GroupBy(m => m.DestinationAddress);

            PubsubMessage ToPubSubMessage(OutgoingMessage message)
            {
                var transportMessage = message.TransportMessage;
                var headers = transportMessage.Headers;
                var body = transportMessage.Body;
                return new PubsubMessage
                {
                    MessageId = headers.GetValue(Headers.MessageId),
                    Attributes = {headers},
                    Data = ByteString.CopyFrom(body)
                };
            }

            async Task SendMessagesToQueue(string queueName, IEnumerable<OutgoingMessage> messages)
            {
                var publisherClient = await GetPublisherClient(queueName);

                await Task.WhenAll(
                    messages
                        .Select(ToPubSubMessage)
                        .Select(publisherClient.PublishAsync)
                );
            }

            await Task.WhenAll(messagesByDestinationQueues.Select(g => SendMessagesToQueue(g.Key, g)));
        }

        async Task<PublisherClient> GetPublisherClient(string queueName)
        {
            async Task<PublisherClient> CreatePublisherClient()
            {
                var topicName = TopicName.FromProjectTopic(_projectId, queueName);
                try
                {
                    return await PublisherClient.CreateAsync(topicName);
                }
                catch (Exception exception)
                {
                    throw new RebusApplicationException(exception, $"Could not create publisher client for topic {topicName}");
                }
            }

            var task = _clients.GetOrAdd(queueName, _ => new Lazy<Task<PublisherClient>>(CreatePublisherClient));

            return await task.Value;
        }
    }
}