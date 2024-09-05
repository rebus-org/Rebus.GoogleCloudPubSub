using System;
using Rebus.GoogleCloudPubSub;
using Rebus.GoogleCloudPubSub.Messages;
using Rebus.Logging;
using Rebus.Transport;

namespace Rebus.Config;

public static class GoogleCloudPubSubTransportConfigurationExtensions
{
    public static void UsePubSub(this StandardConfigurer<ITransport> configurer, string projectId,
        string inputQueueName)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));
        configurer.Register(c => new GoogleCloudPubSubTransport(projectId, inputQueueName,
            c.Get<IRebusLoggerFactory>(), c.Get<IMessageConverter>()));
        
        configurer.OtherService<IMessageConverter>().Register(c => new DefaultMessageConverter());
    }

    public static void UsePubSubAsOneWayClient(this StandardConfigurer<ITransport> configurer, string projectId)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        configurer.Register(c =>
            new GoogleCloudPubSubTransport(projectId, null, c.Get<IRebusLoggerFactory>(),
                c.Get<IMessageConverter>()));
        
        configurer.OtherService<IMessageConverter>().Register(c => new DefaultMessageConverter());
    }
}