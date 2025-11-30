using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using System;
using System.Linq;
using System.Text;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// A input status report reply as defined in OSDP v2.2.2 specification.
    /// </summary>
    public class InputStatus : PayloadData
    {
        /// <summary>
        /// Initializes a new instance of InputStatus class
        /// </summary>
        /// <param name="statuses">Array of input status values ordered by input number.</param>
        public InputStatus(InputStatusValue[] statuses)
        {
            InputStatuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
        }

        /// <summary>
        /// Initializes a new instance of InputStatus class with boolean values for backward compatibility.
        /// </summary>
        /// <param name="statuses">Array of boolean status values where true=Active, false=Inactive.</param>
        [Obsolete("Use the constructor accepting InputStatusValue[] instead. This constructor will be removed in a future version.")]
        public InputStatus(bool[] statuses)
        {
            if (statuses == null) throw new ArgumentNullException(nameof(statuses));
            InputStatuses = statuses.Select(x => x ? InputStatusValue.Active : InputStatusValue.Inactive).ToArray();
        }

        /// <inheritdoc />
        public override byte Code => (byte)ReplyType.InputStatusReport;

        /// <summary>
        /// Gets all the PD's input statuses as an array ordered by input number.
        /// Each value represents the status as defined in Table 52 of the OSDP v2.2.2 specification.
        /// </summary>
        public InputStatusValue[] InputStatuses { get; }

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.ReplyMessageWithDataSecurity;

        /// <summary>
        /// Parses the data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>A input status report reply.</returns>
        internal static InputStatus ParseData(ReadOnlySpan<byte> data)
        {
            return new InputStatus(data.ToArray().Select(b => (InputStatusValue)b).ToArray());
        }

        /// <inheritdoc />
        public override byte[] BuildData() => InputStatuses.Select(x => (byte)x).ToArray();

        /// <inheritdoc />
        public override string ToString()
        {
            byte inputNumber = 0;
            var build = new StringBuilder();
            foreach (InputStatusValue inputStatus in InputStatuses)
            {
                build.AppendLine($"Input Number {inputNumber++:00}: {inputStatus}");
            }

            return build.ToString();
        }
    }
}