using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;

namespace Rebus.GoogleCloudPubSub.Tests
{
    [TestFixture]
    [Explicit]
    public class ReadMeCode : FixtureBase
    {
        [Test]
        public void Normal()
        {
            Configure.With(Using(new BuiltinHandlerActivator()))
                .Transport(t => t.UsePubSub("alluring-brewski-123456", "your_queue"))
                .Start();
        }

        [Test]
        public void OneWayClient()
        {
            var bus = Configure.With(Using(new BuiltinHandlerActivator()))
                .Transport(t => t.UsePubSubAsOneWayClient("alluring-brewski-123456"))
                .Start();
        }
    }
}