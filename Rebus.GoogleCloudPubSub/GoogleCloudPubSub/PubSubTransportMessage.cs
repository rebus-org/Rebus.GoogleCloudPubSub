using System.Collections.Generic;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Newtonsoft.Json;
using Rebus.Extensions;
using Rebus.Messages;


namespace Rebus.GoogleCloudPubSub
{
    public class PubSubTransportMessage
    {
        public byte[] Headers { get; set; }
        public byte[] Body { get; set; }
    }
}