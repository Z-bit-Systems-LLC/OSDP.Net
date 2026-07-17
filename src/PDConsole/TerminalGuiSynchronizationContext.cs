using System.Threading;
using Terminal.Gui.App;

namespace PDConsole
{
    /// <summary>
    /// A <see cref="SynchronizationContext"/> that marshals continuations onto the Terminal.Gui
    /// UI thread via <see cref="IApplication.Invoke(System.Action)"/>.
    /// </summary>
    /// <remarks>
    /// Terminal.Gui v2 (unlike v1) does not install a synchronization context, so <c>await</c>
    /// continuations in <c>async</c> UI handlers would otherwise resume on a thread-pool thread.
    /// Any UI work performed after an <c>await</c> (showing a dialog, MessageBox, updating a view)
    /// must run on the UI thread; installing this context on the UI thread before the application
    /// runs restores that behavior.
    /// </remarks>
    internal sealed class TerminalGuiSynchronizationContext : SynchronizationContext
    {
        private readonly IApplication _app;

        public TerminalGuiSynchronizationContext(IApplication app)
        {
            _app = app;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            _app.Invoke(() => d(state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            _app.Invoke(() => d(state));
        }

        public override SynchronizationContext CreateCopy()
        {
            return new TerminalGuiSynchronizationContext(_app);
        }
    }
}
