using NUnit.Framework;
using Rebus.GoogleCloudPubSub.Tests.Factory;
using Rebus.Tests.Contracts.Transports;

namespace Rebus.GoogleCloudPubSub.Tests.Contracts
{
    [TestFixture]
    public class GoogleCloudPubSubTestManyMessages : TestManyMessages<GoogleCloudPubSubBusFactory>
    {
    }
}