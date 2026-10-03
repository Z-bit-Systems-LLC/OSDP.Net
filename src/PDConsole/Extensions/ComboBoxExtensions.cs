using Terminal.Gui.Views;

namespace PDConsole.Extensions
{
    /// <summary>
    /// Extension methods for Terminal.Gui <see cref="DropDownList"/> (the Terminal.Gui v2
    /// replacement for the v1 ComboBox).
    /// </summary>
    /// <remarks>
    /// All drop-down selectors should use the <see cref="ConfigureForOptimalUX"/> extension method
    /// to ensure consistent behavior across the application.
    ///
    /// Width Requirements:
    /// - A minimum width of <see cref="MinimumRecommendedWidth"/> is recommended so the expanded
    ///   drop-down list displays correctly.
    /// </remarks>
    public static class ComboBoxExtensions
    {
        /// <summary>
        /// Minimum recommended width for a drop-down list so its expanded list displays correctly.
        /// </summary>
        public const int MinimumRecommendedWidth = 30;

        /// <summary>
        /// Configures the drop-down list with standard settings for a consistent user experience.
        /// </summary>
        /// <param name="dropDownList">The drop-down list to configure.</param>
        /// <returns>The configured drop-down list for method chaining.</returns>
        public static DropDownList ConfigureForOptimalUX(this DropDownList dropDownList)
        {
            // Terminal.Gui v2 manages the drop-down popup internally; no additional
            // configuration is required beyond ensuring an adequate width at creation time.
            return dropDownList;
        }
    }
}
