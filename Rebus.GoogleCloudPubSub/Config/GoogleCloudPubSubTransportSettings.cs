namespace Rebus.Config;

public class GoogleCloudPubSubTransportSettings
{
    internal bool AutomaticLeaseRenewalEnabled { get; private set; }
    internal bool SkipResourceCreationEnabled { get; private set; }
    internal int AckDeadlineSeconds { get; private set; } = 30;


    /// <summary>
    /// Skips resource creation (topics and subscriptions). Can be used when the service does not have rights 
    /// to create resources, or when it is expected that resources are pre-provisioned.
    /// </summary>
    public GoogleCloudPubSubTransportSettings SkipResourceCreation()
    {
        SkipResourceCreationEnabled = true;
        return this;
    }
    
    /// <summary>
    /// Sets the acknowledgment deadline in seconds. This defines the time within which a message
    /// must be acknowledged before it is redelivered.
    /// </summary>
    public GoogleCloudPubSubTransportSettings SetAckDeadlineSeconds(int seconds)
    {
        AckDeadlineSeconds = seconds;
        return this;
    }
    
    /// <summary>
    /// Enables automatic lease renewal, which attempts to renew the lease on messages being processed before
    /// they expire. This is useful for handling messages that require longer processing times.
    /// 
    /// Please note that lease renewal can have performance implications, so it should be used with caution. 
    /// It is disabled by default to prioritize performance in typical scenarios where message processing 
    /// times are expected to be short.
    /// 
    /// If you enable this option, ensure that you truly need it for long-running operations.
    /// </summary>
    public GoogleCloudPubSubTransportSettings EnableAutomaticLeaseRenewal()
    {
        AutomaticLeaseRenewalEnabled = true;
        return this;
    }
}