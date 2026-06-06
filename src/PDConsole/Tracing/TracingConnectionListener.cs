using System;
using System.Threading.Tasks;
using OSDP.Net.Connections;

namespace PDConsole.Tracing
{
    /// <summary>
    /// Decorates an <see cref="IOsdpConnectionListener"/> so every accepted connection is wrapped
    /// in a <see cref="TracingOsdpConnection"/> that feeds captured packets to the supplied tracer.
    /// </summary>
    internal sealed class TracingConnectionListener(
        IOsdpConnectionListener inner,
        PDPacketCaptureTracer tracer) : IOsdpConnectionListener
    {
        public int BaudRate => inner.BaudRate;

        public bool IsRunning => inner.IsRunning;

        public int ConnectionCount => inner.ConnectionCount;

        public Task Start(Func<IOsdpConnection, Task> newConnectionHandler)
        {
            return inner.Start(async connection =>
            {
                var tracingConnection = new TracingOsdpConnection(connection, tracer.Trace);
                try
                {
                    await newConnectionHandler(tracingConnection);
                }
                finally
                {
                    tracer.CloseWriter(tracingConnection.ConnectionId);
                }
            });
        }

        public Task Stop() => inner.Stop();

        public void Dispose() => inner.Dispose();
    }
}
