using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Player;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Music.Commons;
using RiasBot.Services;

namespace RiasBot.Modules.Utility
{
    public partial class Utility
    {
        public class Info : RiasSubmodule
        {
            private readonly DiscordShardedClient _client;
            private readonly IBotCredentials _creds;
            private readonly InteractiveService _is;
            private readonly IAudioService _audioService;

            public Info(DiscordShardedClient client, IBotCredentials creds, InteractiveService iss, IAudioService audioService)
            {
                _client = client;
                _creds = creds;
                _is = iss;
                _audioService = audioService;
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            public async Task StatsAsync()
            {
                var guilds = await Context.Client.GetGuildsAsync();
                var shard = 0;
                if (Context.Guild != null)
                    shard = _client.GetShardIdFor(Context.Guild) + 1;

                var textChannels = 0;
                var voiceChannels = 0;
                var users = 0;

                foreach (var guild in guilds)
                {
                    textChannels += ((SocketGuild)guild).TextChannels.Count;
                    voiceChannels += ((SocketGuild)guild).VoiceChannels.Count;
                    users += ((SocketGuild)guild).MemberCount;
                }

                var playingPlayers = 0;
                var afkPlayers = 0;

                foreach (var musicPlayer in _audioService.GetPlayers<MusicPlayer>())
                {
                    switch (musicPlayer.State)
                    {
                        case PlayerState.Playing:
                            playingPlayers++;
                            break;
                        case PlayerState.NotPlaying:
                        case PlayerState.Paused:
                            afkPlayers++;
                            break;
                    }
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithAuthor($"{Context.Client.CurrentUser.Username} Bot v{RiasBot.Version}", Context.Client.CurrentUser.GetRealAvatarUrl())
                    .AddField(GetText("author"), RiasBot.Author, true).AddField(GetText("bot_id"), Context.Client.CurrentUser.Id, true)
                    .AddField(GetText("master_id"), _creds.MasterId, true).AddField(GetText("shard"), $"#{shard}/{_client.Shards.Count}", true)
                    .AddField(GetText("in_server"), Context.Guild?.Name ?? "-", true).AddField(GetText("commands_executed"), RiasBot.CommandsExecuted, true)
                    .AddField(GetText("uptime"), GetTimeString(RiasBot.UpTime.Elapsed), true)
                    .AddField(GetText("presence"), $"{guilds.Count} {GetText("guilds")}\n{textChannels} {GetText("text_channels")}\n" +
                                                   $"{voiceChannels} {GetText("voice_channels")}\n{users} {GetText("#bot_users")}", true)
                    .AddField(GetText("music"), $"{GetText("music_playing", playingPlayers)}\n{GetText("music_afk", afkPlayers)}", true);

                var links = new StringBuilder();
                const string delimiter = " • ";

                if (!string.IsNullOrEmpty(_creds.Invite))
                    links.Append(GetText("#help_invite_me", _creds.Invite));

                if (links.Length > 0) links.Append(delimiter);
                if (!string.IsNullOrEmpty(_creds.OwnerServerInvite))
                {
                    var ownerServer = await Context.Client.GetGuildAsync(_creds.OwnerServerId);
                    links.Append(GetText("#help_support_server", ownerServer.Name, _creds.OwnerServerInvite));
                }

                if (links.Length > 0) links.Append(delimiter);
                if (!string.IsNullOrEmpty(_creds.Website))
                    links.Append(GetText("#help_website", _creds.Website));

                if (links.Length > 0) links.Append(delimiter);
                if (!string.IsNullOrEmpty(_creds.Patreon))
                    links.Append(GetText("#help_donate", _creds.Patreon));

                embed.AddField(GetText("#help_links"), links.ToString());

                embed.WithThumbnailUrl(Context.Client.CurrentUser.GetRealAvatarUrl());
                embed.WithFooter("© 2018 Copyright: Koneko#0001");

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task UserInfoAsync([Remainder] IGuildUser user = null)
            {
                user = user ?? (IGuildUser)Context.User;

                var activity = user.Activity?.Name;
                var activityType = user.Activity?.Type;

                switch (activityType)
                {
                    case ActivityType.Playing:
                        activity = GetText("#bot_activity_playing", activity);
                        break;
                    case ActivityType.Listening:
                        activity = GetText("#bot_activity_listening", activity);
                        break;
                    case ActivityType.Watching:
                        activity = GetText("#bot_activity_watching", activity);
                        break;
                    case ActivityType.Streaming:
                        activity = GetText("#bot_activity_streaming", activity);
                        break;
                    default:
                        activity = "-";    //if the activityType is null
                        break;
                }

                var joinedServer = user.JoinedAt?.UtcDateTime.ToUniversalTime().ToString("MMM dd, yyyy hh:mm tt");
                var accountCreated = user.CreatedAt.UtcDateTime.ToUniversalTime().ToString("MMM dd, yyyy hh:mm tt");

                var userRoles = user.RoleIds.Select(roleId => Context.Guild.GetRole(roleId))
                    .Where(role => role.Id != Context.Guild.EveryoneRole.Id)
                    .OrderByDescending(r => r.Position)
                    .ToList();

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .AddField(GetText("username"), user, true).AddField(GetText("nickname"), user.Nickname ?? "-", true)
                    .AddField(GetText("activity"), activity, true).AddField(GetText("#administration_id"), user.Id, true)
                    .AddField(GetText("status"), user.Status, true).AddField(GetText("joined_server"), joinedServer ?? "-", true)
                    .AddField(GetText("joined_discord"), accountCreated, true).AddField($"{GetText("roles")} ({userRoles.Count})",
                        userRoles.Count == 0 ? "-" : string.Join("\n", userRoles.Take(10)), true)
                .WithThumbnailUrl(user.GetRealAvatarUrl());

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task ServerInfoAsync()
            {
                var guild = (SocketGuild)Context.Guild;

                var onlineUsersCount = 0;
                var botsCount = 0;

                foreach (var getUser in guild.Users)
                {
                    if (getUser.IsBot) botsCount++;
                    if (getUser.Status != UserStatus.Offline || getUser.Status != UserStatus.Invisible)
                        onlineUsersCount++;
                }
                var guildCreatedAt = guild.CreatedAt.UtcDateTime.ToUniversalTime().ToString("MMM dd, yyyy hh:mm tt");

                var afkChannel = guild.AFKChannel?.Name;
                if (string.IsNullOrEmpty(afkChannel))
                    afkChannel = "-";

                var emotes = new StringBuilder();
                foreach (var emote in guild.Emotes)
                {
                    if (emotes.Length + emote.ToString().Length <= 1024)
                    {
                        emotes.Append(emote);
                    }
                }

                if (emotes.Length == 0)
                    emotes.Append("-");

                var features = string.Join("\n", guild.Features);

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithTitle(Context.Guild.Name)
                    .AddField(GetText("#administration_id"), guild.Id, true).AddField(GetText("owner"), guild.Owner, true)
                    .AddField(GetText("members"), guild.MemberCount, true).AddField(GetText("currently_online"), onlineUsersCount, true)
                    .AddField(GetText("bots"), botsCount, true).AddField(GetText("created_at"), guildCreatedAt, true)
                    .AddField(GetText("text_channels"), guild.TextChannels.Count, true).AddField(GetText("voice_channels"), guild.VoiceChannels.Count, true)
                    .AddField(GetText("afk_channel"), afkChannel, true).AddField(GetText("region"), Context.Guild.VoiceRegionId, true)
                    .AddField(GetText("verification_level"), guild.VerificationLevel.ToString(), true);

                if (guild.Features.Any())
                    embed.AddField(GetText("guild_features", guild.Features.Count), features, true);

                embed.AddField(GetText("guild_emotes", guild.Emotes.Count), emotes)
                    .WithThumbnailUrl(Context.Guild.IconUrl);

                if (!string.IsNullOrEmpty(guild.SplashUrl))
                    embed.WithImageUrl(guild.SplashUrl);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            public async Task ShardsInfoAsync()
            {
                var shards = _client.Shards;
                var shardsConnected = shards.Count(x => x.ConnectionState == ConnectionState.Connected);

                var shardIndex = 0;
                var shardsConnectionState = shards.Select(shard => GetText("shard_state", shardIndex++, shard.ConnectionState.ToString()));

                var pager = new PaginatedMessage
                {
                    Title = GetText("shards_info", shardsConnected, shards.Count),
                    Color = new Color(_creds.ConfirmColor),
                    Pages = shardsConnectionState,
                    Options = new PaginatedAppearanceOptions
                    {
                        ItemsPerPage = 15,
                        Timeout = TimeSpan.FromMinutes(1),
                        DisplayInformationIcon = false,
                        JumpDisplayOptions = JumpDisplayOptions.Never
                    }
                };

                await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task UserAvatarAsync([Remainder]IGuildUser user = null)
            {
                user = user ?? (IGuildUser)Context.User;

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithAuthor(user.ToString(), null, user.GetRealAvatarUrl())
                    .WithImageUrl(user.GetRealAvatarUrl());

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task ServerIconAsync()
            {
                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithImageUrl(Context.Guild.IconUrl + "?size=1024");

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task WhoIsPlayingAsync([Remainder]string game)
            {
                var guildUsers = await Context.Guild.GetUsersAsync();
                var playingUsers = (from guildUser in guildUsers
                    let activityName = guildUser.Activity?.Name
                    where !string.IsNullOrEmpty(activityName)
                    where activityName.StartsWith(game, StringComparison.InvariantCultureIgnoreCase)
                    select new UserActivity {Username = guildUser.ToString(), ActivityName = activityName}).ToList();

                if (playingUsers.Any())
                {
                    var playingUsersList = new List<string>();

                    var playingUsersGroup = playingUsers.OrderBy(x => x.Username).GroupBy(y => y.ActivityName);
                    foreach (var group in playingUsersGroup)
                    {
                        playingUsersList.Add($"•{Format.Bold(group.Key)}");
                        playingUsersList.AddRange(group.Select(subGroup => $"\t~>{subGroup.Username}"));
                    }

                    var pager = new PaginatedMessage
                    {
                        Title = GetText("users_play", game),
                        Color = new Color(_creds.ConfirmColor),
                        Pages = playingUsersList,
                        Options = new PaginatedAppearanceOptions
                        {
                            ItemsPerPage = 15,
                            Timeout = TimeSpan.FromMinutes(1),
                            DisplayInformationIcon = false,
                            JumpDisplayOptions = JumpDisplayOptions.Never
                        }

                    };

                    await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
                }
                else
                {
                    await ReplyErrorAsync("no_user_play", game);
                }
            }

            private static string GetTimeString(TimeSpan timeSpan)
            {
                var days = timeSpan.Days;
                var hoursInt = timeSpan.Hours;
                var minutesInt = timeSpan.Minutes;
                var secondsInt = timeSpan.Seconds;

                var hours = hoursInt.ToString();
                var minutes = minutesInt.ToString();
                var seconds = secondsInt.ToString();

                if (hoursInt < 10)
                    hours = "0" + hours;
                if (minutesInt < 10)
                    minutes = "0" + minutes;
                if (secondsInt < 10)
                    seconds = "0" + seconds;

                return $"{days} days {hours}:{minutes}:{seconds}";
            }

            private class UserActivity
            {
                public string Username { get; set; }
                public string ActivityName { get; set; }
            }
        }
    }
}