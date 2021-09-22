using System;
using System.Collections.Generic;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Newtonsoft.Json;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Transport;

namespace Rebus.GoogleCloudPubSub
{
    public static class TransportMessageExtensions
    {
        public static DateTimeOffset AbsoluteExpiryTimeUtc(this TransportMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            if (message.Headers.ContainsKey(Headers.TimeToBeReceived) && message.Headers.ContainsKey(Headers.SentTime))
            {
                if (TimeSpan.TryParse(message.Headers[Headers.TimeToBeReceived], out var timeToBeReceived) && DateTimeOffset.TryParse(message.Headers[Headers.SentTime], out var sentTime))
                {
                    return sentTime.Add(timeToBeReceived).ToUniversalTime();
                }
            }
            return DateTimeOffset.MinValue;
        }

        public static bool IsExpired(this TransportMessage message, DateTimeOffset comparedTo)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var absoluteExpiry = message.AbsoluteExpiryTimeUtc();
            return absoluteExpiry > DateTimeOffset.MinValue && absoluteExpiry < comparedTo;
        }

        public static TransportMessage ToRebusTransportMessage(this ReceivedMessage message)
        {
            var msg = JsonConvert.DeserializeObject<PubSubTransportMessage>(System.Text.Encoding.UTF8.GetString(message.Message.Data.ToByteArray()));
            var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(System.Text.Encoding.UTF8.GetString(msg.Headers));
            return new TransportMessage(headers, msg.Body);
        }

        public static PubsubMessage ToPubSubMessage(this TransportMessage msg)
        {
            var rebusTransportMessage = msg;
            var transport = new PubSubTransportMessage { Headers = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(rebusTransportMessage.Headers)), Body = rebusTransportMessage.Body };
            return new PubsubMessage
            {
                MessageId = rebusTransportMessage.Headers.GetValue(Headers.MessageId),
                Data = ByteString.CopyFrom(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(transport)))
            };
        }
    }
}