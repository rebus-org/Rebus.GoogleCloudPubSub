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

public class GoogleCloudPubSubTransport : AbstractRebusTransport, IInitializable, IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<PublisherClient>>> _clients = new();
    private IAsyncTask _leaseRenewalTimer;
    private readonly ConcurrentDictionary<string, MessageLeaseRenewer> _leaseRenewers = new();
    private readonly IMessageConverter _messageConverter;
    private readonly IAsyncTaskFactory _asyncTaskFactory;
    
    private readonly GoogleCloudPubSubTransportSettings _googleCloudPubSubTransportSettings;
    private readonly string _projectId;
    protected readonly ILog Log;
    
    private readonly SubscriberServiceApiClient _googlePubSubscriberServiceApiClient;
    
    private readonly TopicName _googlePubSubTopicName;
    private readonly SubscriptionName _googlePubSubSubscriptionName;

    public GoogleCloudPubSubTransport(string projectId, string queueName, IRebusLoggerFactory rebusLoggerFactory,
        IAsyncTaskFactory asyncTaskFactory,
        IMessageConverter messageConverter,
        GoogleCloudPubSubTransportSettings googleCloudPubSubTransportSettings) : base(
        GetResourcesName(queueName).topic)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        
        if (queueName != null)
        {
            var (topic, subscription) = GetResourcesName(queueName);
            _googlePubSubTopicName = new TopicName(_projectId, topic);
            _googlePubSubSubscriptionName = new SubscriptionName(_projectId, subscription);
            _googlePubSubscriberServiceApiClient = GetSubscriberServiceApiClientAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        if (rebusLoggerFactory == null) throw new ArgumentNullException(nameof(rebusLoggerFactory));
        Log = rebusLoggerFactory.GetLogger<GoogleCloudPubSubTransport>();
        _messageConverter = messageConverter ?? throw new ArgumentNullException(nameof(messageConverter));
        _asyncTaskFactory = asyncTaskFactory ?? throw new ArgumentNullException(nameof(asyncTaskFactory));
        _googleCloudPubSubTransportSettings = googleCloudPubSubTransportSettings;
    }


    public void Dispose()
    {
        _leaseRenewalTimer?.Dispose();
    }

    public void Initialize()
    {
        if (string.IsNullOrEmpty(Address)) return;
        
        CreateQueue(Address);
        AsyncHelpers.RunSync(CreateSubscriptionAsync);
        
        if (_googleCloudPubSubTransportSettings.AutomaticLeaseRenewalEnabled)
        {
            SetupLeaseRenewalTimer(_googleCloudPubSubTransportSettings.AckDeadlineSeconds);
        }
    }

    private void SetupLeaseRenewalTimer(int ackDeadlineSeconds)
    {
        var renewIntervalSeconds = ackDeadlineSeconds / 2;
        _leaseRenewalTimer = _asyncTaskFactory.Create("Lease Renewal", RenewLeases, true, renewIntervalSeconds);

        _leaseRenewalTimer.Start();
    }

    private static (string topic, string subscription) GetResourcesName(string address)
    {
        if (string.IsNullOrEmpty(address))
            return (address, address);

        if (!address.Contains(":")) return (address, address);

        var parts = address.Split(':');

        return (parts[0], parts[1]);
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

    public override void CreateQueue(string address)
    {
        if (_googleCloudPubSubTransportSettings.SkipResourceCreationEnabled)
        {
            Log.Info(
                "Transport configured to not create resources - skipping existence check and potential creation for topic {address}",
                address);
            return;
        }

        Log.Info("Creating topic {topic}", address);
        
        var publisherServiceApiClient = GetPublisherServiceApiClientAsync()
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        
        try
        {
            publisherServiceApiClient.GetTopic(_googlePubSubTopicName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            var retries = 0;
            const int maxRetries = 100;
            while (retries < maxRetries)
            {
                try
                {
                    publisherServiceApiClient.CreateTopic(_googlePubSubTopicName);
                    Task.Delay(5000).GetAwaiter().GetResult();
                    Log.Info("Created topic {topic} ", _googlePubSubTopicName);
                    break;
                }
                catch (Exception e)
                {
                    Log.Warn(e,"Failed creating topic {topic} {times}", 
                        _googlePubSubTopicName.ToString(), retries + 1);
                    retries++;
                    if (retries == maxRetries)
                        throw new RebusApplicationException($"Could not create topic {_googlePubSubTopicName}");
                }
            }
                
            
        }
    }

    public async Task PurgeQueueAsync()
    {
        try
        {
            var publisherServiceApiClient = await GetPublisherServiceApiClientAsync();
            await publisherServiceApiClient.DeleteTopicAsync(_googlePubSubTopicName);
            Log.Info("Purged topic {topic} by deleting it", _googlePubSubTopicName.ToString());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            Log.Warn("Tried purging topic {topic} by deleting it, but it could not be found", _googlePubSubTopicName.ToString());
        }
        
        try
        {
            await _googlePubSubscriberServiceApiClient.DeleteSubscriptionAsync(_googlePubSubSubscriptionName);
            Log.Info("Purged subscription {subscriptionName} by deleting it", _googlePubSubSubscriptionName.ToString());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            Log.Info("Tried purging subscription {subscriptionName} by deleting it, but it could not be found",
                _googlePubSubSubscriptionName.ToString());
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
        if (_googleCloudPubSubTransportSettings.SkipResourceCreationEnabled)
        {
            Log.Info(
                "Transport configured to not create resources - skipping existence check and potential creation for subscription {subscription}",
                _googlePubSubSubscriptionName);
            return;
        }
        
        try
        {
            await _googlePubSubscriberServiceApiClient.GetSubscriptionAsync(_googlePubSubSubscriptionName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            Log.Info("Creating subscription {subscription} for topic {topic}", _googlePubSubSubscriptionName, _googlePubSubTopicName);
            var retries = 0;
            const int maxRetries = 100;
            while (retries < maxRetries)
                try
                {
                    await _googlePubSubscriberServiceApiClient.CreateSubscriptionAsync(_googlePubSubSubscriptionName.ToString(),
                        _googlePubSubTopicName.ToString(), null, _googleCloudPubSubTransportSettings.AckDeadlineSeconds); 
                    
                    await Task.Delay(5000);
                   
                    await _googlePubSubscriberServiceApiClient.GetSubscriptionAsync(_googlePubSubSubscriptionName.ToString());

                    Log.Info("Created subscription {sub} for topic {topic}", _googlePubSubSubscriptionName.ToString(),
                        _googlePubSubTopicName.ToString());
                    break;
                }
                catch (RpcException ex1) when (ex1.StatusCode == StatusCode.NotFound)
                {
                    Log.Warn("Failed creating subscription {sub} for topic {topic} {times}",
                        _googlePubSubSubscriptionName.ToString(), _googlePubSubTopicName.ToString(), retries + 1);
                    retries++;
                    if (retries == maxRetries)
                        throw new RebusApplicationException($"Could not create subscription topic {_googlePubSubTopicName}");
                }
        }
    }

    public override async Task<TransportMessage> Receive(ITransactionContext context,
        CancellationToken cancellationToken)
    {
        if (_googlePubSubscriberServiceApiClient == null) return null;

        ReceivedMessage receivedMessage = null;
        
        try
        {
            var response = await _googlePubSubscriberServiceApiClient.PullAsync(
                new PullRequest { SubscriptionAsSubscriptionName = _googlePubSubSubscriptionName, MaxMessages = 1 },
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

        var leaseRenewer = new MessageLeaseRenewer(receivedMessage, _googlePubSubscriberServiceApiClient, _googlePubSubSubscriptionName, _googleCloudPubSubTransportSettings.AckDeadlineSeconds);
        _leaseRenewers.TryAdd(receivedMessage.Message.MessageId, leaseRenewer);

        context.OnAck(async ctx =>
        {
            await _googlePubSubscriberServiceApiClient.AcknowledgeAsync(_googlePubSubSubscriptionName, new[] { receivedMessage.AckId });
            _leaseRenewers.TryRemove(receivedMessage.Message.MessageId, out _);
        });

        context.OnNack(async ctx =>
        {
            await _googlePubSubscriberServiceApiClient.ModifyAckDeadlineAsync(_googlePubSubSubscriptionName, new[] { receivedMessage.AckId }, 0);
            _leaseRenewers.TryRemove(receivedMessage.Message.MessageId, out _);
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