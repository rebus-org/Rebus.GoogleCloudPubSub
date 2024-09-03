using System;
using System.Collections.Generic;
using System.Globalization;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
using Rebus.GoogleCloudPubSub.Messages;
using Rebus.Messages;

namespace Rebus.GoogleCloudPubSub.Tests.GoogleCloudPubSub.Messages;

[TestFixture]
[TestOf(typeof(DefaultMessageConverter))]
public class DefaultMessageConverterTest
{
    [SetUp]
    public void SetUp()
    {
        _converter = new DefaultMessageConverter();
    }

    private DefaultMessageConverter _converter;

    [Test]
    public void ToTransport_ConvertsPubsubMessageToTransportMessage()
    {
        // Arrange
        var pubsubMessage = new PubsubMessage
        {
            Data = ByteString.CopyFromUtf8("Test message"),
            MessageId = "test-message-id",
            PublishTime = Timestamp.FromDateTime(DateTime.UtcNow),
            Attributes =
            {
                { "ContentType", "text/plain" },
                { "CorrelationId", "12345" },
                { "CustomHeader", "CustomValue" }
            },
            OrderingKey = 12345.ToString()
        };

        // Act
        var transportMessage = _converter.ToTransport(pubsubMessage);

        // Assert
        Assert.NotNull(transportMessage);
        Assert.AreEqual(pubsubMessage.Data.ToByteArray(), transportMessage.Body);
        Assert.AreEqual(pubsubMessage.MessageId, transportMessage.Headers[Headers.MessageId]);
        Assert.AreEqual(pubsubMessage.OrderingKey, transportMessage.Headers[ExtraHeaders.OrderingKey]);
        Assert.AreEqual(pubsubMessage.PublishTime.ToDateTime().ToString("o", CultureInfo.InvariantCulture),
            transportMessage.Headers[Headers.SentTime]);
        Assert.AreEqual("text/plain", transportMessage.Headers[Headers.ContentType]);
        Assert.AreEqual("12345", transportMessage.Headers[Headers.CorrelationId]);
        Assert.AreEqual("CustomValue", transportMessage.Headers["CustomHeader"]);
    }

    [Test]
    public void ToPubsub_ConvertsTransportMessageToPubsubMessage()
    {
        // Arrange
        var transportMessage = new TransportMessage(new Dictionary<string, string>
        {
            { Headers.MessageId, "test-message-id" },
            { Headers.ContentType, "text/plain" },
            { ExtraHeaders.OrderingKey, "12345" },
            { Headers.CorrelationId, "12345" },
            { "CustomHeader", "CustomValue" },
            { Headers.SentTime, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }
        }, ByteString.CopyFromUtf8("Test message").ToByteArray());

        // Act
        var pubsubMessage = _converter.ToPubsub(transportMessage);

        // Assert
        Assert.NotNull(pubsubMessage);
        Assert.AreEqual(transportMessage.Body, pubsubMessage.Data.ToByteArray());
        Assert.AreEqual(transportMessage.Headers[Headers.MessageId], pubsubMessage.MessageId);
        Assert.AreEqual(transportMessage.Headers[ExtraHeaders.OrderingKey],
            pubsubMessage.OrderingKey);
        Assert.AreEqual(transportMessage.Headers[Headers.SentTime],
            pubsubMessage.PublishTime.ToDateTime().ToString("o", CultureInfo.InvariantCulture));
        Assert.AreEqual(transportMessage.Headers[Headers.ContentType],
            pubsubMessage.Attributes[ExtraHeaders.ContentType]);
        Assert.AreEqual(transportMessage.Headers[Headers.CorrelationId],
            pubsubMessage.Attributes[ExtraHeaders.CorrelationId]);
        Assert.AreEqual("CustomValue", pubsubMessage.Attributes["CustomHeader"]);
    }

    [Test]
    public void ToTransport_MessageWithoutHeaders_ConvertsSuccessfully()
    {
        // Arrange
        var pubsubMessage = new PubsubMessage
        {
            Data = ByteString.CopyFromUtf8("Test message"),
            MessageId = "test-message-id",
            PublishTime = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        // Act
        var transportMessage = _converter.ToTransport(pubsubMessage);

        // Assert
        Assert.NotNull(transportMessage);
        Assert.AreEqual(pubsubMessage.Data.ToByteArray(), transportMessage.Body);
        Assert.AreEqual(pubsubMessage.MessageId, transportMessage.Headers[Headers.MessageId]);
        Assert.AreEqual(pubsubMessage.PublishTime.ToDateTime().ToString("o", CultureInfo.InvariantCulture),
            transportMessage.Headers[Headers.SentTime]);
        Assert.False(transportMessage.Headers.ContainsKey(Headers.ContentType));
        Assert.False(transportMessage.Headers.ContainsKey(Headers.CorrelationId));
    }

    [Test]
    public void ToPubsub_MessageWithoutHeaders_ConvertsSuccessfully()
    {
        // Arrange
        var body = ByteString.CopyFromUtf8("Test message").ToByteArray();
        var transportMessage = new TransportMessage(new Dictionary<string, string>(), body);

        // Act
        var pubsubMessage = _converter.ToPubsub(transportMessage);

        // Assert
        Assert.NotNull(pubsubMessage);
        Assert.AreEqual(transportMessage.Body, pubsubMessage.Data.ToByteArray());
        Assert.False(pubsubMessage.Attributes.ContainsKey(Headers.MessageId));
        Assert.False(pubsubMessage.Attributes.ContainsKey(Headers.SentTime));
        Assert.False(pubsubMessage.Attributes.ContainsKey(ExtraHeaders.ContentType));
        Assert.False(pubsubMessage.Attributes.ContainsKey(ExtraHeaders.CorrelationId));
        Assert.False(pubsubMessage.Attributes.ContainsKey(Headers.SentTime));
    }
}