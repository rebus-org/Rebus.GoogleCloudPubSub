using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Extensions;
using Rebus.GoogleCloudPubSub.Messages;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Tests.Contracts.Utilities;
using Rebus.Threading.TaskParallelLibrary;
using Rebus.Transport;
using Rebus.Workers.TplBased;

namespace Rebus.GoogleCloudPubSub.Tests;

[TestFixture]
public class GoogleCloudPubSubLeaseRenewalTest : GoogleCloudFixtureBase
{
    private const string QueueName = "your-subscription-name";

    readonly ConsoleLoggerFactory _consoleLoggerFactory = new(false);
    BuiltinHandlerActivator _activator;
    GoogleCloudPubSubTransport _transport;
    IBus _bus;
    IBusStarter _busStarter;

    protected override void SetUp()
    {
        _transport = new GoogleCloudPubSubTransport(
            ProjectId,
            QueueName,
            _consoleLoggerFactory,
            new TplAsyncTaskFactory(_consoleLoggerFactory),
            new DefaultMessageConverter()
        );

        Using(_transport);
        _transport.Initialize();
        AsyncHelpers.RunSync(() => _transport.PurgeQueueAsync());
        
        _activator = new BuiltinHandlerActivator();
        _busStarter = Configure.With(_activator)
            .Logging(l => l.Use(new ListLoggerFactory(outputToConsole: true, detailed: true)))
            .Transport(t => t.UsePubSub(ProjectId, QueueName))
            .Options(o =>
            {
                o.UseTplToReceiveMessages();
            })
            .Create();

        _bus = _busStarter.Bus;
        Using(_bus);
    }

    [Test]
    public async Task LeaseRenewalWorks()
    {
        var gotMessage = new ManualResetEvent(false);

        _activator.Handle<string>(async (bus, context, message) =>
        {
            Console.WriteLine($"Received message with ID {context.Headers.GetValue(Headers.MessageId)} - processing...");
            
            await Task.Delay(TimeSpan.FromMinutes(3));

            Console.WriteLine("Finished processing message.");
            gotMessage.Set();
        });

        _busStarter.Start();
        
        await _bus.SendLocal("Test message for lease renewal");
        
        Assert.IsTrue(gotMessage.WaitOne(TimeSpan.FromMinutes(3.5)), "Message was not processed in time.");
        
        _bus.Dispose();
        
        using var scope = new RebusTransactionScope();
        var message = await _transport.Receive(scope.TransactionContext, CancellationToken.None);
        await scope.CompleteAsync();

        Assert.IsNull(message, "Expected no message, but found one.");
    }
}
