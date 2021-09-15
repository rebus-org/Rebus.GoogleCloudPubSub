using System;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Rebus.Messages;

namespace Rebus.GoogleCloudPubSub
{
    public static class PubSubMessageExtensions
    {
        public static DateTimeOffset AbsoluteExpiryTimeUtc(this PubsubMessage message)
        {
            if (message.Attributes.ContainsKey(Headers.TimeToBeReceived) && message.Attributes.ContainsKey(Headers.SentTime))
            {
                if (TimeSpan.TryParse(message.Attributes[Headers.TimeToBeReceived], out var timeToBeReceived) && DateTimeOffset.TryParse(message.Attributes[Headers.SentTime], out var sentTime))
                {
                    return sentTime.Add(timeToBeReceived).ToUniversalTime();
                }
            }
            return DateTimeOffset.MinValue;
        }

        public static bool IsExpired(this PubsubMessage message, DateTimeOffset comparedTo)
        {
            var absoluteExpiry = message.AbsoluteExpiryTimeUtc();
            return absoluteExpiry > DateTimeOffset.MinValue && absoluteExpiry < comparedTo;
        }
    }
}