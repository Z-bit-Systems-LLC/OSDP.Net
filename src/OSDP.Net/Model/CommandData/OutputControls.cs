using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Command data to control the outputs of a PD.
    /// </summary>
    public class OutputControls : CommandData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OutputControls"/> class.
        /// </summary>
        /// <param name="controls">The output controls to be updated. It can't be null and requires at least one in the collection.</param>
        public OutputControls(IEnumerable<OutputControl> controls)
        {
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            
            Controls = controls.ToArray();
            
            if (!Controls.Any())
            {
                throw new Exception("Requires at least one output control");
            }
        }

        /// <summary>
        /// One or more commands to control the outputs of a PD.
        /// </summary>
        public IEnumerable<OutputControl> Controls { get; }
        
        /// <inheritdoc />
        public override CommandType CommandType => CommandType.OutputControl;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte Code => (byte)CommandType;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new List<byte>();
            foreach (var outputControl in Controls)
            {
                data.AddRange(outputControl.BuildData());
            }

            return data.ToArray();
        }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="payloadData">Message payload as bytes</param>
        /// <returns>An instance of OutputControls representing the message payload</returns>
        public static OutputControls ParseData(ReadOnlySpan<byte> payloadData)
        {
            return new OutputControls(SplitData(4, data => OutputControl.ParseData(data), payloadData));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Output Count: {Controls.Count()}");
            var index = 1;
            foreach (var control in Controls)
            {
                sb.AppendLine($"Output #{index}:");
                sb.AppendLine($" Output #: {control.OutputNumber}");
                sb.AppendLine($"     Code: {control.OutputControlCode}");
                sb.AppendLine($"     Time: {control.Timer} (100ms units)");
                index++;
            }
            return sb.ToString();
        }
    }
}