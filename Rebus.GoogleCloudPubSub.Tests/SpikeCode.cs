using System;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using NUnit.Framework;
using Rebus.Extensions;
using Rebus.Messages;

namespace Rebus.GoogleCloudPubSub.Tests
{
    [TestFixture]
    public class SpikeCode : GoogleCloudFixtureBase
    {
        [Test]
        public async Task CanDoThis()
        {
            var topicName = TopicName.FromProjectTopic("nimble-chimera-321908", "test-topic");
            var publisherClient = await PublisherClient.CreateAsync(topicName);

            var pubsubMessage = new PubsubMessage
            {
                Attributes =
                {
                    {Headers.MessageId, Guid.NewGuid().ToString("n") },
                    {Headers.ContentType, "application/json; charset=utf-8" },
                    {Headers.Type, typeof(SpikeCode).GetSimpleAssemblyQualifiedName()},
                },
                Data = ByteString.CopyFrom(1, 2, 3, 4)
            };

            await publisherClient.PublishAsync(pubsubMessage);

        }
    }
}