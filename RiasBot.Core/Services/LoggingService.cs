using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using Victoria;

namespace RiasBot.Services
{
    [Service]
    public class LoggingService
    {
        public bool Ready { get; set; }
        public string CommandArguments { private get; set; }

        public LoggingService(DiscordShardedClient client, CommandService commands, LavaShardClient lavaShardClient, RLog log)
        {
            client.Log += OnDiscordLogAsync;
            commands.Log += OnDiscordLogAsync;
            commands.CommandExecuted += OnCommandLogAsync;
            lavaShardClient.Log += OnDiscordLogAsync;
            log.Log += OnDiscordLogAsync;
        }

        private Task OnDiscordLogAsync(LogMessage msg)
        {
            if (!Ready)
            {
                var log = $"{DateTime.UtcNow:MMM dd hh:mm:ss} [{msg.Severity}] {msg.Source}: {msg.Exception?.ToString() ?? msg.Message}";

                Console.Out.WriteLineAsync(log);
            }
            else if (msg.Severity == LogSeverity.Info || msg.Severity == LogSeverity.Error || msg.Severity == LogSeverity.Critical)
            {
                var log = $"{DateTime.UtcNow:MMM dd hh:mm:ss} [{msg.Severity}] {msg.Source}: {msg.Exception?.ToString() ?? msg.Message}";

                Console.Out.WriteLineAsync(log);
            }

            return Task.CompletedTask;
        }

        private Task OnCommandLogAsync(Optional<CommandInfo> commandInfo, ICommandContext context, IResult result)
        {
            if (!commandInfo.IsSpecified) return Task.CompletedTask;
            
            Console.Out.WriteLineAsync($"{DateTime.UtcNow:MMM dd hh:mm:ss} [Command] \"{commandInfo.Value?.Name}\"\n" +
                                       $"\t\t[Arguments] \"{CommandArguments}\"\n" +
                                       $"\t\t[User] \"{context.User}\" ({context.User.Id})\n" +
                                       $"\t\t[Channel] \"{context.Channel.Name}\" ({context.Channel.Id})\n" +
                                       $"\t\t[Guild] \"{context.Guild?.Name ?? "DM"}\" ({context.Guild?.Id ?? 0})");
            return Task.CompletedTask;
        }
    }
}