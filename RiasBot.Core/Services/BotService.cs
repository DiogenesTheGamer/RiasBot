using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Database.Models;
using Victoria;

namespace RiasBot.Services
{
    [Service]
    public class BotService
    {
        private readonly DiscordShardedClient _client;
        private readonly IBotCredentials _creds;
        private readonly DbService _db;

        public Timer DblTimer { get; }

        private Timer _activityTimer;
        private string[] _activities;
        private int _activityCount;

        public BotService(DiscordShardedClient client, IBotCredentials creds, DbService db, LavaShardClient lavaShardClient)
        {
            _client = client;
            _creds = creds;
            _db = db;

            lavaShardClient.StartAsync(client, new Configuration
            {
                Host = _creds.LavalinkConfig.Host,
                Port = _creds.LavalinkConfig.Port,
                Password = _creds.LavalinkConfig.Password
            });

            _client.UserJoined += OnUserJoinedAsync;
            _client.UserLeft += OnUserLeftAsync;

            if (!string.IsNullOrEmpty(_creds.DiscordBotsListApiKey))
            {
                if(!_creds.IsBeta)
                {
                    DblTimer = new Timer(async _ => await DblStatsAsync(), null, new TimeSpan(0, 0, 30), new TimeSpan(0, 0, 30));
                }
            }
        }
        
        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            var currentUser = user.Guild.CurrentUser;

            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == user.Guild.Id);
                var userGuildDb = db.UserGuilds.Where(x => x.GuildId == user.Guild.Id).FirstOrDefault(x => x.UserId == user.Id);

                if (guildDb != null)
                {
                    if (guildDb.Greet)
                    {
                        if (!string.IsNullOrEmpty(guildDb.GreetMessage))
                        {
                            var channel = user.Guild.GetTextChannel(guildDb.GreetChannel);
                            if (channel != null)
                            {
                                var bot = user.Guild.CurrentUser;
                                var preconditions = bot.GetPermissions(channel);
                                if (preconditions.ViewChannel && preconditions.SendMessages)
                                {
                                    var greetMsg = guildDb.GreetMessage;
                                    greetMsg = greetMsg.Replace("%mention%", user.Mention);
                                    greetMsg = greetMsg.Replace("%user%", user.ToString());
                                    greetMsg = greetMsg.Replace("%guild%", Format.Bold(user.Guild.Name));
                                    greetMsg = greetMsg.Replace("%server%", Format.Bold(user.Guild.Name));

                                    if (Extensions.Extensions.TryParseEmbed(greetMsg, out var embed))
                                    {
                                        await channel.SendMessageAsync(embed: embed.Build());

                                    }
                                    else
                                    {
                                        await channel.SendMessageAsync(greetMsg);
                                    }
                                }
                            }
                        }
                    }

                    if (currentUser.GuildPermissions.ManageRoles)
                    {
                        await AddAssignableRoleAsync(guildDb, user, currentUser);

                        if (userGuildDb != null)
                        {
                            if (userGuildDb.IsMuted)
                            {
                                var role = user.Guild.GetRole(guildDb.MuteRole) ?? user.Guild.Roles.FirstOrDefault(x => x.Name == "rias-mute");
                                if (role != null)
                                {
                                    await user.AddRoleAsync(role);
                                    await user.ModifyAsync(x => x.Mute = true);
                                }
                                else
                                {
                                    userGuildDb.IsMuted = false;
                                    await db.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }
                
                //TODO: reduce the nesting
            }
        }

        private async Task OnUserLeftAsync(SocketGuildUser user)
        {
            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == user.Guild.Id);
                if (guildDb != null)
                {
                    if (guildDb.Bye)
                    {
                        if (!string.IsNullOrEmpty(guildDb.ByeMessage))
                        {
                            var channel = user.Guild.GetTextChannel(guildDb.ByeChannel);
                            if (channel != null)
                            {
                                var bot = user.Guild.CurrentUser;
                                var preconditions = bot.GetPermissions(channel);
                                if (preconditions.ViewChannel && preconditions.SendMessages)
                                {
                                    var byeMsg = guildDb.ByeMessage;
                                    byeMsg = byeMsg.Replace("%mention%", user.Mention);
                                    byeMsg = byeMsg.Replace("%user%", user.ToString());
                                    byeMsg = byeMsg.Replace("%guild%", Format.Bold(user.Guild.Name));
                                    byeMsg = byeMsg.Replace("%server%", Format.Bold(user.Guild.Name));

                                    if (Extensions.Extensions.TryParseEmbed(byeMsg, out var embed))
                                    {
                                        await channel.SendMessageAsync(embed: embed.Build());

                                    }
                                    else
                                    {
                                        await channel.SendMessageAsync(byeMsg);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //TODO: Add check the mute role (rias-mute) when the user leaves the guild
            //TODO: reduce the nesting
        }

        public async Task AddAssignableRoleAsync(GuildConfig guildDb, IGuildUser user, IGuildUser currentUser)
        {
            var roleIds = user.RoleIds;
            if (roleIds.Count > 1) return;

            if (guildDb is null) return;

            if (guildDb.AutoAssignableRole > 0)
            {
                var aar = _client.GetGuild(user.Guild.Id).GetRole(guildDb.AutoAssignableRole);
                if (aar != null)
                {
                    if (Extensions.UserExtensions.CheckHierarchyRoles(aar, user.Guild, currentUser))
                    {
                        await user.AddRoleAsync(aar);
                    }
                }
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
        
        public async Task DblStatsAsync()
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