using System;
using System.Collections.Generic;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Extended write (transparent mode) command data sent to a PD.
    /// </summary>
    /// <remarks>
    /// Transparent mode allows an ACU to communicate with an ISO 7816-4 smart card
    /// through a PD's reader by tunneling raw APDU bytes over the OSDP link. The PD
    /// acts as a bridge between the ACU and the smart card.
    /// </remarks>
    public class ExtendedWrite : CommandData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExtendedWrite"/> class.
        /// </summary>
        /// <param name="mode">Extended READ/WRITE mode (0 = configuration, 1 = transparent APDU).</param>
        /// <param name="pCommand">Command code whose meaning depends on <paramref name="mode"/>.</param>
        /// <param name="pData">Command-specific data.</param>
        /// <exception cref="ArgumentNullException">pData</exception>
        public ExtendedWrite(byte mode, byte pCommand, byte[] pData)
        {
            Mode = mode;
            PCommand = pCommand;
            PData = pData ?? throw new ArgumentNullException(nameof(pData));
        }

        /// <summary>
        /// Gets the extended READ/WRITE mode.
        /// </summary>
        public byte Mode { get; }

        /// <summary>
        /// Gets the mode-specific command code.
        /// </summary>
        public byte PCommand { get; }

        /// <summary>
        /// Gets the command-specific data.
        /// </summary>
        public byte[] PData { get; }

        /// <inheritdoc />
        public override CommandType CommandType => CommandType.ExtendedWrite;

        /// <inheritdoc />
        public override byte Code => (byte)CommandType;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new List<byte>(2 + PData.Length) { Mode, PCommand };
            data.AddRange(PData);
            return data.ToArray();
        }

        /// <summary>Parses the message payload bytes.</summary>
        /// <param name="data">Message payload as bytes.</param>
        /// <returns>An instance of <see cref="ExtendedWrite"/> representing the message payload.</returns>
        /// <remarks>
        /// Leniently handles short payloads so the PD does not NAK mid-handshake when an
        /// ACU sends a Mode-only or Mode+PCommand probe. Missing fields default to zero/empty.
        /// </remarks>
        public static ExtendedWrite ParseData(ReadOnlySpan<byte> data)
        {
            byte mode = data.Length > 0 ? data[0] : (byte)0;
            byte pCommand = data.Length > 1 ? data[1] : (byte)0;
            byte[] pData = data.Length > 2 ? data.Slice(2).ToArray() : [];
            return new ExtendedWrite(mode, pCommand, pData);
        }

        /// <summary>
        /// Queries the PD for its current extended mode setting.
        /// </summary>
        public static ExtendedWrite ReadModeSetting() =>
            new(0, 1, []);

        /// <summary>
        /// Configures Mode 0 (enable or disable transparent mode).
        /// </summary>
        /// <param name="enabled">Whether Mode 0 is enabled.</param>
        public static ExtendedWrite ModeZeroConfiguration(bool enabled) =>
            new(0, 2, [0, (byte)(enabled ? 1 : 0)]);

        /// <summary>
        /// Switches the PD to Mode 1 (transparent APDU).
        /// </summary>
        public static ExtendedWrite ModeOneConfiguration() =>
            new(0, 2, [1, 0]);

        /// <summary>
        /// Passes a raw APDU to the smart card through the specified reader.
        /// </summary>
        /// <param name="readerNumber">The reader number on the PD.</param>
        /// <param name="command">The raw APDU bytes to send to the smart card.</param>
        /// <exception cref="ArgumentNullException">command</exception>
        public static ExtendedWrite ModeOnePassAPDUCommand(byte readerNumber, byte[] command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            var data = new byte[command.Length + 1];
            data[0] = readerNumber;
            Array.Copy(command, 0, data, 1, command.Length);
            return new ExtendedWrite(1, 1, data);
        }

        /// <summary>
        /// Instructs the PD to terminate the smart card session on the specified reader.
        /// </summary>
        /// <param name="readerNumber">The reader number on the PD.</param>
        public static ExtendedWrite ModeOneTerminateSmartCardConnection(byte readerNumber) =>
            new(1, 2, [readerNumber]);

        /// <summary>
        /// Instructs the PD to scan for a smart card on the specified reader.
        /// </summary>
        /// <param name="readerNumber">The reader number on the PD.</param>
        public static ExtendedWrite ModeOneSmartCardScan(byte readerNumber) =>
            new(1, 4, [readerNumber]);

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"    Mode: {Mode}");
            sb.AppendLine($"PCommand: {PCommand}");
            sb.AppendLine($"   PData: {BitConverter.ToString(PData)}");
            return sb.ToString();
        }
    }
}
