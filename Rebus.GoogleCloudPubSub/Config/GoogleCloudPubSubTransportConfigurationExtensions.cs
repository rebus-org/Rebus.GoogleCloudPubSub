using System;
using Rebus.GoogleCloudPubSub;
using Rebus.Injection;
using Rebus.Logging;
using Rebus.Transport;

namespace Rebus.Config
{
    public static class GoogleCloudPubSubTransportConfigurationExtensions
    {
        public static void UsePubSub(this StandardConfigurer<ITransport> configurer, string projectId, string inputQueueName)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (projectId == null) throw new ArgumentNullException(nameof(projectId));
            if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));

            configurer.Register(c =>
                new GoogleCloudPubSubTransport(projectId, inputQueueName, c.Get<IRebusLoggerFactory>()));
        }

        public static void UsePubSubAsOneWayClient(this StandardConfigurer<ITransport> configurer, string projectId)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (projectId == null) throw new ArgumentNullException(nameof(projectId));

            configurer.Register(c => new GoogleCloudPubSubTransport(projectId, null, c.Get<IRebusLoggerFactory>()));
        }

    }
}
