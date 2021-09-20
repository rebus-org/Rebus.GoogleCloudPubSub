using System;
using System.Collections.Generic;
using System.Linq;
using Google.Cloud.PubSub.V1;
using NUnit.Framework;
using Rebus.Messages;
using Rebus.Sagas.Idempotent;

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

            var googlePubSubMessage = rebusTransportMessage.ToPubSubMessage();
            var received = new ReceivedMessage() { AckId = "ackid", DeliveryAttempt = 1, Message = googlePubSubMessage };
            var final = received.ToRebusTransportMessage();

            Assert.AreEqual(theBodyContent, System.Text.Encoding.UTF8.GetString(final.Body));
            CollectionAssert.AreEqual(theHeaderContent, final.Headers);
        }
    }
}