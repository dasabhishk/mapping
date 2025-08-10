using System;
using Serilog;

namespace Logger
{
    public sealed class Logger : ILogger
    {
        private static readonly Logger logInstance = new Logger();

        private Logger() { }

        public static Logger GetInstance
        {
            get
            {
                return logInstance;
            }
        }
        public void LogInfo(string message)
        {
            Log.Information(message);
        }

        public void LogWarning(string message)
        {
            Log.Warning(message);
        }

        public void LogError(string message, Exception? ex)
        {
            string fullMessage = ex != null
                ? $"{message}{Environment.NewLine}{ex.Message}{Environment.NewLine}{ex.StackTrace}"
                : message;
            Log.Error(ex, fullMessage);
        }
    }
}
