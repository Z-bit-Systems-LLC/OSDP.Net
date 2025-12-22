using OSDP.Net.Connections;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.Tracing;
using System;
using System.Text;
using System.Threading;

namespace OSDP.Net.PanelCommands
{
    namespace DeviceDiscover
    {
        /// <summary>
        /// Represents an error condition encountered during device discovery
        /// </summary>
        public class DeviceDiscoveryException : OSDPNetException
        {
            /// <summary>
            /// Initializes a new instance of DeviceDiscoveryException
            /// </summary>
            /// <param name="message"></param>
            public DeviceDiscoveryException(string message) : base(message) { }
        }

        /// <summary>
        /// This exception will be thrown if a caller attempts to perform device discovery on a
        /// control panel that already has open connections. Having open connections is 
        /// disallowed because the discovery itself will have to open/close the connections on the
        /// serial ports being tested and there cannot be more than one open handle to the same port
        /// </summary>
        public class ControlPanelInUseException : DeviceDiscoveryException
        {
            /// <summary>
            /// Instantiates a new instance of ControlPanelInUseException
            /// </summary>
            public ControlPanelInUseException() 
                : base("To perform device discovery control panel cannot have any open connections") { }
        }

        /// <summary>
        /// Represents current state of the device discovery process. As the device discovery
        /// unfolds, this status value indicates which fields of the <see cref="DiscoveryResult"/>
        /// instance have been filled in.
        /// </summary>
        public enum DiscoveryStatus
        {
            /// <summary>
            /// Device discovery has been started
            /// </summary>
            Started,

            /// <summary>
            /// About to send an osdp_POLL to configuration address (0x7f) to see if a device
            /// will respond on a connection instance being tested
            /// </summary>
            LookingForDeviceOnConnection,

            /// <summary>
            /// Received a valid reply too osdp_POLL indicating that the right connection
            /// has been found
            /// </summary>
            ConnectionWithDeviceFound,

            /// <summary>
            /// About to send an osdp_PDID to a specific address to see if a device will
            /// respond and if it does to get its identification report
            /// </summary>
            LookingForDeviceAtAddress,

            /// <summary>
            /// Received a valid reply to osdp_PDID command, device has been found and
            /// its address determined
            /// </summary>
            DeviceIdentified,

            /// <summary>
            /// Received a valid reply to osdp_CAM command
            /// </summary>
            CapabilitiesDiscovered,

            /// <summary>
            /// Device discovery completed successfully
            /// </summary>
            Succeeded,

            /// <summary>
            /// No devices have been found
            /// </summary>
            DeviceNotFound,

            /// <summary>
            /// Something unexpected has gone wrong during the device discovery process
            /// </summary>
            Error,

            /// <summary>
            /// Device discovery process terminated due to a cancellation request
            /// </summary>
            Cancelled
        }

        /// <summary>
        /// Callback used for reporting device discovery status updates. 
        /// </summary>
        /// <param name="current">Discover results instance. This object will get incrementally
        /// filled in as the device discovery progresses. Check status field to determine
        /// which fields have valid values in them.</param>
        public delegate void DiscoveryProgress(DiscoveryResult current);

        /// <summary>
        /// Defines a set of optional parameters the caller can pass in to the
        /// <see cref="ControlPanel.DiscoverDevice(System.Collections.Generic.IEnumerable{IOsdpConnection}, DiscoveryOptions)"/>
        /// call.
        /// </summary>
        public class DiscoveryOptions
        {
            /// <summary>
            /// Callback to invoke when there is a status update
            /// </summary>
            public DiscoveryProgress ProgressCallback { get; set; }

            /// <summary>
            /// Maximum time to wait for a poll command to return. This is important here since if a
            /// connection doesn't have a device or a poll is sent to an unknown address, there will
            /// be no reply. Therefore, this timeout interval determines the maximum time of the
            /// device discovery
            /// </summary>
            public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

            /// <summary>
            /// When discovery is enumerating multiple possible connections, this option specifies the 
            /// time interval to wait between the close of a connection and opening of a subsequent one.
            /// This might be necessary when testing multiple baud rates on a single serial COM port
            /// as the port might still be marked "in use" right after we release our handle to it.
            /// </summary>
            public TimeSpan ReconnectDelay { get; set; } = TimeSpan.Zero;

            /// <summary>
            /// Tracer instance to use to report network commands/replies as issued by the 
            /// device discovery
            /// </summary>
            public Action<TraceEntry> Tracer { get; set; }

            /// <summary>
            /// Cancellation token to listen to if the caller wishes to be able to interrupt
            /// device discovery
            /// </summary>
            public CancellationToken CancellationToken { get; set; }

            /// <summary>
            /// The library has a default tracer that will always write to [connectionId].osdpcap
            /// file in the current working directory of the calling process. Call this method to
            /// set <see cref="Tracer"/> property to this default instance.
            /// </summary>
            /// <param name="setDefault">Flag that indicates whether or not a default tracer is to
            /// be used. From an API perspective, this seems a bit silly to have, but it's here
            /// because it made calling code slightly more conscise</param>
            /// <returns></returns>
            public DiscoveryOptions WithDefaultTracer(bool setDefault=true)
            {
                if (setDefault) Tracer = OSDPFileCaptureTracer.Trace;
                return this;
            }
        }

        /// <summary>
        /// Contains the results of the device discovery. When <see cref="DiscoveryOptions.ProgressCallback"/> is
        /// used, this instance will get incrementally filled in based on current value of the <see cref="Status"/> field
        /// </summary>
        public class DiscoveryResult
        {
            /// <summary>
            /// Determines current state of the device discovery
            /// </summary>
            public DiscoveryStatus Status { get; internal set; }

            /// <summary>
            /// When Status is <see cref="DiscoveryStatus.LookingForDeviceOnConnection"/> this property identifies
            /// connection being tested even if ultimately no device will be discovered on it. After 
            /// <see cref="DiscoveryStatus.ConnectionWithDeviceFound"/> status, this property identifies
            /// connection with a valid device
            /// </summary>
            public IOsdpConnection Connection { get; internal set; }

            /// <summary>
            /// When Status is <see cref="DiscoveryStatus.LookingForDeviceAtAddress"/>, this property
            /// identifies the address being polled to see if there is an available device. After
            /// <see cref="DiscoveryStatus.DeviceIdentified"/> status, this property identifies the
            /// address where a device was found
            /// </summary>
            public byte Address { get; internal set; }

            /// <summary>
            /// After status <see cref="DiscoveryStatus.DeviceIdentified"/>, this property will contain
            /// the identification report from the device
            /// </summary>
            public DeviceIdentification Id { get; internal set; }

            /// <summary>
            /// After status <see cref="DiscoveryStatus.CapabilitiesDiscovered"/>, this property will contain
            /// the capabilities report from the device
            /// </summary>
            public DeviceCapabilities Capabilities { get; internal set; }

            /// <summary>
            /// A flag that indicates whether or not discovered device responds to the default security key
            /// The security key will only be tested if a) PD returns a "Communication Security" capability and
            /// b) that capability indicates that it supports AES128. Otherwise, this value will remain false.
            /// </summary>
            public bool UsesDefaultSecurityKey { get; set; }

            /// <summary>
            /// When status is set to <see cref="DiscoveryStatus.Error"/> or <see cref="DiscoveryStatus.Cancelled"/>
            /// this property will be set to the exception instance that terminated the device discovery
            /// </summary>
            public Exception Error { get; internal set; }

            /// <inheritdoc/>
            public override string ToString()
            {
                var sb = new StringBuilder();

                sb.AppendLine($"    Baud Rate: {Connection.BaudRate}");
                sb.AppendLine($"      Address: {Address}");
                sb.AppendLine("Identification:");
                sb.Append("        ");
                sb.AppendLine(Id?.ToString()?.TrimEnd().Replace("\n", "\n        "));
                sb.AppendLine("  Capabilities:");
                sb.Append("        ");
                sb.AppendLine(Capabilities.ToString().TrimEnd().Replace("\n", "\n        "));
                sb.AppendLine($"Responds to Default Security Key: {(UsesDefaultSecurityKey ? "Yes" : "No")}");

                return sb.ToString();
            }
        }
    }
}
