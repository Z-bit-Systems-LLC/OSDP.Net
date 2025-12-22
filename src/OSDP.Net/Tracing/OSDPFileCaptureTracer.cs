using System;
using System.Collections.Concurrent;

namespace OSDP.Net.Tracing;

internal static class OSDPFileCaptureTracer
{
    private static readonly ConcurrentDictionary<Guid, OSDPCaptureFileWriter> Writers = new();

    public static void Trace(TraceEntry trace)
    {
        var writer = Writers.GetOrAdd(trace.ConnectionId, connectionId =>
            new OSDPCaptureFileWriter($"{connectionId:D}.osdpcap", "OSDP.Net", append: true));

        writer.WriteTrace(trace);
    }

    /// <summary>
    /// Closes and disposes the writer for a specific connection.
    /// Should be called when a connection is closed to properly release resources.
    /// </summary>
    internal static void CloseWriter(Guid connectionId)
    {
        if (Writers.TryRemove(connectionId, out var writer))
        {
            writer.Dispose();
        }
    }

    /// <summary>
    /// Closes and disposes all writers.
    /// Should be called during application shutdown.
    /// </summary>
    internal static void CloseAllWriters()
    {
        foreach (var writer in Writers.Values)
        {
            writer.Dispose();
        }
        Writers.Clear();
    }
}
