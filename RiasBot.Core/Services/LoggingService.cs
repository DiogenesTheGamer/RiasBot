using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using Serilog;
using Serilog.Events;
using Victoria;

namespace RiasBot.Services
{
    [Service]
    public class LoggingService
    {
        public string CommandArguments { private get; set; }

        public LoggingService(DiscordShardedClient client, CommandService commands, LavaShardClient lavaShardClient)
        {
            client.Log += OnDiscordLogAsync;
            commands.Log += OnDiscordLogAsync;
            commands.CommandExecuted += OnCommandLogAsync;
            lavaShardClient.Log += OnDiscordLogAsync;
        }

        private Task OnDiscordLogAsync(LogMessage msg)
        {
            LogEventLevel logEventLevel;
            switch (msg.Severity)
            {
                case LogSeverity.Verbose:
                    logEventLevel = LogEventLevel.Verbose;
                    break;
                case LogSeverity.Info:
                    logEventLevel = LogEventLevel.Information;
                    break;
                case LogSeverity.Debug:
                    logEventLevel = LogEventLevel.Debug;
                    break;
                case LogSeverity.Warning:
                    logEventLevel = LogEventLevel.Warning;
                    break;
                case LogSeverity.Error:
                    logEventLevel = LogEventLevel.Error;
                    break;
                case LogSeverity.Critical:
                    logEventLevel = LogEventLevel.Fatal;
                    break;
                default:
                    logEventLevel = LogEventLevel.Verbose;
                    break;
            }

            Log.Logger.Write(logEventLevel, $"{msg.Source}: {msg.Exception?.ToString() ?? msg.Message}");

            return Task.CompletedTask;
        }

        private Task OnCommandLogAsync(Optional<CommandInfo> commandInfo, ICommandContext context, IResult result)
        {
            if (!commandInfo.IsSpecified) return Task.CompletedTask;

            Log.Information($"{DateTime.UtcNow:MMM dd hh:mm:ss} [Command] \"{commandInfo.Value?.Name}\"\n" +
                                       $"\t\t[Arguments] \"{CommandArguments}\"\n" +
                                       $"\t\t[User] \"{context.User}\" ({context.User.Id})\n" +
                                       $"\t\t[Channel] \"{context.Channel.Name}\" ({context.Channel.Id})\n" +
                                       $"\t\t[Guild] \"{context.Guild?.Name ?? "DM"}\" ({context.Guild?.Id ?? 0})");
            return Task.CompletedTask;
        }
    }
}