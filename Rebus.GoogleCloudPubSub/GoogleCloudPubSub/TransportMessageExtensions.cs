using System;
using Rebus.Messages;

namespace Rebus.GoogleCloudPubSub;

public static class TransportMessageExtensions
{
    public static DateTimeOffset AbsoluteExpiryTimeUtc(this TransportMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (message.Headers.ContainsKey(Headers.TimeToBeReceived) && message.Headers.ContainsKey(Headers.SentTime))
            if (TimeSpan.TryParse(message.Headers[Headers.TimeToBeReceived], out var timeToBeReceived) &&
                DateTimeOffset.TryParse(message.Headers[Headers.SentTime], out var sentTime))
                return sentTime.Add(timeToBeReceived).ToUniversalTime();

        return DateTimeOffset.MinValue;
    }

    public static bool IsExpired(this TransportMessage message, DateTimeOffset comparedTo)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        var absoluteExpiry = message.AbsoluteExpiryTimeUtc();
        return absoluteExpiry > DateTimeOffset.MinValue && absoluteExpiry < comparedTo;
    }
}