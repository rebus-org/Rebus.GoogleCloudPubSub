using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Messages;
using Rebus.Transport;

namespace Rebus.GoogleCloudPubSub
{
    public class GoogleCloudPubSubTransport : AbstractRebusTransport, IInitializable
    {
        public GoogleCloudPubSubTransport(string inputQueueName) : base(inputQueueName)
        {
        }

        public override void CreateQueue(string address)
        {
        }

        public void Initialize()
        {
            CreateQueue(Address);
        }

        public override async Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            return null;
        }

        protected override async Task SendOutgoingMessages(IEnumerable<OutgoingMessage> outgoingMessages, ITransactionContext context)
        {
        }
    }
}