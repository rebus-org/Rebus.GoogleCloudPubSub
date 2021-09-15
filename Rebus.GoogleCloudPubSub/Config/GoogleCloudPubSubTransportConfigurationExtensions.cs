using System;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using Rebus.GoogleCloudPubSub;
using Rebus.Injection;
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
