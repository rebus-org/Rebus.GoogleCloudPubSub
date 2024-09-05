using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.GoogleCloudPubSub.Messages;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Transport;

namespace Rebus.GoogleCloudPubSub;

public class GoogleCloudPubSubTransport : AbstractRebusTransport, IInitializable, IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<PublisherClient>>> _clients = new();
    private readonly string _inputQueueName;
    private readonly IMessageConverter _messageConverter;
    private readonly string _projectId;
    protected readonly ILog Log;

    private TopicName _inputTopic;
    private SubscriberServiceApiClient _subscriberClient;
    private SubscriptionName _subscriptionName;


    public GoogleCloudPubSubTransport(string projectId, string inputQueueName, IRebusLoggerFactory rebusLoggerFactory,
        IMessageConverter messageConverter) : base(inputQueueName)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _inputQueueName = inputQueueName;
        if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));
        Log = rebusLoggerFactory.GetLogger<GoogleCloudPubSubTransport>();
        _messageConverter = messageConverter ?? throw new ArgumentNullException(nameof(messageConverter));
    }

    public void Dispose()
    {
    }

    public void Initialize()
    {
        if (!string.IsNullOrEmpty(_inputQueueName))
        {
            _inputTopic = new TopicName(_projectId, _inputQueueName);
            CreateQueue(_inputQueueName);
            AsyncHelpers.RunSync(CreateSubscriptionAsync);
        }
    }

    private async Task<PublisherServiceApiClient> GetPublisherServiceApiClientAsync()
    {
        return await new PublisherServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync();
    }

    public override void CreateQueue(string address)
    {
        var service = GetPublisherServiceApiClientAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        var topic = new TopicName(_projectId, address);
        try
        {
            service.GetTopic(topic);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            service.CreateTopic(topic);
            Log.Info("Created topic {topic} ", _inputTopic);
        }
    }

    public async Task PurgeQueueAsync()
    {
        var topic = new TopicName(_projectId, _inputQueueName);
        try
        {
            var service = await GetPublisherServiceApiClientAsync();
            await service.DeleteTopicAsync(topic);
            Log.Info("Purged topic {topic} by deleting it", topic.ToString());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            Log.Warn("Tried purging topic {topic} by deleting it, but it could not be found", topic.ToString());
        }

        _subscriptionName = SubscriptionName.FromProjectSubscription(_projectId, _inputQueueName);
        try
        {
            _subscriberClient = await GetSubscriberServiceApiClientAsync();
            await _subscriberClient.DeleteSubscriptionAsync(_subscriptionName);
            Log.Info("Purged subscription {subscriptionname} by deleting it", _subscriptionName.ToString());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            Log.Info("Tried purging subscription {subscriptionname} by deleting it, but it could not be found",
                _subscriptionName.ToString());
        }
    }

    private async Task<SubscriberServiceApiClient> GetSubscriberServiceApiClientAsync()
    {
        return await new SubscriberServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync();
    }

    private async Task CreateSubscriptionAsync()
    {
        _subscriptionName = SubscriptionName.FromProjectSubscription(_projectId, _inputQueueName);

        _subscriberClient = await GetSubscriberServiceApiClientAsync();

        try
        {
            await _subscriberClient.GetSubscriptionAsync(_subscriptionName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            var retries = 0;
            var maxRetries = 10;
            while (retries < maxRetries)
                try
                {
                    await _subscriberClient.CreateSubscriptionAsync(_subscriptionName.ToString(),
                        _inputTopic.ToString(), null, 30);
                    //wait after subscription is created - because some delay on google's side
                    await Task.Delay(5000);
                    Log.Info("Created subscription {sub} for topic {topic}", _subscriptionName.ToString(),
                        _inputTopic.ToString());
                    break;
                }
                catch (RpcException ex1) when (ex1.StatusCode == StatusCode.NotFound)
                {
                    Log.Warn("Failed creating subscription {sub} for topic {topic} {times}",
                        _subscriptionName.ToString(), _inputTopic.ToString(), retries + 1);
                    retries++;
                    if (retries == maxRetries)
                        throw new RebusApplicationException($"Could not create subscription topic {_inputTopic}");
                    await Task.Delay(1000 * retries);
                }
        }
    }

    public override async Task<TransportMessage> Receive(ITransactionContext context,
        CancellationToken cancellationToken)
    {
        if (_subscriberClient == null) return null;

        ReceivedMessage receivedMessage = null;
        try
        {
            var response = await _subscriberClient.PullAsync(
                new PullRequest { SubscriptionAsSubscriptionName = _subscriptionName, MaxMessages = 1 },
                CallSettings.FromCancellationToken(cancellationToken)
            );
            receivedMessage = response.ReceivedMessages.FirstOrDefault();
        }
        catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.Unavailable)
        {
            throw new RebusApplicationException(ex,
                "GooglePubSub UNAVAILABLE due to too many concurrent pull requests pending for the given subscription");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            if (!cancellationToken.IsCancellationRequested)
                //Rebus has not ordered this cancellation - therefore throwing
                throw new RebusApplicationException(ex, "Cancelled when fetching messages from GooglePubSub");
        }
        catch (Exception ex)
        {
            throw new RebusApplicationException(ex, "Failed when fetching messages from GooglePubSub");
        }


        if (receivedMessage == null) return null;

        var receivedTransportMessage = _messageConverter.ToTransport(receivedMessage.Message);

        var utcNow = DateTimeOffset.UtcNow;
        if (receivedTransportMessage.IsExpired(utcNow))
        {
            Log.Debug(
                $"Discarded message {string.Join(",", receivedTransportMessage.Headers.Select(a => a.Key + " : " + a.Value).ToArray())} because message expired {receivedTransportMessage.AbsoluteExpiryTimeUtc()} which is lesser than current time {utcNow}");
            return null;
        }


        context.OnAck(async ctx =>
        {
            await _subscriberClient.AcknowledgeAsync(_subscriptionName, new[] { receivedMessage.AckId });
        });

        context.OnNack(async ctx =>
        {
            await _subscriberClient.ModifyAckDeadlineAsync(_subscriptionName, new[] { receivedMessage.AckId }, 0);
        });

        return receivedTransportMessage;
    }

    protected override async Task SendOutgoingMessages(IEnumerable<OutgoingTransportMessage> outgoingMessages,
        ITransactionContext context)
    {
        var messagesByDestinationQueues = outgoingMessages.GroupBy(m => m.DestinationAddress);

        async Task SendMessagesToQueue(string queueName, IEnumerable<OutgoingTransportMessage> messages)
        {
            var publisherClient = await GetPublisherClient(queueName);

            await Task.WhenAll(
                messages
                    .Select(m => _messageConverter.ToPubsub(m.TransportMessage))
                    .Select(publisherClient.PublishAsync)
            );
        }

        await Task.WhenAll(messagesByDestinationQueues.Select(g => SendMessagesToQueue(g.Key, g)));
    }

    private async Task<PublisherClient> GetPublisherClient(string queueName)
    {
        async Task<PublisherClient> CreatePublisherClient()
        {
            var topicName = TopicName.FromProjectTopic(_projectId, queueName);
            try
            {
                return await new PublisherClientBuilder
                {
                    TopicName = topicName,
                    EmulatorDetection = EmulatorDetection.EmulatorOrProduction
                }.BuildAsync();
            }
            catch (Exception exception)
            {
                throw new RebusApplicationException(exception,
                    $"Could not create publisher client for topic {topicName}");
            }
        }

        var task = _clients.GetOrAdd(queueName, _ => new Lazy<Task<PublisherClient>>(CreatePublisherClient));

        return await task.Value;
    }
}