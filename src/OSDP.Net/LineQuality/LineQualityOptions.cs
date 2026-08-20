using System;
using System.Collections.Generic;
using OSDP.Net.Tracing;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// Options controlling a line quality test run.
    /// </summary>
    public class LineQualityOptions
    {
        /// <summary>
        /// Gets or sets the test profile, which sets the iteration count and therefore the
        /// smallest loss rate the run can detect. Defaults to
        /// <see cref="TestProfile.Screening"/>.
        /// </summary>
        public TestProfile Profile { get; set; } = TestProfile.Screening;

        /// <summary>
        /// Gets or sets the baud rates to sweep. Defaults to
        /// <see cref="LineQualityProtocol.DefaultBaudRates"/>, the six rates OSDP names.
        /// </summary>
        public IReadOnlyList<int> BaudRates { get; set; }

        /// <summary>
        /// Gets or sets the responder address. Defaults to the dedicated test address 125.
        /// </summary>
        public byte Address { get; set; } = LineQualityProtocol.TestAddress;

        /// <summary>
        /// Gets or sets how long to wait for the first byte of a reply. Defaults to the 200 ms
        /// reply window from OSDP section 5.7.
        /// </summary>
        public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Gets or sets how long to wait after a baud rate change acknowledgment has drained
        /// before retuning the port. Defaults to the 100 ms the test procedure requires.
        /// </summary>
        public TimeSpan BaudRateSettleDelay { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Gets or sets whether to return the responder to 9600 baud when the run finishes.
        /// Defaults to <c>true</c>, so an interrupted installation is left in a known state.
        /// </summary>
        public bool ReturnToBaselineWhenDone { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional callback receiving progress updates.
        /// </summary>
        public IProgress<LineQualityProgress> Progress { get; set; }

        /// <summary>
        /// Gets or sets an optional tracer receiving every packet sent and received, for capture
        /// to an osdpcap file.
        /// </summary>
        public Action<TraceEntry> Tracer { get; set; }
    }

    /// <summary>
    /// A progress update from a running line quality test.
    /// </summary>
    public class LineQualityProgress
    {
        internal LineQualityProgress(string message, int baudRate, int packetsSentAtRate,
            int totalPacketsAtRate, int completedBaudRates, int totalBaudRates)
        {
            Message = message;
            BaudRate = baudRate;
            PacketsSentAtRate = packetsSentAtRate;
            TotalPacketsAtRate = totalPacketsAtRate;
            CompletedBaudRates = completedBaudRates;
            TotalBaudRates = totalBaudRates;
        }

        /// <summary>Gets a human-readable description of what the test is doing.</summary>
        public string Message { get; }

        /// <summary>Gets the baud rate currently being exercised.</summary>
        public int BaudRate { get; }

        /// <summary>Gets how many packets have been sent at the current rate.</summary>
        public int PacketsSentAtRate { get; }

        /// <summary>Gets how many packets the current rate will send in total.</summary>
        public int TotalPacketsAtRate { get; }

        /// <summary>Gets how many baud rates have been completed.</summary>
        public int CompletedBaudRates { get; }

        /// <summary>Gets how many baud rates the run will exercise in total.</summary>
        public int TotalBaudRates { get; }
    }
}
