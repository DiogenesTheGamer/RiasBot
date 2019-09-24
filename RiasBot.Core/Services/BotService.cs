using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Tracking;
using Microsoft.Extensions.DependencyInjection;
using RiasBot.Commons.Attributes;
using RiasBot.Database.Models;
using RiasBot.Extensions;
using RiasBot.Modules.Music.Commons;
using Serilog;

namespace RiasBot.Services
{
    [Service]
    public class BotService
    {
        private readonly DiscordShardedClient _client;
        private readonly IAudioService _audioService;
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly IServiceProvider _services;

        private Timer DblTimer { get; }

        private Timer _activityTimer;
        private string[] _activities;
        private int _activityCount;

        private bool _allShardsDoneConnection;
        private int _shardsConnected;
        private int _recommendedShardCount;

        public BotService(DiscordShardedClient client, IAudioService audioService, IBotCredentials creds, DbService db, IServiceProvider services)
        {
            _client = client;
            _audioService = audioService;
            _creds = creds;
            _db = db;
            _services = services;

            _client.ShardConnected += ShardConnectedAsync;
            _client.ShardDisconnected += ShardDisconnectedAsync;
            _client.UserJoined += UserJoinedAsync;
            _client.UserLeft += UserLeftAsync;

            if (!string.IsNullOrEmpty(_creds.DiscordBotsListApiKey))
            {
                if(!_creds.IsBeta)
                {
                    DblTimer = new Timer(async _ => await DblStatsAsync(), null, new TimeSpan(0, 0, 30), new TimeSpan(0, 0, 30));
                }
            }
        }

        private async Task UserJoinedAsync(SocketGuildUser user)
        {
            if (user.Id == _client.CurrentUser.Id)
                return;

            _ = Task.Run(async() => await AddAssignableRoleAsync(user));

            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == user.Guild.Id);
                var userGuildDb = db.UserGuilds.Where(x => x.GuildId == user.Guild.Id).FirstOrDefault(x => x.UserId == user.Id);

                await SendGreetMessageAsync(guildDb, user);

                var currentUser = user.Guild.CurrentUser;
                if (!currentUser.GuildPermissions.ManageRoles) return;
                if (userGuildDb is null) return;
                if (!userGuildDb.IsMuted) return;

                var role = user.Guild.GetRole(guildDb?.MuteRole ?? 0) ?? user.Guild.Roles.FirstOrDefault(x => x.Name == "rias-mute");
                if (role != null)
                {
                    await user.AddRoleAsync(role);
                }
                else
                {
                    userGuildDb.IsMuted = false;
                    await db.SaveChangesAsync();
                }
            }
        }

        private async Task SendGreetMessageAsync(GuildConfig guildDb, SocketGuildUser user)
        {
            if (guildDb is null) return;
            if (!guildDb.Greet) return;
            if (string.IsNullOrWhiteSpace(guildDb.GreetMessage)) return;

            var channel = user.Guild.GetTextChannel(guildDb.GreetChannel);
            if (channel is null) return;

            var currentUser = user.Guild.CurrentUser;
            var preconditions = currentUser.GetPermissions(channel);
            if (!preconditions.ViewChannel || !preconditions.SendMessages) return;

            var greetMsg = ReplacePlaceholders(user, guildDb.GreetMessage);
            if (Extensions.Extensions.TryParseEmbed(greetMsg, out var embed))
                await channel.SendMessageAsync(embed: embed.Build());
            else
                await channel.SendMessageAsync(greetMsg);
        }

        private async Task UserLeftAsync(SocketGuildUser user)
        {
            if (user.Id == _client.CurrentUser.Id)
            {
                var musicPlayer = _audioService.GetPlayer<MusicPlayer>(user.Guild.Id);
                if (musicPlayer != null)
                {
                    await musicPlayer.LeaveAndDisposeAsync(false);
                }
                return;
            }

            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == user.Guild.Id);
                await SendByeMessageAsync(guildDb, user);

                if (guildDb is null) return;
                if (user.Roles.All(r => r.Id != guildDb.MuteRole))
                {
                    var userGuildDb = db.UserGuilds.Where(x => x.GuildId == user.Guild.Id).FirstOrDefault(x => x.UserId == user.Id);
                    if (userGuildDb is null) return;

                    userGuildDb.IsMuted = false;
                    await db.SaveChangesAsync();
                }
            }
        }

        private async Task SendByeMessageAsync(GuildConfig guildDb, SocketGuildUser user)
        {
            if (guildDb is null) return;
            if (!guildDb.Bye) return;
            if (string.IsNullOrWhiteSpace(guildDb.ByeMessage)) return;

            var channel = user.Guild.GetTextChannel(guildDb.ByeChannel);
            if (channel is null) return;

            var currentUser = user.Guild.CurrentUser;
            var preconditions = currentUser.GetPermissions(channel);
            if (!preconditions.ViewChannel || !preconditions.SendMessages) return;

            var byeMsg = ReplacePlaceholders(user, guildDb.ByeMessage);
            if (Extensions.Extensions.TryParseEmbed(byeMsg, out var embed))
                await channel.SendMessageAsync(embed: embed.Build());
            else
                await channel.SendMessageAsync(byeMsg);
        }

        private static string ReplacePlaceholders(SocketGuildUser user, string message)
            => new StringBuilder(message)
                .Replace("%mention%", user.Mention)
                .Replace("%user%", user.ToString())
                .Replace("%user_id%", user.Id.ToString())
                .Replace("%guild%", Format.Bold(user.Guild.Name))
                .Replace("%server%", Format.Bold(user.Guild.Name))
                .Replace("%avatar%", user.GetRealAvatarUrl()).ToString();

        private async Task ShardConnectedAsync(DiscordSocketClient client)
        {
            if (!_allShardsDoneConnection)
                _shardsConnected++;

            if (_recommendedShardCount == 0)
                _recommendedShardCount = _client.Shards.Count;

            if (_shardsConnected == _recommendedShardCount && !_allShardsDoneConnection)
            {
                await _client.GetGuild(_creds.OwnerServerId).DownloadUsersAsync().ConfigureAwait(false);

                try
                {
                    await _audioService.InitializeAsync();
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                }

                Log.Information("Lavalink started!");
                _allShardsDoneConnection = true;
            }

            //TODO: make this better
        }

        private async Task ShardDisconnectedAsync(Exception ex, DiscordSocketClient client)
        {
            foreach (var guild in client.Guilds)
            {
                var player = _audioService.GetPlayer<MusicPlayer>(guild.Id);
                if (player != null)
                    await player.LeaveAndDisposeAsync(false);
            }
        }

        public async Task AddAssignableRoleAsync(IUser user)
        {
            if (!(user is IGuildUser guildUser)) return;

            var currentUser = await guildUser.Guild.GetCurrentUserAsync();
            if (!currentUser.GuildPermissions.ManageRoles)
                return;

            var roleIds = guildUser.RoleIds;
            if (roleIds.Count > 1) return;

            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == guildUser.GuildId);

                if (guildDb is null) return;
                if (guildDb.AutoAssignableRole == 0) return;

                var aar = guildUser.Guild.GetRole(guildDb.AutoAssignableRole);
                if (aar is null) return;

                if (currentUser.CheckRoleHierarchy(aar) > 0)
                    await guildUser.AddRoleAsync(aar);
            }
        }

        public void StartActivityRotateAsync(int duration, string[] activities)
        {
            _activities = activities;
            _activityTimer = new Timer(async _ => await ActivityRotateAsync(), null, 0, duration * 1000);
        }

        public void StopActivityRotate()
        {
            _activityTimer?.Dispose();
            _activities = null;
            _activityCount = 0;
        }

        private async Task ActivityRotateAsync()
        {
            var activity = _activities[_activityCount].Trim();
            var activityType = activity.Substring(0, activity.IndexOf(" ", StringComparison.Ordinal)).Trim();
            var activityName = activity.Remove(0, activity.IndexOf(" ", StringComparison.Ordinal)).Trim();

            activityName = activityName.Replace("%guilds%", _client.Guilds.Count.ToString());
            if (activityName.Contains("%users%"))
            {
                var users = _client.Guilds.Sum(guild => guild.MemberCount);
                activityName = activityName.Replace("%users%", users.ToString());
            }

            foreach (var shard in _client.Shards)
            {
                if (shard.ConnectionState != ConnectionState.Connected) continue;

                switch (activityType.ToLowerInvariant())
                {
                    case "playing":
                        await SetGameAsync(shard, activityName);
                        break;
                    case "listening":
                        await SetGameAsync(shard, activityName, type: ActivityType.Listening);
                        break;
                    case "watching":
                        await SetGameAsync(shard, activityName, type: ActivityType.Watching);
                        break;
                    case "streaming":
                        if (activityName.Contains(" "))
                        {
                            var streamUrl = activityName.Substring(0, activityName.IndexOf(" ", StringComparison.Ordinal));
                            var streamName = activityName.Remove(0, activityName.IndexOf(" ", StringComparison.Ordinal)).TrimStart();
                            await SetGameAsync(shard, streamName, streamUrl, ActivityType.Streaming);
                        }

                        break;
                }
            }

            if (_activityCount == _activities.Length - 1)
                _activityCount = 0;
            else
                _activityCount++;
        }

        private static async Task SetGameAsync(DiscordSocketClient shard, string name, string streamUrl = null, ActivityType type = ActivityType.Playing)
        {
            try
            {
                await shard.SetGameAsync(name, streamUrl, type);
            }
            catch
            {
                //ignored
            }
        }

        private async Task DblStatsAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_creds.DiscordBotsListApiKey))
                    return;
                using (var http = new HttpClient())
                {
                    using (var content = new FormUrlEncodedContent(
                        new Dictionary<string, string> {
                            { "shard_count",  _client.Shards.Count.ToString()},
                            //{ "shard_id", _discord.ShardId.ToString() },
                            { "server_count", _client.Guilds.Count.ToString() }
                        }))
                    {
                        content.Headers.Clear();
                        content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                        http.DefaultRequestHeaders.Add("Authorization", _creds.DiscordBotsListApiKey);

                        await http.PostAsync($"https://discordbots.org/api/bots/{_client.CurrentUser.Id}/stats", content).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}