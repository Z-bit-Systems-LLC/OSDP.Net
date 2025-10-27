using System;
using Terminal.Gui;

namespace ACUConsole.Extensions
{
    /// <summary>
    /// Extension methods for Terminal.Gui ComboBox
    /// </summary>
    /// <remarks>
    /// IMPORTANT: All ComboBox instances MUST use the ConfigureForOptimalUX() extension method
    /// to ensure proper dropdown functionality and consistent user experience across the application.
    ///
    /// ComboBox Width Requirements:
    /// - Minimum width of 30 is required for dropdown lists to display properly
    /// - The width parameter in new ComboBox(new Rect(x, y, width, height), items) directly affects
    ///   the dropdown list display area, not just the input box
    /// - Using width less than 30 will cause dropdown lists to be clipped or not display correctly
    ///
    /// Example Usage:
    /// var comboBox = new ComboBox(new Rect(15, 1, 30, 5), items)
    ///     .ConfigureForOptimalUX();
    /// </remarks>
    public static class ComboBoxExtensions
    {
        /// <summary>
        /// Minimum recommended width for ComboBox to ensure dropdown lists display correctly
        /// </summary>
        public const int MinimumRecommendedWidth = 30;

        /// <summary>
        /// Configures the ComboBox with standard settings for better user experience.
        /// This method ensures consistent behavior across all ComboBox instances in the application.
        /// </summary>
        /// <param name="comboBox">The ComboBox to configure</param>
        /// <returns>The configured ComboBox for method chaining</returns>
        /// <remarks>
        /// IMPORTANT: Always call this method after creating a ComboBox instance.
        ///
        /// This method configures:
        /// - HideDropdownListOnClick: Automatically closes the dropdown after selection
        ///
        /// Width Validation:
        /// If the ComboBox width is less than 30, the dropdown list may not display correctly.
        /// Ensure ComboBox is created with: new ComboBox(new Rect(x, y, 30, 5), items)
        /// </remarks>
        public static ComboBox ConfigureForOptimalUX(this ComboBox comboBox)
        {
            // Validate width to ensure dropdown functionality
            if (comboBox.Frame.Width < MinimumRecommendedWidth)
            {
                throw new ArgumentException(
                    $"ComboBox width must be at least {MinimumRecommendedWidth} for dropdown lists to display correctly. " +
                    $"Current width is {comboBox.Frame.Width}. " +
                    $"Use: new ComboBox(new Rect(x, y, {MinimumRecommendedWidth}, 5), items)",
                    nameof(comboBox));
            }

            comboBox.HideDropdownListOnClick = true;

            return comboBox;
        }
    }
}