using System.Windows;
using Logger;

namespace CMMT.Services
{
    public static class LoggingService
    {
        private static readonly ILogger logInstance = Logger.Logger.GetInstance;

        public static void LogInfo(string message, bool showMsgBox = false)
        {
            logInstance.LogInfo(message);
            ShowMessageBox(showMsgBox, message, "Information", MessageBoxImage.Information);
        }

        public static void LogWarning(string message, bool showMsgBox = false)
        {
            logInstance.LogWarning(message);
            ShowMessageBox(showMsgBox, message, "Warning", MessageBoxImage.Warning);
        }

        public static void LogError(string message, Exception? ex, bool showMsgBox = false)
        {
            logInstance.LogError(message, ex);
            ShowMessageBox(showMsgBox, message, "Error", MessageBoxImage.Error);
        }

        private static void ShowMessageBox(bool showMsgBox, string message, string title, MessageBoxImage type)
        {
            if (showMsgBox && !string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, type);
            }
        }
    }
}
