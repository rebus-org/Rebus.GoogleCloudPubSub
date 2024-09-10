using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.GoogleCloudPubSub.Messages;
using Rebus.Logging;
using Rebus.Tests.Contracts.Transports;
using Rebus.Threading.TaskParallelLibrary;

namespace Rebus.GoogleCloudPubSub.Tests.Factory;

public class GoogleCloudPubSubBusFactory : IBusFactory
{
    private readonly string _projectId = GoogleCredentials.GetProjectIdFromGoogleCredentials();
    private readonly List<IDisposable> _stuffToDispose = new();

    public IBus GetBus<TMessage>(string inputQueueAddress, Func<TMessage, Task> handler)
    {
        var builtinHandlerActivator = new BuiltinHandlerActivator();

        builtinHandlerActivator.Handle(handler);

        PurgeQueue(inputQueueAddress);

        var bus = Configure.With(builtinHandlerActivator)
            .Transport(t => t.UsePubSub(_projectId, inputQueueAddress))
            .Options(o =>
            {
                o.SetNumberOfWorkers(10);
                o.SetMaxParallelism(10);
            })
            .Start();

        _stuffToDispose.Add(bus);

        return bus;
    }

    public void Cleanup()
    {
        _stuffToDispose.ForEach(d => d.Dispose());
        _stuffToDispose.Clear();
    }

    private void PurgeQueue(string queueName)
    {
        var consoleLoggerFactory = new ConsoleLoggerFactory(false);

        using var transport = new GoogleCloudPubSubTransport(_projectId, queueName, consoleLoggerFactory,
            new TplAsyncTaskFactory(consoleLoggerFactory), new DefaultMessageConverter());

        transport.PurgeQueueAsync()
            .GetAwaiter()
            .GetResult();
    }
}