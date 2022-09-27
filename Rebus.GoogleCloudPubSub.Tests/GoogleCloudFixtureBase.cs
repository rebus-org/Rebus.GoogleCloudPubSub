using Rebus.Tests.Contracts;

namespace Rebus.GoogleCloudPubSub.Tests
{
    public abstract class GoogleCloudFixtureBase : FixtureBase
    {
        protected readonly string ProjectId = GoogleCredentials.GetProjectIdFromGoogleCredentials();

        protected override void SetUp()
        {
            base.SetUp();
            GoogleCredentials.GetProjectIdFromGoogleCredentials();
        }
    }
}
