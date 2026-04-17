using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.IntegrationTests;

/// <summary>
/// Tests for reply event handling in ControlPanel, including:
/// - RawReplyReceived event fires for all replies
/// - Exception isolation between typed and raw event handlers
/// - Typed event handlers continue working after RawReplyReceived subscriber throws
/// - RawReplyReceived fires even when typed event subscriber throws
/// </summary>
[TestFixture]
[Category("Integration")]
public class ReplyEventHandlingTests : IntegrationTestFixtureBase
{
    [Test]
    public async Task RawReplyReceived_FiresForCardDataReply()
    {
        // Arrange
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var rawReplyReceived = new TaskCompletionSource<ControlPanel.RawReplyEventArgs>();
        TargetPanel.RawReplyReceived += (_, e) =>
        {
            if (e.ReplyType == (byte)ReplyType.RawReaderData)
            {
                rawReplyReceived.TrySetResult(e);
            }
        };

        // Act
        var bits = new BitArray(26);
        bits[0] = true;
        var cardData = new RawCardData(0, FormatCode.NotSpecified, bits);
        TargetDevice.EnqueuePollReply(cardData);

        // Assert
        var result = await Task.WhenAny(rawReplyReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(rawReplyReceived.Task), "Timed out waiting for RawReplyReceived event");

        var args = await rawReplyReceived.Task;
        Assert.That(args.ConnectionId, Is.EqualTo(ConnectionId));
        Assert.That(args.ReplyType, Is.EqualTo((byte)ReplyType.RawReaderData));
        Assert.That(args.Payload.Length, Is.GreaterThan(0));
    }

    [Test]
    public async Task RawReplyReceived_FiresAfterTypedEvent()
    {
        // Arrange
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var eventOrder = new ConcurrentQueue<string>();
        var allReceived = new TaskCompletionSource<bool>();

        TargetPanel.RawCardDataReplyReceived += (_, _) =>
        {
            eventOrder.Enqueue("typed");
        };

        TargetPanel.RawReplyReceived += (_, e) =>
        {
            if (e.ReplyType == (byte)ReplyType.RawReaderData)
            {
                eventOrder.Enqueue("raw");
                allReceived.TrySetResult(true);
            }
        };

        // Act
        TargetDevice.EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, new BitArray(26)));

        // Assert
        var result = await Task.WhenAny(allReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(allReceived.Task), "Timed out waiting for events");

        Assert.That(eventOrder.Count, Is.EqualTo(2));
        eventOrder.TryDequeue(out var first);
        eventOrder.TryDequeue(out var second);
        Assert.That(first, Is.EqualTo("typed"));
        Assert.That(second, Is.EqualTo("raw"));
    }

    [Test]
    public async Task RawReplyReceived_StillFires_WhenTypedEventSubscriberThrows()
    {
        // Arrange
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var rawReplyReceived = new TaskCompletionSource<ControlPanel.RawReplyEventArgs>();

        TargetPanel.RawCardDataReplyReceived += (_, _) =>
        {
            throw new InvalidOperationException("Subscriber fault");
        };

        TargetPanel.RawReplyReceived += (_, e) =>
        {
            if (e.ReplyType == (byte)ReplyType.RawReaderData)
            {
                rawReplyReceived.TrySetResult(e);
            }
        };

        // Act
        TargetDevice.EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, new BitArray(26)));

        // Assert
        var result = await Task.WhenAny(rawReplyReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(rawReplyReceived.Task),
            "RawReplyReceived must fire even when typed event subscriber throws");
    }

    [Test]
    public async Task TypedEvents_ContinueWorking_AfterRawReplyReceivedSubscriberThrows()
    {
        // Arrange
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var typedRepliesReceived = new ConcurrentQueue<RawCardData>();
        var secondReceived = new TaskCompletionSource<bool>();

        TargetPanel.RawReplyReceived += (_, _) =>
        {
            throw new InvalidOperationException("Subscriber fault");
        };

        TargetPanel.RawCardDataReplyReceived += (_, e) =>
        {
            typedRepliesReceived.Enqueue(e.RawCardData);
            if (typedRepliesReceived.Count >= 2)
            {
                secondReceived.TrySetResult(true);
            }
        };

        // Act - send two card reads to verify the reply processing loop survives the first exception
        TargetDevice.EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, new BitArray(8)));
        TargetDevice.EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, new BitArray(16)));

        // Assert
        var result = await Task.WhenAny(secondReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(secondReceived.Task),
            "Typed events must continue working after RawReplyReceived subscriber throws");
        Assert.That(typedRepliesReceived.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task ReplyProcessingLoop_Survives_WhenTypedEventSubscriberThrows()
    {
        // Arrange
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var secondCardReceived = new TaskCompletionSource<RawCardData>();
        var throwOnFirst = true;

        TargetPanel.RawCardDataReplyReceived += (_, e) =>
        {
            if (throwOnFirst)
            {
                throwOnFirst = false;
                throw new InvalidOperationException("Subscriber fault on first card");
            }

            secondCardReceived.TrySetResult(e.RawCardData);
        };

        // Act - first card triggers the exception, second card should still be delivered
        TargetDevice.EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, new BitArray(8)));
        TargetDevice.EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, new BitArray(16)));

        // Assert
        var result = await Task.WhenAny(secondCardReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(secondCardReceived.Task),
            "Reply processing loop must survive a typed event subscriber exception");

        var card = await secondCardReceived.Task;
        Assert.That(card.BitCount, Is.EqualTo(16));
    }

    [Test]
    public async Task RawReplyReceived_PayloadIsReadOnly()
    {
        // Arrange
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var rawReplyReceived = new TaskCompletionSource<ControlPanel.RawReplyEventArgs>();
        TargetPanel.RawReplyReceived += (_, e) =>
        {
            if (e.ReplyType == (byte)ReplyType.RawReaderData)
            {
                rawReplyReceived.TrySetResult(e);
            }
        };

        // Act
        TargetDevice.EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, new BitArray(26)));

        // Assert
        var result = await Task.WhenAny(rawReplyReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(rawReplyReceived.Task));

        var args = await rawReplyReceived.Task;
        Assert.That(args.Payload.GetType(), Is.EqualTo(typeof(ReadOnlyMemory<byte>)),
            "Payload should be ReadOnlyMemory<byte> to prevent mutation");
    }
}
