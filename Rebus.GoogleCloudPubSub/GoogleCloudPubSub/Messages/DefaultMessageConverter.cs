using System;
using System.Globalization;
using System.Linq;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Rebus.Extensions;
using Rebus.Messages;

namespace Rebus.GoogleCloudPubSub.Messages;

public class DefaultMessageConverter : IMessageConverter
{
    public TransportMessage ToTransport(PubsubMessage message)
    {
        var attributes = message.Attributes;

        var headers = attributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString());

        if (headers.TryGetValue(ExtraHeaders.ContentType, out var contentType))
            headers[Headers.ContentType] = contentType;

        if (headers.TryGetValue(ExtraHeaders.CorrelationId, out var correlationId))
            headers[Headers.CorrelationId] = correlationId;

        if (!string.IsNullOrEmpty(message.OrderingKey)) headers[ExtraHeaders.OrderingKey] = message.OrderingKey;
        
        if (message.PublishTime is not null)
        {
            headers[Headers.SentTime] = message.PublishTime.ToDateTime().ToString("o", CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(message.MessageId))
        {
            headers[Headers.MessageId] = message.MessageId;
        }

        return new TransportMessage(headers, message.Data.ToByteArray());
    }

    public PubsubMessage ToPubsub(TransportMessage transportMessage)
    {
        var message = new PubsubMessage
        {
            Data = ByteString.CopyFrom(transportMessage.Body)
        };

        var headers = transportMessage.Headers.Clone();

        if (headers.TryGetValue(Headers.MessageId, out var messageId))
        {
            message.MessageId = messageId;
            headers.Remove(Headers.MessageId);
        }

        if (headers.TryGetValue(Headers.SentTime, out var sentTimeString) &&
            DateTime.TryParse(sentTimeString, out var sentTime))
        {
            headers.Remove(Headers.SentTime);
            message.PublishTime = Timestamp.FromDateTime(sentTime.ToUniversalTime());
        }

        if (headers.TryGetValue(ExtraHeaders.OrderingKey, out var orderingKey))
        {
            headers.Remove(ExtraHeaders.OrderingKey);
            message.OrderingKey = orderingKey;
        }

        if (headers.TryGetValue(Headers.ContentType, out var contentType))
        {
            headers.Remove(Headers.ContentType);
            message.Attributes[ExtraHeaders.ContentType] = contentType;
        }

        if (headers.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            headers.Remove(Headers.CorrelationId);
            message.Attributes[ExtraHeaders.CorrelationId] = correlationId;
        }

        foreach (var kvp in headers) message.Attributes[kvp.Key] = kvp.Value;

        return message;
    }
}