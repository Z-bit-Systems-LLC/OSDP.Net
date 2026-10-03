using System.Collections.Generic;
using Terminal.Gui.Editor.Rendering;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace ACUConsole.Controls
{
    /// <summary>
    /// A read-only <see cref="Terminal.Gui.Editor.Editor"/> for the message log. Text can be selected and
    /// copied to the clipboard (Ctrl+C or the right-click context menu; Ctrl+C with no selection copies
    /// the current line), and individual lines can be given their own color.
    /// </summary>
    public class LogView : Terminal.Gui.Editor.Editor
    {
        private IReadOnlyList<Attribute?> _lineAttributes = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="LogView"/> class.
        /// </summary>
        public LogView()
        {
            ReadOnly = true;
            ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar | ViewportSettingsFlags.HasHorizontalScrollBar;

            // The log is replaced wholesale on every refresh; keeping undo history would grow without bound
            Document!.UndoStack.SizeLimit = 0;

            LineTransformers.Add(new LineColorTransformer(this));
        }

        /// <summary>
        /// Replaces the log contents.
        /// </summary>
        /// <param name="text">The text to display.</param>
        /// <param name="lineAttributes">An optional color for each line of <paramref name="text"/>;
        /// <see langword="null"/> entries use the normal color.</param>
        public void SetLog(string text, IReadOnlyList<Attribute?> lineAttributes)
        {
            _lineAttributes = lineAttributes;

            // Replacing the document scrolls the caret into view, which would yank the user back to the
            // caret on every new event, so restore the caret and scroll position afterward
            var caretOffset = CaretOffset;
            var viewport = Viewport;

            Text = text;

            CaretOffset = caretOffset;
            Viewport = viewport;
        }

        private sealed class LineColorTransformer(LogView owner) : IVisualLineTransformer
        {
            public void Transform(CellVisualLine line)
            {
                // Document line numbers are one-based
                var index = line.DocumentLine.LineNumber - 1;
                if (index < 0 || index >= owner._lineAttributes.Count || owner._lineAttributes[index] is not { } attribute)
                {
                    return;
                }

                foreach (var element in line.Elements)
                {
                    element.Attribute = attribute;
                }
            }
        }
    }
}
