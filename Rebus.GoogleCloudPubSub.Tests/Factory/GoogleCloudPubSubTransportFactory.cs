﻿using System.Collections.Concurrent;
using Rebus.Config;
using Rebus.GoogleCloudPubSub.Messages;
using Rebus.Logging;
using Rebus.Tests.Contracts.Transports;
using Rebus.Threading.TaskParallelLibrary;
using Rebus.Transport;

namespace Rebus.GoogleCloudPubSub.Tests.Factory;

public class GoogleCloudPubSubTransportFactory : ITransportFactory
{
    private readonly ConcurrentStack<GoogleCloudPubSubTransport> _disposables = new();
    private static string ProjectId => GoogleCredentials.GetProjectIdFromGoogleCredentials();

    public ITransport CreateOneWayClient()
    {
        var consoleLoggerFactory = new ConsoleLoggerFactory(false);
        var transport = new GoogleCloudPubSubTransport(ProjectId, Constants.Receiver,
            consoleLoggerFactory,
            new TplAsyncTaskFactory(consoleLoggerFactory), new DefaultMessageConverter(),
            new GoogleCloudPubSubTransportSettings().SetAckDeadlineSeconds(600));

        _disposables.Push(transport);

        return transport;
    }

    public ITransport Create(string inputQueue)
    {
        var consoleLoggerFactory = new ConsoleLoggerFactory(false);
        var transport = new GoogleCloudPubSubTransport(ProjectId, inputQueue, consoleLoggerFactory,
            new TplAsyncTaskFactory(consoleLoggerFactory), new DefaultMessageConverter(),
            new GoogleCloudPubSubTransportSettings().SetAckDeadlineSeconds(600));
        AsyncHelpers.RunSync(transport.PurgeQueueAsync);
        transport.Initialize();

        _disposables.Push(transport);

        return transport;
    }

    public void CleanUp()
    {
        while (_disposables.TryPop(out var disposable))
        {
            disposable.PurgeQueueAsync().ConfigureAwait(false);
            disposable.Dispose();
        }
    }
}