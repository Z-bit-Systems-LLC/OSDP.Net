﻿using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Messages.ACU;
using OSDP.Net.Messages.PD;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;


namespace OSDP.Net;

public class Device : IDisposable
{
    private readonly ILogger _logger;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _listenerTask = Task.CompletedTask;
    private ConcurrentQueue<ReplyData> _pendingPollReplies = new ();


    public Device(ILogger<DeviceProxy> logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Dispose() => Dispose(true);

    public bool IsConnected { get; private set; } = false;

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellationTokenSource?.Dispose();
        }
    }

    public async void StartListening(IOsdpConnection connection)
    {
        var cancellationTokenSource = _cancellationTokenSource;
        if (cancellationTokenSource != null) return;
        _cancellationTokenSource = cancellationTokenSource = new CancellationTokenSource();

        _listenerTask = await Task.Factory.StartNew(async () =>
        {
            try
            {
                connection.Open();
                
                var channel = new PdMessageSecureChannel(connection);

                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    var command = await channel.ReadNextCommand(_cancellationTokenSource.Token);
                    if (command != null)
                    {
                        var reply = HandleCommand(command);
                        channel.SendReply(reply);
                    }
                }

                IsConnected = false;
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, $"Unexpected exception in polling loop");
            }
        }, TaskCreationOptions.LongRunning);
    }

    public async void StopListening()
    {
        var cancellationTokenSource = _cancellationTokenSource;
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();

            // TODO: why not block indefinitely?
            //_shutdownComplete.WaitOne(TimeSpan.FromSeconds(1));
            await _listenerTask;
            _cancellationTokenSource = null;
        }
    }

    public void EnqueuePollReply(ReplyData reply) => _pendingPollReplies.Enqueue(reply);

    protected virtual Messages.PD.Reply HandleCommand(IncomingMessage command) => 
        new(command, (CommandType)command.Type switch
        {
            CommandType.Poll => HandlePoll(),
            CommandType.IdReport => HandleIdReport(),
            CommandType.TextOutput => HandleTextOutput(command),
            CommandType.BuzzerControl => HandleBuzzerControl(command),
            CommandType.OutputControl => HandleOutputControl(command),
            CommandType.DeviceCapabilities => HandleDeviceCap(command),
            CommandType.PivData => HandlePivData(command),
            CommandType.ManufacturerSpecific => HandleManufacturerCommand(command),
            _ => HandleUnknownCommand(command),
        });

    protected virtual ReplyData HandlePoll()
    {
        if (_pendingPollReplies.TryDequeue(out var reply)) return reply;
        return new Ack();
    }

    protected virtual ReplyData HandleIdReport()
    {
        return new DeviceIdentification([ 0x01, 0x02, 0x03 ], 0x04, 0x05, 0x06070809, 0x0a, 0x0b, 0x0c);
    }

    protected virtual ReplyData HandleTextOutput(IncomingMessage command)
    {
        return new Ack();
    }

    protected virtual ReplyData HandleBuzzerControl(IncomingMessage command)
    {
        return new Ack();
    }

    protected virtual ReplyData HandleOutputControl(IncomingMessage command)
    {
        return new Ack();
    }

    protected virtual ReplyData HandleDeviceCap(IncomingMessage command)
    {
        return new Ack();
    }

    protected virtual ReplyData HandlePivData(IncomingMessage command)
    {
        var payload = GetPIVData.ParseData(command.Payload);

        // TODO: This is where we would trigger async gathering of PIV data which
        // will be returned through a reply to a future poll command

        return new Ack();
    }
    protected virtual ReplyData HandleManufacturerCommand(IncomingMessage command)
    {
        var payload = Model.CommandData.ManufacturerSpecific.ParseData(command.Payload);

        // TODO: This is where we would trigger async manufacturer-specific command
        // reply to which would be returned as a reply to a future poll command

        return new Ack();
    }

    protected virtual ReplyData HandleUnknownCommand(IncomingMessage command)
    {
        byte[] rawMessage = command.OriginalMessageData.ToArray();

        _logger.LogInformation($"Unexpected Command: {{cmd_bytes}}!!{Environment.NewLine}" +
            $"    Cmd: {{cmd_code}}({{cmd_name}}){Environment.NewLine}" +
            $"    Payload: {{payload}}",
            string.Join("-", rawMessage.Select(x => x.ToString("X2"))),
            command.Type, Enum.GetName(typeof(CommandType), command.Type),
            string.Join("-", command.Payload.Select(x => x.ToString("X2"))));

        return new Nak(ErrorCode.UnknownCommandCode);
    }
}
