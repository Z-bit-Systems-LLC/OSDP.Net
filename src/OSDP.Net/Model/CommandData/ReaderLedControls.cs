using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Command data to control the color of LEDs.
    /// </summary>
    public class ReaderLedControls : CommandData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReaderLedControls"/> class.
        /// </summary>
        /// <param name="controls">The reader LED controls to be updated. It can't be null and requires at least one in the collection.</param>
        public ReaderLedControls(IEnumerable<ReaderLedControl> controls)
        {
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            
            Controls = controls.ToArray();
            
            if (!Controls.Any())
            {
                throw new Exception("Requires at least one output control");
            }
        }

        /// <summary>
        /// One or more commands to control the LEDs of a PD.
        /// </summary>
        public IEnumerable<ReaderLedControl> Controls { get; }

        /// <inheritdoc />
        public override CommandType CommandType => CommandType.LEDControl;
        
        /// <inheritdoc />
        public override byte Code => (byte)CommandType;
        
        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock()
        {
            return SecurityBlock.CommandMessageWithDataSecurity;
        }

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new List<byte>();
            foreach (var readerLedControl in Controls)
            {
                data.AddRange(readerLedControl.BuildData());
            }

            return data.ToArray();
        }
        
        /// <summary>Parses the message payload bytes</summary>
        /// <param name="payloadData">Message payload as bytes</param>
        /// <returns>An instance of ReaderLedControls representing the message payload</returns>
        public static ReaderLedControls ParseData(ReadOnlySpan<byte> payloadData)
        {
            return new ReaderLedControls(SplitData(14, data => ReaderLedControl.ParseData(data), payloadData));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"LED Count: {Controls.Count()}");
            var index = 1;
            foreach (var control in Controls)
            {
                sb.AppendLine($"LED #{index}:");
                sb.AppendLine($" Reader #: {control.ReaderNumber}");
                sb.AppendLine($"    LED #: {control.LedNumber}");
                sb.AppendLine($"Temp Mode: {control.TemporaryMode}");
                sb.AppendLine($"  On Time: {control.TemporaryOnTime} (100ms)");
                sb.AppendLine($" Off Time: {control.TemporaryOffTime} (100ms)");
                sb.AppendLine($" On Color: {control.TemporaryOnColor}");
                sb.AppendLine($"Off Color: {control.TemporaryOffColor}");
                sb.AppendLine($"    Timer: {control.TemporaryTimer} (100ms)");
                index++;
            }
            return sb.ToString();
        }
    }
}