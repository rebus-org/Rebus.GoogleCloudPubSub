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
                .Transport(t => t.UsePubSub(Constants.Receiver))
                .Start();
        }

        [Test]
        public void OneWayClient()
        {
            Configure.With(Using(new BuiltinHandlerActivator()))
                 .Transport(t => t.UsePubSubAsOneWayClient())
                 .Start();
        }
    }
}