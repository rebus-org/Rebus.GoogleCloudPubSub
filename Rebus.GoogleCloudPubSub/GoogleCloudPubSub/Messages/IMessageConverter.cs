using Google.Cloud.PubSub.V1;
using Rebus.Messages;

namespace Rebus.GoogleCloudPubSub.Messages;

public interface IMessageConverter
{
    TransportMessage ToTransport(PubsubMessage message);
    PubsubMessage ToPubsub(TransportMessage transportMessage);
}