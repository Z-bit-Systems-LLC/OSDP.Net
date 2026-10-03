using System.Collections.Generic;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace ACUConsole.Controls
{
    /// <summary>
    /// A read-only <see cref="TextView"/> for the message log. Text can be selected and copied
    /// to the clipboard (Ctrl+C or the right-click context menu), and individual lines can be
    /// given their own color.
    /// </summary>
    /// <remarks>
    /// <see cref="TextView"/> is obsolete in Terminal.Gui 2.5 in favor of Terminal.Gui.Editor's
    /// EditorView. It is still the only built-in view with text selection, so its use is kept
    /// to this class to make a later switch a local change.
    /// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
    public class LogTextView : TextView
#pragma warning restore CS0618
    {
        private IReadOnlyList<Attribute?> _lineAttributes = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="LogTextView"/> class.
        /// </summary>
        public LogTextView()
        {
            ReadOnly = true;
            WordWrap = false;
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
            Text = text;
        }

        /// <inheritdoc/>
        protected override void OnDrawReadOnlyColor(List<Cell> line, int idxCol, int idxRow)
        {
            var attribute = idxRow >= 0 && idxRow < _lineAttributes.Count ? _lineAttributes[idxRow] : null;
            SetAttribute(attribute ?? GetAttributeForRole(VisualRole.Normal));
        }
    }
}
