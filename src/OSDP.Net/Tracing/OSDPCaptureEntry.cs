using System;
using OSDP.Net.Model;

namespace OSDP.Net.Tracing;

/// <summary>
/// One line entry of a file that captures OSDP messages
/// </summary>
public class OSDPCaptureEntry
{
    /// <summary>
    /// Default constructor
    /// </summary>
    /// <param name="timeStamp">When the OSDP captured message was recorded</param>
    /// <param name="direction">Trace the source of the captured OSDP message</param>
    /// <param name="packet">Message data from the captured OSDP message</param>
    /// <param name="traceVersion"></param>
    /// <param name="source"></param>
    internal OSDPCaptureEntry(DateTime timeStamp, TraceDirection direction, Packet packet, string traceVersion, string source)
    {
        TimeStamp = timeStamp;
        Direction = direction;
        Packet = packet;
        TraceVersion = traceVersion;
        Source = source;
    }

    /// <summary>
    /// When the OSDP captured message was recorded
    /// </summary>
    public DateTime TimeStamp { get; private set; }
    
    /// <summary>
    /// Trace the source of the captured OSDP message
    /// </summary>
    public TraceDirection Direction { get; private set; }
    
    /// <summary>
    /// Message data from the captured OSDP message
    /// </summary>
    public Packet Packet { get; private set; }
    
    /// <summary>
    /// Version of the OSDP capture file
    /// </summary>
    public string TraceVersion { get; private set; }
    
    /// <summary>
    /// Source of the OSDP captured log
    /// </summary>
    public string Source { get; private set; }
}