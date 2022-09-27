using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;

namespace Rebus.GoogleCloudPubSub.Tests
{
    [TestFixture]
    [Explicit]
    public class ReadMeCode : GoogleCloudFixtureBase
    {
        [Test]
        public void Normal()
        {
            Configure.With(Using(new BuiltinHandlerActivator()))
                .Transport(t => t.UsePubSub(ProjectId,Constants.Receiver))
                .Start();
        }

        [Test]
        public void OneWayClient()
        {
            Configure.With(Using(new BuiltinHandlerActivator()))
                 .Transport(t => t.UsePubSubAsOneWayClient(ProjectId))
                 .Start();
        }
    }
}