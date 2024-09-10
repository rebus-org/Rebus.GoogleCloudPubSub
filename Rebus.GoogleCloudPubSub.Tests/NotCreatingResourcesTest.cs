using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;

namespace Rebus.GoogleCloudPubSub.Tests;

[TestFixture]
public class NotCreatingResourcesTest : GoogleCloudFixtureBase
{
    [Test]
    public async Task ShouldNotCreateTopicOrSubscriptionWhenConfiguredNotTo()
    {
        var topicName = "any-topic-name";
        var subscriptionName = "any-subscription-name";
        var queueName = $"{topicName}:{subscriptionName}";

        var publisherClient = await new PublisherServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync();

        var subscriberClient = await new SubscriberServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync();


        var activator = Using(new BuiltinHandlerActivator());

        Configure.With(activator)
            .Logging(l => l.ColoredConsole())
            .Transport(t =>
            {
                t.UsePubSub(ProjectId, queueName)
                    .SkipResourceCreation();
            })
            .Start();

        var topicExists = await TopicExistsAsync(publisherClient, ProjectId, topicName);
        Assert.That(topicExists, Is.False);

        var subscriptionExists = await SubscriptionExistsAsync(subscriberClient, ProjectId, subscriptionName);
        Assert.That(subscriptionExists, Is.False);
    }

    private static async Task<bool> TopicExistsAsync(PublisherServiceApiClient client, string projectId, string topicId)
    {
        try
        {
            await client.GetTopicAsync(TopicName.FromProjectTopic(projectId, topicId));
            return true;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }

    private static async Task<bool> SubscriptionExistsAsync(SubscriberServiceApiClient client, string projectId,
        string subscriptionId)
    {
        try
        {
            await client.GetSubscriptionAsync(SubscriptionName.FromProjectSubscription(projectId, subscriptionId));
            return true;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }
}