using System;
using System.Threading.Tasks;
using Discord;
using RiasBot.Commons.Attributes;

namespace RiasBot.Services
{
    [Service]
    public class RLog
    {
        public event Func<LogMessage, Task> Log;
        
        /// <summary>
        /// Output a verbose log message to the console
        /// </summary>
        public Task Verbose(string message)
        {
            var logMessage = new LogMessage(LogSeverity.Verbose, "RiasBot", message);
            Log?.Invoke(logMessage);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Output an info log message to the console
        /// </summary>
        public Task Info(string message)
        {
            var logMessage = new LogMessage(LogSeverity.Info, "RiasBot", message);
            Log?.Invoke(logMessage);

            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Output a debug log message to the console
        /// </summary>
        public Task Debug(string message)
        {
            var logMessage = new LogMessage(LogSeverity.Debug, "RiasBot", message);
            Log?.Invoke(logMessage);

            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Output an error log message to the console
        /// </summary>
        public Task Error(string message)
        {
            var logMessage = new LogMessage(LogSeverity.Error, "RiasBot", message);
            Log?.Invoke(logMessage);

            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Output a warning log message to the console
        /// </summary>
        public Task Warning(string message)
        {
            var logMessage = new LogMessage(LogSeverity.Warning, "RiasBot", message);
            Log?.Invoke(logMessage);

            return Task.CompletedTask;
        }
    }
}