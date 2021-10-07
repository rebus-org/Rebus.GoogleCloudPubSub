using System;
using Rebus.GoogleCloudPubSub;
using Rebus.Logging;
using Rebus.Transport;

namespace Rebus.Config
{
    public static class GoogleCloudPubSubTransportConfigurationExtensions
    {
        public static void UsePubSub(this StandardConfigurer<ITransport> configurer, string projectId, string inputQueueName)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));
            configurer.Register(c => new GoogleCloudPubSubTransport(projectId, inputQueueName, c.Get<IRebusLoggerFactory>()));
        }

        public static void UsePubSubAsOneWayClient(this StandardConfigurer<ITransport> configurer, string projectId)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            configurer.Register(c => new GoogleCloudPubSubTransport(projectId, null, c.Get<IRebusLoggerFactory>()));
        }

        public static void UsePubSub(this StandardConfigurer<ITransport> configurer, string inputQueueName)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));
            configurer.Register(c => new GoogleCloudPubSubTransport(DefaultProjectId, inputQueueName, c.Get<IRebusLoggerFactory>()));
        }

        public static void UsePubSubAsOneWayClient(this StandardConfigurer<ITransport> configurer)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            configurer.Register(c => new GoogleCloudPubSubTransport(DefaultProjectId, null, c.Get<IRebusLoggerFactory>()));
        }

        private static string DefaultProjectId => GoogleCredentials.GetGoogleCredentialsFromEnvironmentVariable().ProjectId;
    }
}
