using System;
using Rebus.GoogleCloudPubSub;
using Rebus.Logging;
using Rebus.Transport;

namespace Rebus.Config
{
    public static class GoogleCloudPubSubTransportConfigurationExtensions
    {
        public static void UsePubSub(this StandardConfigurer<ITransport> configurer, string inputQueueName)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));
            configurer.Register(c => new GoogleCloudPubSubTransport(ProjectId, inputQueueName, c.Get<IRebusLoggerFactory>()));
        }

        public static void UsePubSubAsOneWayClient(this StandardConfigurer<ITransport> configurer)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            configurer.Register(c => new GoogleCloudPubSubTransport(ProjectId, null, c.Get<IRebusLoggerFactory>()));
        }

        private static string ProjectId => GoogleCredentials.GetGoogleCredentialsFromEnvironmentVariable().ProjectId;
    }
}
