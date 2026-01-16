using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

[assembly: InternalsVisibleTo("OSDP.Net.Tests")]
namespace OSDP.Net;

internal class DeviceProxy : IComparable<DeviceProxy>
{
    private const int RetryAmount = 2;

    private readonly ConcurrentQueue<CommandData> _commands = new();
    private readonly bool _useSecureChannel;
    
    private int _counter = RetryAmount;
    private DateTime _lastValidReply = DateTime.MinValue;
    private CommandData _retryCommand;

    /// <summary>
    /// Represents a device with a specific address.
    /// </summary>
    /// <param name="address">The address of the device.</param>
    /// <param name="useCrc">Specifies whether to use CRC (Cyclic Redundancy Check) for message validation.</param>
    /// <param name="useSecureChannel">Specifies whether to use a secure channel for communication.</param>
    /// <param name="secureChannelKey">The key used for securing the communication channel.</param>
    public DeviceProxy(byte address, bool useCrc, bool useSecureChannel, byte[] secureChannelKey = null)
    {
        _useSecureChannel = useSecureChannel;

        Address = address;
        MessageControl = new Control(0, useCrc, useSecureChannel);

        if (UseSecureChannel)
        {
            SecureChannelKey = secureChannelKey ?? SecurityContext.DefaultKey;
            
            IsDefaultKey = SecurityContext.DefaultKey.SequenceEqual(SecureChannelKey);

            MessageSecureChannel = new ACUMessageSecureChannel(new SecurityContext(SecureChannelKey));
        }
        else
        {
            MessageSecureChannel = new ACUMessageSecureChannel(new SecurityContext());
        }
    }

    internal byte[] SecureChannelKey { get; }

    private bool IsDefaultKey { get; }

    public byte Address { get; }

    internal Control MessageControl { get; }

    public bool UseSecureChannel => !IsSendingMultiMessageNoSecureChannel && _useSecureChannel;

    public bool IsSecurityEstablished => !IsSendingMultiMessageNoSecureChannel && MessageControl.HasSecurityControlBlock && MessageSecureChannel.IsSecurityEstablished;

    public bool IsConnected => _lastValidReply + TimeSpan.FromSeconds(8) >= DateTime.UtcNow &&
                               (IsSendingMultiMessageNoSecureChannel || !MessageControl.HasSecurityControlBlock || IsSecurityEstablished);
    
    public IMessageSecureChannel MessageSecureChannel { get; }

    internal bool IsSendingMultipartMessage { get; set; }

    internal DateTime RequestDelay { get; set; }

    internal bool IsSendingMultiMessageNoSecureChannel { get; set; }
        
    internal bool IsReceivingMultipartMessage { get; set; }
        
    /// <summary>
    /// Has one or more commands waiting in the queue
    /// </summary>
    internal bool HasQueuedCommand => _commands.Any();
    
    /// <inheritdoc />
    public int CompareTo(DeviceProxy other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return Address.CompareTo(other.Address);
    }

    /// <summary>
    /// Get the next command in the queue, setup security, or send a poll command
    /// </summary>
    /// <param name="isPolling">If false, only send commands in the queue</param>
    /// <returns>The next command always if polling, could be null if not polling</returns>
    internal virtual OutgoingMessage GetNextCommandData(bool isPolling)
    {
        if (_retryCommand != null)
        {
            var saveCommand = _retryCommand;
            _retryCommand = null;
            return new OutgoingMessage(Address, MessageControl, saveCommand);
        }

        if (isPolling)
        {
            // Don't send clear text polling if using secure channel
            if (MessageControl.Sequence == 0 && !UseSecureChannel)
            {
                return new OutgoingMessage(Address, MessageControl, new NoPayloadCommandData(CommandType.Poll));
            }

            if (UseSecureChannel && !MessageSecureChannel.IsInitialized)
            {
                return new OutgoingMessage(Address, MessageControl,
                    new SecurityInitialization(MessageSecureChannel.ServerRandomNumber, IsDefaultKey));
            }

            if (UseSecureChannel && !MessageSecureChannel.IsSecurityEstablished)
            {
                return new OutgoingMessage(Address, MessageControl,
                    new ServerCryptogramData(MessageSecureChannel.ServerCryptogram, IsDefaultKey));
            }
        }

        if (!_commands.TryDequeue(out var command) && isPolling)
        {
            return new OutgoingMessage(Address, MessageControl, new NoPayloadCommandData(CommandType.Poll));
        }

        return command != null
            ? new OutgoingMessage(Address, MessageControl, command)
            : new OutgoingMessage(Address, MessageControl, new NoPayloadCommandData(CommandType.Poll));
    }

    internal void SendCommand(CommandData command)
    {
        _commands.Enqueue(command);
    }

    /// <summary>
    /// Store command for retry
    /// </summary>
    /// <param name="command"></param>
    internal void RetryCommand(CommandData command)
    {
        if (_counter-- > 0)
        {
            _retryCommand = command;
        }
        else
        {
            _retryCommand = null;
            _counter = RetryAmount;
        }
    }

    internal void ValidReplyHasBeenReceived(byte sequence)
    {
        MessageControl.IncrementSequence(sequence);
            
        // It's valid once sequences are above zero
        if (sequence > 0) _lastValidReply = DateTime.UtcNow;
            
        // Reset retry counter
        _counter = RetryAmount;
    }

    internal void InitializeSecureChannel(byte[] payload, byte[] secureBlockData)
    {
        // Validate payload: must be 32 bytes (cUID[8] + clientRnd[8] + clientCryptogram[16])
        if (payload == null || payload.Length < 32)
        {
            throw new InvalidPayloadException(
                $"osdp_CCRYPT payload must be 32 bytes, received {payload?.Length ?? 0}");
        }

        // Validate SCB key type: secureBlockData[0] indicates SCBK-D (0x00) or SCBK (0x01)
        // This must match the key type the ACU sent in osdp_CHLNG
        if (secureBlockData == null || secureBlockData.Length == 0)
        {
            throw new InvalidPayloadException(
                "osdp_CCRYPT response missing security control block data for key type validation");
        }

        bool pdClaimsDefaultKey = secureBlockData[0] == 0x00;
        if (IsDefaultKey != pdClaimsDefaultKey)
        {
            throw new SecureChannelKeyTypeMismatchException(IsDefaultKey, pdClaimsDefaultKey);
        }

        MessageSecureChannel.InitializeACU(payload.Skip(8).Take(8).ToArray(),
            payload.Skip(16).Take(16).ToArray());
    }
    
    internal void ResetSecureChannelSession()
    {
        MessageSecureChannel.ResetSecureChannelSession();
    }

    internal ReadOnlySpan<byte> GenerateMac(ReadOnlySpan<byte> message, bool isIncoming)
    {
        return MessageSecureChannel.GenerateMac(message, isIncoming);
    }
}

internal interface IDeviceProxyFactory
{
    DeviceProxy Create(byte address, bool useCrc, bool useSecureChannel, byte[] secureChannelKey = null);
}

internal class DeviceProxyFactory : IDeviceProxyFactory
{
    public DeviceProxy Create(
        byte address, bool useCrc, bool useSecureChannel, byte[] secureChannelKey = null)
        => new DeviceProxy(address, useCrc, useSecureChannel, secureChannelKey);
}
