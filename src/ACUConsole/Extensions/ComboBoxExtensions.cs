using Terminal.Gui;

namespace ACUConsole.Extensions
{
    /// <summary>
    /// Extension methods for Terminal.Gui ComboBox
    /// </summary>
    public static class ComboBoxExtensions
    {
        /// <summary>
        /// Configures the ComboBox with standard settings for better user experience
        /// </summary>
        /// <param name="comboBox">The ComboBox to configure</param>
        /// <returns>The configured ComboBox for method chaining</returns>
        public static ComboBox ConfigureForOptimalUX(this ComboBox comboBox)
        {
            comboBox.HideDropdownListOnClick = true;
            return comboBox;
        }
    }
}