using System;
using System.Threading.Tasks;
using Google.Api.Gax;
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
        [Explicit]
        public async Task CanDoThis()
        {
            var topicName = TopicName.FromProjectTopic(ProjectId, Constants.Receiver);
            var publisherClient = await PublisherClient.CreateAsync(topicName,new PublisherClient.ClientCreationSettings().WithEmulatorDetection(EmulatorDetection.EmulatorOrProduction));

            var pubsubMessage = new PubsubMessage
            {
                Attributes =
                {
                    {Headers.MessageId, Guid.NewGuid().ToString("n")},
                    {Headers.ContentType, "application/json; charset=utf-8"},
                    {Headers.Type, typeof(SpikeCode).GetSimpleAssemblyQualifiedName()},
                },
                Data = ByteString.CopyFrom(1, 2, 3, 4)
            };

            await publisherClient.PublishAsync(pubsubMessage);

        }
    }
}