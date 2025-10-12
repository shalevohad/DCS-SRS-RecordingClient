using System;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Infrastructure;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient
{
    /// <summary>
    /// Modernized program entry point using factory pattern and proper DI
    /// </summary>
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [STAThread]
        static void Main()
        {
            try
            {
                // Enable visual styles and high DPI support
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetHighDpiMode(HighDpiMode.SystemAware);

                // Set up global exception handling early
                SetupGlobalExceptionHandling();

                Logger.Info("Starting DCS SRS Recording Player Client");

                // Create main form using factory pattern
                using var mainForm = MainFormFactory.CreateMainForm();
                
                Logger.Info("Main form created successfully, starting application");
                Application.Run(mainForm);
                
                Logger.Info("Application shutdown complete");
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Fatal error during application startup");
                
                var detailedMessage = $"A fatal error occurred during startup:\n\n{ex.Message}";
                if (ex.InnerException != null)
                {
                    detailedMessage += $"\n\nInner exception: {ex.InnerException.Message}";
                }
                
                if (ex.StackTrace != null)
                {
                    detailedMessage += $"\n\nStack trace:\n{ex.StackTrace}";
                }
                
                MessageBox.Show(
                    detailedMessage,
                    "Fatal Error - DCS SRS Recording Player Client",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void SetupGlobalExceptionHandling()
        {
            // Handle unhandled exceptions in UI thread
            Application.ThreadException += (sender, e) =>
            {
                Logger.Error(e.Exception, "Unhandled exception in UI thread");
                
                var errorMessage = $"An unexpected error occurred:\n\n{e.Exception.Message}";
                if (e.Exception.InnerException != null)
                {
                    errorMessage += $"\n\nInner exception: {e.Exception.InnerException.Message}";
                }
                errorMessage += "\n\nPlease check the log files for more details.";
                
                MessageBox.Show(
                    errorMessage,
                    "Unexpected Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            };

            // Handle unhandled exceptions in background threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                Logger.Fatal(exception, "Unhandled exception in background thread");
                
                if (e.IsTerminating)
                {
                    Logger.Fatal("Application is terminating due to unhandled exception");
                    
                    // Show a final error message if the application is terminating
                    try
                    {
                        var message = $"A fatal error occurred that cannot be recovered from:\n\n{exception?.Message}\n\nThe application will now exit.";
                        MessageBox.Show(message, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch
                    {
                        // If we can't even show a message box, just exit
                    }
                }
            };

            // Set mode to catch exceptions
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        }
    }
}