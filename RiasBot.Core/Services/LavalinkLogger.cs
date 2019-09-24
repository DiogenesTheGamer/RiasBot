using System;
using Discord;
using Lavalink4NET.Logging;
using Serilog.Events;

namespace RiasBot.Services
{
    public class LavalinkLogger : ILogger
    {
        public void Log(object source, string message, LogLevel level = LogLevel.Information, Exception exception = null)
        {
            LogEventLevel logEventLevel;
            switch (level)
            {
                case LogLevel.Information:
                    logEventLevel = LogEventLevel.Information;
                    break;
                case LogLevel.Debug:
                    logEventLevel = LogEventLevel.Debug;
                    break;
                case LogLevel.Warning:
                    logEventLevel = LogEventLevel.Warning;
                    break;
                case LogLevel.Error:
                    logEventLevel = LogEventLevel.Error;
                    break;
                case LogLevel.Trace:
                    logEventLevel = LogEventLevel.Fatal;
                    break;
                default:
                    logEventLevel = LogEventLevel.Verbose;
                    break;
            }

            Serilog.Log.Write(logEventLevel, $"{source}: {exception?.ToString() ?? message}");
        }
    }
}