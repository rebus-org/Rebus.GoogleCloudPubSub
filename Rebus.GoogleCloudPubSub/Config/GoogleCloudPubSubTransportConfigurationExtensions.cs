using System;
using Rebus.Transport;

namespace Rebus.Config
{
    public static class GoogleCloudPubSubTransportConfigurationExtensions
    {
        public static void UsePubSub(this StandardConfigurer<ITransport> configurer, string inputQueueName)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));
        }

        public static void UsePubSubAsOneWayClient(this StandardConfigurer<ITransport> configurer)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        }
    }
}
