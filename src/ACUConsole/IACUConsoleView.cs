using System;

namespace ACUConsole
{
    /// <summary>
    /// Interface defining the contract between the View and Presenter in MVP pattern
    /// The View is responsible for UI rendering and collecting user input
    /// </summary>
    public interface IACUConsoleView
    {
        /// <summary>
        /// Initializes the view and sets up UI components
        /// </summary>
        void Initialize();

        /// <summary>
        /// Starts the main application loop
        /// </summary>
        void Run();

        /// <summary>
        /// Shuts down the view and cleans up resources
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Shows an informational message to the user
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="message">Message content</param>
        void ShowInformation(string title, string message);

        /// <summary>
        /// Shows an error message to the user
        /// </summary>
        /// <param name="title">Error title</param>
        /// <param name="message">Error message</param>
        void ShowError(string title, string message);

        /// <summary>
        /// Shows a warning message to the user
        /// </summary>
        /// <param name="title">Warning title</param>
        /// <param name="message">Warning message</param>
        void ShowWarning(string title, string message);

        /// <summary>
        /// Asks the user a yes/no question
        /// </summary>
        /// <param name="title">Question title</param>
        /// <param name="message">Question text</param>
        /// <returns>True if user chose Yes, false if No</returns>
        bool AskYesNo(string title, string message);

        /// <summary>
        /// Updates the discover menu item state
        /// </summary>
        /// <param name="title">New menu item title</param>
        /// <param name="action">New action to perform when clicked</param>
        void UpdateDiscoverMenuItem(string title, Action action);

        /// <summary>
        /// Forces a refresh of the message display
        /// </summary>
        void RefreshMessageDisplay();
    }
}