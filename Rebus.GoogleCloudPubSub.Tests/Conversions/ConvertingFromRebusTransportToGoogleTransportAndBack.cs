using System;
using System.Collections.Generic;
using Google.Cloud.PubSub.V1;
using NUnit.Framework;
using Rebus.GoogleCloudPubSub.Messages;
using Rebus.Messages;

namespace Rebus.GoogleCloudPubSub.Tests.Conversions
{
    [TestFixture]
    public class ConvertingFromRebusTransportToGoogleTransportAndBack
    {
        [Test]
        public void ShouldNotAddOrRemoveAnyContent()
        {
            var theBodyContent = "SomeBodyContent";
            var theHeaderContent = new Dictionary<string, string>() { { "xheader1", "header1content" }, { "xheader2", "header2content" }, { Headers.MessageId, Guid.NewGuid().ToString() } };
            var rebusTransportMessage = new TransportMessage(theHeaderContent, System.Text.Encoding.UTF8.GetBytes(theBodyContent));

            var messageConverter = new DefaultMessageConverter();
            
            var googlePubSubMessage = messageConverter.ToPubsub(rebusTransportMessage);
            var received = new ReceivedMessage { AckId = "ackid", DeliveryAttempt = 1, Message = googlePubSubMessage };
            var final = messageConverter.ToTransport(received.Message);

            Assert.AreEqual(theBodyContent, System.Text.Encoding.UTF8.GetString(final.Body));
            CollectionAssert.AreEqual(theHeaderContent, final.Headers);
        }
    }
}