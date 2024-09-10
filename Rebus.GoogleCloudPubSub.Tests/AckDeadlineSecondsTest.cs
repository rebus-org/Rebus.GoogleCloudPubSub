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
using Rebus.Threading.TaskParallelLibrary;
using Rebus.Transport;

namespace Rebus.GoogleCloudPubSub.Tests;

[TestFixture]
public class AckDeadlineSecondsTest : GoogleCloudFixtureBase
{
    private const string QueueName = "test-topic:test-subscription";
    private const int MaxAckDeadlineSeconds = 3;
    private readonly ConsoleLoggerFactory _consoleLoggerFactory = new(false);
    private BuiltinHandlerActivator _activator;
    private GoogleCloudPubSubTransport _transport;
    private IBus _bus;
    private IBusStarter _busStarter;

    private void SetUp(int maxParallelism)
    {
        _transport = new GoogleCloudPubSubTransport(ProjectId, QueueName, _consoleLoggerFactory,
            new TplAsyncTaskFactory(_consoleLoggerFactory), new DefaultMessageConverter(),
            new GoogleCloudPubSubTransportSettings());
        Using(_transport);
        _transport.Initialize();
        AsyncHelpers.RunSync(() => _transport.PurgeQueueAsync());
        _activator = new BuiltinHandlerActivator();
        _busStarter = Configure.With(_activator).Logging(l => l.Use(_consoleLoggerFactory)).Transport(t =>
            t.UsePubSub(ProjectId, QueueName).SetAckDeadlineSeconds(MaxAckDeadlineSeconds)).Options(o =>
        {
            o.SetNumberOfWorkers(1);
            o.SetMaxParallelism(2);
        }).Create();
        _bus = _busStarter.Bus;
        Using(_bus);
    }

    [Test]
    public async Task ProcessMessageWithinAckDeadline()
    {
        SetUp(1);
        var gotMessage = new ManualResetEvent(false);
        
        _activator.Handle<string>(async (bus, context, message) =>
        {
            Console.WriteLine(
                $"Received message with ID {context.Headers.GetValue(Headers.MessageId)} - processing...");
            await Task.Delay(TimeSpan.FromSeconds(MaxAckDeadlineSeconds * 0.1));
            Console.WriteLine("Finished processing message.");
            gotMessage.Set();
        });
        
        _busStarter.Start();
        
        await _bus.SendLocal("Test message for AckDeadline");
        
        Assert.IsTrue(gotMessage.WaitOne(TimeSpan.FromSeconds(MaxAckDeadlineSeconds)),
            "Message was processed in time.");
        
        _bus.Dispose();
        
        using var scope = new RebusTransactionScope();
        var message = await _transport.Receive(scope.TransactionContext, CancellationToken.None);
        await scope.CompleteAsync();
        
        Assert.IsNull(message, "Expected no message, but found one.");
    }

    [Test]
    public async Task AckDeadlineExpiresRedeliverMessage()
    {
        SetUp(2);
        var firstAttemptProcessed = new ManualResetEvent(false);
        var secondAttemptProcessed = new ManualResetEvent(false);
        var attemptCount = 0;
        const int totalAttempts = 2;
        _activator.Handle<string>(async (bus, context, message) =>
        {
            attemptCount++;
            Console.WriteLine(
                $"Received message with ID {context.Headers.GetValue(Headers.MessageId)} - processing attempt {attemptCount}...");
            if (attemptCount == 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(MaxAckDeadlineSeconds * 3));
                Console.WriteLine("First attempt: Message Processing took too long, ack deadline expired.");
                firstAttemptProcessed.Set();
            }
            else
            {
                Console.WriteLine("Second attempt: Message redelivered and processed.");
                secondAttemptProcessed.Set();
            }
        });
        
        _busStarter.Start();
        
        await _bus.SendLocal("Test message for AckDeadlineExpiration");
        
        Assert.IsTrue(secondAttemptProcessed.WaitOne(TimeSpan.FromSeconds(MaxAckDeadlineSeconds * totalAttempts)),
            "Second attempt, the message was redelivered and did not process in time.");
        
        Assert.IsFalse(firstAttemptProcessed.WaitOne(TimeSpan.FromSeconds(MaxAckDeadlineSeconds)),
            "First attempt did not process in time.");
        
        _bus.Dispose();
    }
}