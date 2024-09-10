using System;
using Rebus.GoogleCloudPubSub;
using Rebus.GoogleCloudPubSub.Messages;
using Rebus.Logging;
using Rebus.Threading;
using Rebus.Transport;

namespace Rebus.Config;

public static class GoogleCloudPubSubTransportConfigurationExtensions
{
    public static GoogleCloudPubSubTransportSettings UsePubSub(this StandardConfigurer<ITransport> configurer, string projectId,
        string inputQueueName)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));
        
        var settings = new GoogleCloudPubSubTransportSettings();
        
        configurer.Register(c => new GoogleCloudPubSubTransport(
            projectId,
            inputQueueName,
            c.Get<IRebusLoggerFactory>(),
            c.Get<IAsyncTaskFactory>(),
            c.Get<IMessageConverter>(),
            settings));

        configurer.OtherService<IMessageConverter>().Register(c => new DefaultMessageConverter());
        
        return settings;
    }

    public static GoogleCloudPubSubTransportSettings UsePubSubAsOneWayClient(this StandardConfigurer<ITransport> configurer, string projectId)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        
        var settings = new GoogleCloudPubSubTransportSettings();
        
        configurer.Register(c => new GoogleCloudPubSubTransport(
            projectId,
            null,
            c.Get<IRebusLoggerFactory>(),
            c.Get<IAsyncTaskFactory>(),
            c.Get<IMessageConverter>(),
            settings));

        configurer.OtherService<IMessageConverter>().Register(c => new DefaultMessageConverter());
        
        return settings;
    }
}