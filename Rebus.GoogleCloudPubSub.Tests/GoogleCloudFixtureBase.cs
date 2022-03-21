using System;
using System.IO;
using Grpc.Core;
using Grpc.Core.Logging;
using Rebus.Tests.Contracts;

namespace Rebus.GoogleCloudPubSub.Tests
{
    public abstract class GoogleCloudFixtureBase : FixtureBase
    {
        static GoogleCloudFixtureBase() => GrpcEnvironment.SetLogger(new ConsoleLogger());
        protected readonly string ProjectId = GoogleCredentials.GetProjectIdFromGoogleCredentials();

        protected override void SetUp()
        {
            base.SetUp();
            GoogleCredentials.GetProjectIdFromGoogleCredentials();
        }
    }
}