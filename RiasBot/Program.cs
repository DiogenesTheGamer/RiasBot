﻿using System;
using System.IO;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace RiasBot
{
    public class Program
    {
        private static readonly string LogPath = Path.Combine(Environment.CurrentDirectory, "logs/rias-.log");

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Verbose()
#endif
                .WriteTo.Console(theme: SystemConsoleTheme.Literate)
                .WriteTo.Async(x => x.File(LogPath, shared: true, rollingInterval: RollingInterval.Day))
                .CreateLogger();

            new RiasBot().StartAsync().GetAwaiter().GetResult();
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.ExceptionObject.ToString());
            Log.CloseAndFlush();
        }
    }
}