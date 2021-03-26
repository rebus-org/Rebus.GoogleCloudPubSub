using System;
using Rebus.Tests.Contracts.Transports;
using Rebus.Transport;

namespace Rebus.GoogleCloudPubSub.Tests.Factory
{
    public class GoogleCloudPubSubTransportFactory : ITransportFactory
    {
        public ITransport CreateOneWayClient()
        {
            throw new NotImplementedException();
        }

        public ITransport Create(string inputQueueAddress)
        {
            throw new NotImplementedException();
        }

        public void CleanUp()
        {
            throw new NotImplementedException();
        }
    }
}