namespace Rebus.GoogleCloudPubSub
{
    public class PubSubTransportMessage
    {
        public byte[] Headers { get; set; }
        public byte[] Body { get; set; }
    }
}