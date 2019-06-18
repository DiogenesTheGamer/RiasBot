using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Services;

namespace RiasBot.Modules.Bot
{
    public partial class Bot
    {
        public class Activity : RiasSubmodule
        {
            private readonly DiscordShardedClient _client;
            private readonly BotService _botService;

            public Activity(DiscordShardedClient client, BotService botService)
            {
                _client = client;
                _botService = botService;
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task ActivityAsync(string type = null, [Remainder]string name = null)
            {
                _botService.StopActivityRotate();

                name = name ?? "";
                if (type is null)
                {
                    await _client.SetActivityAsync(new Game(""));
                    return;
                }

                switch (type.ToLower())
                {
                    case "playing":
                        await _client.SetGameAsync(name);
                        await ReplyConfirmationAsync("activity_set", GetText("activity_playing", name));
                        break;
                    case "listening":
                        await _client.SetGameAsync(name, type: ActivityType.Listening);
                        await ReplyConfirmationAsync("activity_set", GetText("activity_listening", name));
                        break;
                    case "watching":
                        await _client.SetGameAsync(name, type: ActivityType.Watching);
                        await ReplyConfirmationAsync("activity_set", GetText("activity_watching", name));
                        break;
                    case "streaming":
                        if (name.Contains(" "))
                        {
                            var streamUrl = name.Substring(0, name.IndexOf(" ", StringComparison.Ordinal));
                            var streamName = name.Remove(0, name.IndexOf(" ", StringComparison.Ordinal)).TrimStart();
                            await _client.SetGameAsync(streamName, streamUrl, ActivityType.Streaming);
                            await ReplyConfirmationAsync("activity_set", GetText("activity_streaming", streamName));
                        }
                        break;
                    default: return;
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task ActivityRotateAsync(int duration, [Remainder]string status)
            {
                if (duration < 12)
                {
                    await ReplyErrorAsync("activity_rotation_limit", 12);
                    return;
                }
                var statuses = status.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                _botService.StopActivityRotate();
                _botService.StartActivityRotateAsync(duration, statuses);

                await ReplyConfirmationAsync("activity_rotation_set", duration, string.Join("\n", statuses));
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task StatusAsync(string name)
            {
                switch (name.ToLowerInvariant())
                {
                    case "online":
                        await ((DiscordShardedClient)Context.Client).SetStatusAsync(UserStatus.Online);
                        await ReplyConfirmationAsync("status_set", GetText("status_" + name.ToLowerInvariant()));
                        break;
                    case "idle":
                        await ((DiscordShardedClient)Context.Client).SetStatusAsync(UserStatus.Idle);
                        await ReplyConfirmationAsync("status_set", GetText("status_" + name.ToLowerInvariant()));
                        break;
                    case "afk":
                        await ((DiscordShardedClient)Context.Client).SetStatusAsync(UserStatus.AFK);
                        await ReplyConfirmationAsync("status_set", GetText("status_" + name.ToLowerInvariant()));
                        break;
                    case "donotdisturb":
                    case "dnd":
                        await ((DiscordShardedClient)Context.Client).SetStatusAsync(UserStatus.DoNotDisturb);
                        await ReplyConfirmationAsync("status_set", GetText("status_" + name.ToLowerInvariant()));
                        break;
                    case "offline":
                        await ((DiscordShardedClient)Context.Client).SetStatusAsync(UserStatus.Offline);
                        await ReplyConfirmationAsync("status_set", GetText("status_" + name.ToLowerInvariant()));
                        break;
                    case "invisible":
                        await ((DiscordShardedClient)Context.Client).SetStatusAsync(UserStatus.Invisible);
                        await ReplyConfirmationAsync("status_set", GetText("status_" + name.ToLowerInvariant()));
                        break;
                }
            }
        }
    }
}