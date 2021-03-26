using System;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Tests.Contracts.Transports;

namespace Rebus.GoogleCloudPubSub.Tests.Factory
{
    public class GoogleCloudPubSubBusFactory : IBusFactory
    {
        public IBus GetBus<TMessage>(string inputQueueAddress, Func<TMessage, Task> handler)
        {
            throw new NotImplementedException();
        }

        public void Cleanup()
        {
            throw new NotImplementedException();
        }
    }
}