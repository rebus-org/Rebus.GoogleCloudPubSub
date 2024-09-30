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
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.GoogleCloudPubSub.Messages;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Threading;
using Rebus.Transport;

namespace Rebus.GoogleCloudPubSub;

public class GoogleCloudPubSubTransport : ITransport, IInitializable, IDisposable
{
    private static readonly Task<int> TaskCompletedResult = Task.FromResult(0);

    /// <summary>
    ///     Outgoing messages are stashed in a concurrent queue under this key.
    /// </summary>
    private const string OutgoingMessagesKey = "google-service-bus-transport";

    /// <summary>
    ///     The delimiter used to separate the topic and subscription in the address.
    /// </summary>
    private const char TopicSubscriptionDelimiter = ':';

    private readonly string _projectId;

    /// <summary>
    ///     Represents the full address combining the topic and subscription,
    ///     separated by the specified delimiter (e.g., "topic:subscription").
    /// </summary>
    public string Address { get; }

    private IAsyncTask _leaseRenewalTimer;
    private readonly ConcurrentDictionary<string, MessageLeaseRenewer> _leaseRenewers = new();
    private readonly IMessageConverter _messageConverter;
    private readonly IAsyncTaskFactory _asyncTaskFactory;
    protected readonly ILog Log;

    private readonly GoogleCloudPubSubTransportSettings _transportSettings;

    private readonly ConcurrentDictionary<string, Lazy<Task<PublisherClient>>> _googlePubSubPublisherClients = new();
    private readonly SubscriberServiceApiClient _subscriberServiceApiClient;

    public GoogleCloudPubSubTransport(string projectId, string queueName, IRebusLoggerFactory rebusLoggerFactory,
        IAsyncTaskFactory asyncTaskFactory,
        IMessageConverter messageConverter,
        GoogleCloudPubSubTransportSettings transportSettings)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        
        if (queueName != null)
        {
            Address = queueName;
            
            _subscriberServiceApiClient = new SubscriberServiceApiClientBuilder
            {
                EmulatorDetection = EmulatorDetection.EmulatorOrProduction
            }.Build();
        }

        if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));

        Log = rebusLoggerFactory.GetLogger<GoogleCloudPubSubTransport>();

        _messageConverter = messageConverter ?? throw new ArgumentNullException(nameof(messageConverter));
        _asyncTaskFactory = asyncTaskFactory ?? throw new ArgumentNullException(nameof(asyncTaskFactory));
        _transportSettings = transportSettings;
    }


    public void Dispose()
    {
        _leaseRenewalTimer?.Dispose();
    }

    public void Initialize()
    {
        if (string.IsNullOrEmpty(Address)) return;

        var topic = GetTopic(Address);
        var subscription = GetSubscription(Address);

        CreateQueue(topic);

        AsyncHelpers.RunSync(() => CreateSubscriptionAsync(topic, subscription));
        
        if (_transportSettings.AutomaticLeaseRenewalEnabled)
            SetupLeaseRenewalTimer(_transportSettings.AckDeadlineSeconds);
    }

    private void SetupLeaseRenewalTimer(int ackDeadlineSeconds)
    {
        var renewIntervalSeconds = ackDeadlineSeconds / 2;
        _leaseRenewalTimer = _asyncTaskFactory.Create("Lease Renewal", RenewLeases, true, renewIntervalSeconds);

        _leaseRenewalTimer.Start();
    }

    private static string GetTopic(string address)
    {
        if (string.IsNullOrEmpty(address))
            return address;

        if (!address.Contains(TopicSubscriptionDelimiter))
            return address;

        var parts = address.Split(TopicSubscriptionDelimiter);
        return parts[0];
    }

    private static string GetSubscription(string address)
    {
        if (string.IsNullOrEmpty(address))
            return address;

        if (!address.Contains(TopicSubscriptionDelimiter))
            return address;

        var parts = address.Split(TopicSubscriptionDelimiter);
        return parts[1];
    }
    private async Task RenewLeases()
    {
        var leaseRenewer = _leaseRenewers
            .Where(r => r.Value.IsDue)
            .Select(kvp => kvp.Value)
            .ToList();

        if (!leaseRenewer.Any()) return;

        Log.Debug("Identified {count} message leases that are due for renewal to prevent expiration",
            leaseRenewer.Count);

        await Task.WhenAll(leaseRenewer.Select(async messageLeaseRenewer =>
        {
            try
            {
                await messageLeaseRenewer.RenewAsync().ConfigureAwait(false);

                Log.Debug("Successfully renewed lease for message with ID {messageId}", messageLeaseRenewer.MessageId);
            }
            catch (Exception ex)
            {
                if (!_leaseRenewers.ContainsKey(messageLeaseRenewer.ReceivedMessage.Message.MessageId)) return;

                Log.Warn(ex, "Error when renewing lease for message with ID {messageId}",
                    messageLeaseRenewer.MessageId);
            }
        }));
    }

    private async Task<PublisherServiceApiClient> GetPublisherServiceApiClientAsync()
    {
        return await new PublisherServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync();
    }

    public void CreateQueue(string topic)
    {
        if (_transportSettings.SkipResourceCreationEnabled)
        {
            Log.Debug(
                "Transport configured to not create resources - skipping existence check and potential creation for topic {address}",
                topic);
            return;
        }
        
        var publisherServiceApiClient = GetPublisherServiceApiClientAsync()
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        
        var topicName = TopicName.FromProjectTopic(_projectId, topic);
        try
        {
            Log.Debug("Checking topic {topicName} exists", topicName);
            publisherServiceApiClient.GetTopic(topicName);
            Log.Debug("Topic {topicName} exists", topicName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            var retries = 0;
            const int maxRetries = 10;
            while (retries < maxRetries)
            {
                try
                {
                    Log.Info("Creating topic {topicName}", topicName);
                    publisherServiceApiClient.CreateTopic(topicName);
                    Task.Delay(1000).GetAwaiter().GetResult();
                    Log.Info("Created topic {topic} ", topicName);
                    break;
                }
                catch (Exception e)
                {
                    retries++;
                    Log.Warn(e,"Failed creating topic {topicName}, retry attempts: {attempts}", 
                        topicName, retries);
                    if (retries != maxRetries) continue;
                    Log.Error(e, "Error when creating topic {topicName}, retry attempts: {attempts}",
                        topicName, retries);
                    throw new RebusApplicationException(e, $"Could not create topic {topicName}");
                }
            }  
        }
    }

    public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
    {
        var outgoingMessages = context.GetOrAdd(OutgoingMessagesKey, () =>
        {
            var messages = new ConcurrentQueue<OutgoingTransportMessage>();

            context.OnCommit(async _ => await SendOutgoingMessages(messages, context));

            return messages;
        });

        outgoingMessages.Enqueue(new OutgoingTransportMessage(message, destinationAddress));

        return TaskCompletedResult;
    }

    public async Task PurgeQueueAsync()
    {
        var topicName = TopicName.FromProjectTopic(_projectId, GetTopic(Address));
        var subscriptionName = SubscriptionName.FromProjectSubscription(_projectId, GetSubscription(Address));
        var subscriberServiceApiClient = await GetSubscriberServiceApiClientAsync();

        try
        {
            var publisherServiceApiClient = await GetPublisherServiceApiClientAsync();
            await publisherServiceApiClient.DeleteTopicAsync(topicName);
            Log.Info("Purged topic {topic} by deleting it", topicName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            Log.Warn("Tried purging topic {topic} by deleting it, but it could not be found", topicName);
        }
        try
        {
            await subscriberServiceApiClient.DeleteSubscriptionAsync(subscriptionName);
            Log.Info("Purged subscription {subscriptionName} by deleting it", subscriptionName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            Log.Info("Tried purging subscription {subscriptionName} by deleting it, but it could not be found",
                subscriptionName);
        }
    }

    private async Task<SubscriberServiceApiClient> GetSubscriberServiceApiClientAsync()
    {
        return await new SubscriberServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync();
    }

    private async Task CreateSubscriptionAsync(string topic, string subscription)
    {
        if (_transportSettings.SkipResourceCreationEnabled)
        {
            Log.Debug(
                "Transport configured to not create resources - skipping existence check and potential creation for subscription {subscription}",
                subscription);
            return;
        }
        
        var topicName = TopicName.FromProjectTopic(_projectId, topic);
        var subscriptionName = SubscriptionName.FromProjectSubscription(_projectId, subscription);
        var subscriberServiceApiClient = await GetSubscriberServiceApiClientAsync();
        
        try
        {
            Log.Debug("Checking subscription {subscriptionName} exists for topic {topicName}", subscriptionName,
                topicName);
            await subscriberServiceApiClient.GetSubscriptionAsync(subscriptionName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            Log.Info("Creating subscription {subscriptionName} for topic {topicName}", subscriptionName, topicName);
            var retries = 0;
            const int maxRetries = 10;
            while (retries < maxRetries)
                try
                {
                    Log.Info("Creating subscription {subscriptionName} for topic {topicName}", subscriptionName, topicName);
                    
                    var subscriptionSettings = new Subscription
                    {
                        TopicAsTopicName = topicName,
                        SubscriptionName = subscriptionName,
                        AckDeadlineSeconds = _transportSettings.AckDeadlineSeconds
                    };

                    await subscriberServiceApiClient.CreateSubscriptionAsync(subscriptionSettings);
                    
                    await Task.Delay(5000);

                    Log.Info("Created subscription {subscriptionName} for topic {topicName}", subscriptionName, topicName);

                    break;
                }
                catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
                {
                    retries++;
                    Log.Warn("Failed creating subscription {subscriptionName} for topic {topicName}, retry attempts: {attempts}",
                        subscriptionName, topicName, retries);
                    if (retries != maxRetries) continue;
                    Log.Error(e,
                        "Error when creating subscription {subscriptionName} for topic {topicName}, retry attempts: {attempts}",
                        subscriptionName,
                        topicName, retries);
                    throw new RebusApplicationException(e,
                        $"Could not create subscription {subscriptionName} exists for topic {topicName}");
                }
        }
    }

    public async Task<TransportMessage> Receive(ITransactionContext context,
        CancellationToken cancellationToken)
    {
        if (_subscriberServiceApiClient is null) return null;
        
        var subscriptionName = SubscriptionName.FromProjectSubscription(_projectId, GetSubscription(Address));
        
        ReceivedMessage receivedMessage = null;

        try
        {
            var response = await _subscriberServiceApiClient.PullAsync(
                new PullRequest { SubscriptionAsSubscriptionName = subscriptionName, MaxMessages = 1 },
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

        context.Items["gcp-pubsub-received-message"] = receivedMessage;
        context.Items["gcp-pubsub-subscriber-api-client"] = _subscriberServiceApiClient;
        context.Items["gcp-pubsub-subscription-name"] = subscriptionName;

        var receivedTransportMessage = _messageConverter.ToTransport(receivedMessage.Message);

        receivedTransportMessage.Headers[Headers.DeliveryCount] = receivedMessage.DeliveryAttempt.ToString();

        var utcNow = DateTimeOffset.UtcNow;

        if (receivedTransportMessage.IsExpired(utcNow))
        {
            Log.Debug(
                $"Discarded message {string.Join(",", receivedTransportMessage.Headers.Select(a => a.Key + " : " + a.Value).ToArray())} because message expired {receivedTransportMessage.AbsoluteExpiryTimeUtc()} which is lesser than current time {utcNow}");
            return null;
        }

        var leaseRenewer = new MessageLeaseRenewer(receivedMessage, _subscriberServiceApiClient, subscriptionName,
            _transportSettings.AckDeadlineSeconds);

        _leaseRenewers.TryAdd(receivedMessage.Message.MessageId, leaseRenewer);

        context.OnAck(async ctx =>
        {
            _leaseRenewers.TryRemove(receivedMessage.Message.MessageId, out _);

            if (ctx.Items.TryGetValue("gcp-pubsub-received-message", out var messageObject) &&
                messageObject is ReceivedMessage)
                await _subscriberServiceApiClient.AcknowledgeAsync(subscriptionName, new[] { receivedMessage.AckId });
        });

        context.OnNack(async ctx =>
        {
            _leaseRenewers.TryRemove(receivedMessage.Message.MessageId, out _);

            if (ctx.Items.TryGetValue("gcp-pubsub-received-message", out var messageObject) &&
                messageObject is ReceivedMessage)
                await _subscriberServiceApiClient.ModifyAckDeadlineAsync(subscriptionName,
                    new[] { receivedMessage.AckId }, 0);
        });

        return receivedTransportMessage;
    }

    private async Task SendOutgoingMessages(IEnumerable<OutgoingTransportMessage> outgoingMessages,
        ITransactionContext context)
    {
        var messagesByDestinationQueues = outgoingMessages.GroupBy(m => m.DestinationAddress);

        async Task SendMessagesToQueue(string queueName, IEnumerable<OutgoingTransportMessage> messages)
        {

            await Task.WhenAll(
                messages
                    .Select(async m =>
                    {
                        var publisherClient = await GetPublisherClient(GetTopic(queueName));
                        return publisherClient.PublishAsync(_messageConverter.ToPubsub(m.TransportMessage));
                    }));
        }

        await Task.WhenAll(messagesByDestinationQueues.Select(g => SendMessagesToQueue(g.Key, g)));
    }

    private async Task<PublisherClient> GetPublisherClient(string topic)
    {
        async Task<PublisherClient> CreatePublisherClient()
        {
            var topicName = TopicName.FromProjectTopic(_projectId, topic);
            try
            {
                CreateQueue(topic);
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

        var task = _googlePubSubPublisherClients.GetOrAdd(topic, _ => new Lazy<Task<PublisherClient>>(CreatePublisherClient));

        return await task.Value;
    }
}