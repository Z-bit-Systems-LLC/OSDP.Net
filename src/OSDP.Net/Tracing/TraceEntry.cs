using System;

namespace OSDP.Net.Tracing;

/// <summary>
/// Direction of trace.
/// </summary>
public enum TraceDirection
{
    /// <summary>Data is sent to the device</summary>
    Input,
    /// <summary>Data is sent from the device</summary>
    Output,
    /// <summary>Data is being monitored</summary>
    Trace
}

/// <summary>
/// Represents low level data sent on the wire between the control panel and the devices.
/// </summary>
public struct TraceEntry
{
    /// <summary>
    /// The direction in which the data is sent.
    /// </summary>
    public TraceDirection Direction { get; }

    /// <summary>
    /// The connection sending/receiving the data.
    /// </summary>
    public Guid ConnectionId { get; }

    /// <summary>
    /// The data that is sent/received
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Initializes a new instance of the TraceEntry struct.
    /// </summary>
    /// <param name="direction">The direction in which the data is sent.</param>
    /// <param name="connectionId">The connection sending/receiving the data.</param>
    /// <param name="data">The data that is sent/received.</param>
    public TraceEntry(TraceDirection direction, Guid connectionId, byte[] data)
    {
        Direction = direction;
        ConnectionId = connectionId;
        Data = data;
    }
}
