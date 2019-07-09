using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Database.Models;
using RiasBot.Extensions;
using RiasBot.Modules.Xp.Services;
using RiasBot.Services;

namespace RiasBot.Modules.Xp
{
    public class Xp : RiasModule<XpService>
    {
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly InteractiveService _is;

        public Xp(IBotCredentials creds, DbService db, InteractiveService iss)
        {
            _creds = creds;
            _db = db;
            _is = iss;
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 30, RateLimitType.GuildUser)]
        public async Task XpAsync([Remainder] IUser user = null)
        {
            user = user ?? Context.User;

            var typing = Context.Channel.EnterTypingState();

            var rolesIds = ((IGuildUser) user).RoleIds;
            var roles = rolesIds.Select(role => Context.Guild.GetRole(role)).ToList();
            var highestRole = roles.OrderByDescending(x => x.Position).Select(y => y).FirstOrDefault();

            try
            {
                using (var img = await Service.GenerateXpImageAsync((IGuildUser) user, highestRole))
                {
                    if (img != null)
                        await Context.Channel.SendFileAsync(img, $"{user.Id}_xp.png");   
                }
            }
            catch
            {
                // ignored
            }

            typing.Dispose();
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RateLimit(1, 10, RateLimitType.GuildUser)]
        public async Task XpLeaderboardAsync(int page = 1)
        {
            page--;
            if (page < 0)
                return;
            
            var restClient = ((DiscordShardedClient) Context.Client).GetShardFor(Context.Guild).Rest;

            using (var db = _db.GetDbContext())
            {
                var xpList = await Task.WhenAll(db.Users.OrderByDescending(u => u.Xp).Skip(page * 10).Take(10).AsEnumerable()
                    .Select(async (x, i) =>
                    {
                        var user = await restClient.GetUserAsync(x.UserId);
                        return $"#{i + 1 + page * 10} {user} ({user.Id})\n\t\t{x.Xp} xp\tlevel {x.Level}\n";
                    }));

                if (!xpList.Any())
                {
                    await ReplyErrorAsync("leaderboard_no_users");
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithDescription(string.Join("\n", xpList));

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 10, RateLimitType.GuildUser)]
        public async Task GuildXpLeaderboardAsync(int page = 1)
        {
            page--;
            if (page < 0)
                return;

            using (var db = _db.GetDbContext())
            {
                var xpDb = db.XpSystem.Where(x => x.GuildId == Context.Guild.Id);

                var xpDbFilter = new List<XpSystem>();
                foreach (var xpEntity in xpDb)
                {
                    var user = await Context.Client.GetUserAsync(xpEntity.UserId);
                    if (user != null)
                        xpDbFilter.Add(xpEntity);
                }

                var xpList = await Task.WhenAll(xpDbFilter.OrderByDescending(x => x.Xp).Skip(page * 10).Take(10).AsEnumerable()
                    .Select(async (x, i) =>
                    {
                        var user = await Context.Client.GetUserAsync(x.UserId);
                        return $"#{i + 1 + page * 10} {user} ({user.Id})\n\t\t{x.Xp} xp\tlevel {x.Level}\n";
                    }));

                if (!xpList.Any())
                {
                    await ReplyErrorAsync("leaderboard_no_users");
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithDescription(string.Join("\n", xpList));

                await Context.Channel.SendMessageAsync(embed: embed.Build());   
            }
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task XpNotifyAsync()
        {
            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == Context.Guild.Id);

                bool xpNotify;
                if (guildDb != null)
                {
                    xpNotify = guildDb.XpGuildNotification = !guildDb.XpGuildNotification;
                }
                else
                {
                    var xpNotifyDb = new GuildConfig {GuildId = Context.Guild.Id, XpGuildNotification = true};
                    await db.AddAsync(xpNotifyDb);
                    xpNotify = true;
                }

                await db.SaveChangesAsync();
                if (xpNotify)
                    await ReplyConfirmationAsync("notifications_enabled");
                else
                    await ReplyConfirmationAsync("notifications_disabled");   
            }
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task LevelUpRoleRewardAsync(int level, [Remainder] string name = null)
        {
            if (level <= 0)
            {
                await ReplyErrorAsync("role_reward_level_0");
                return;
            }

            using (var db = _db.GetDbContext())
            {
                var xpRolesSystemDb = db.XpRolesSystem.Where(x => x.GuildId == Context.Guild.Id);

                if (string.IsNullOrEmpty(name))
                {
                    var oldRoleReward = xpRolesSystemDb.FirstOrDefault(x => x.Level == level);
                    if (oldRoleReward != null)
                    {
                        db.Remove(oldRoleReward);
                        await db.SaveChangesAsync();
                        await ReplyConfirmationAsync("role_reward_remove", level);
                        return;
                    }

                    await ReplyErrorAsync("role_reward_level_no_role", level);
                    return;
                }
                
                var roles = ulong.TryParse(name, out var id)
                    ? Context.Guild.Roles.Where(r => r.Id == id).ToList()
                    : Context.Guild.Roles.Where(r => string.Equals(r.Name, name, StringComparison.InvariantCultureIgnoreCase)).ToList();
            
                if (!roles.Any())
                {
                    await ReplyErrorAsync("#administration_role_not_found");
                    return;
                }

                if (roles.Count > 1)
                {
                    var pager = new PaginatedMessage
                    {
                        Title = GetText("multiple_roles_found", name),
                        Color = new Color(_creds.ConfirmColor),
                        Pages = roles.Select((r, i) => $"#{i+1} {r.Name} | {GetText("#administration_id")}: {r.Id}"),
                        Options = new PaginatedAppearanceOptions
                        {
                            ItemsPerPage = 15,
                            Timeout = TimeSpan.FromMinutes(1),
                            DisplayInformationIcon = false,
                            JumpDisplayOptions = JumpDisplayOptions.Never
                        }

                    };
                
                    await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
                    return;
                }

                var role = roles.First();

                var newRoleReward = xpRolesSystemDb.FirstOrDefault(r => r.Level == level);
                if (newRoleReward != null)
                {
                    newRoleReward.RoleId = role.Id;
                }
                else
                {
                    var roleReward = new XpRolesSystem {GuildId = Context.Guild.Id, Level = level, RoleId = role.Id};
                    await db.AddAsync(roleReward);
                }

                await db.SaveChangesAsync();
                await ReplyConfirmationAsync("role_reward_added", level, role.Name);
            }
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task LevelUpRoleRewardsAsync()
        {
            using (var db = _db.GetDbContext())
            {
                var xpRolesSystemDb = db.XpRolesSystem.Where(x => x.GuildId == Context.Guild.Id);

                if (!xpRolesSystemDb.Any())
                {
                    await ReplyErrorAsync("no_role_rewards");
                    return;
                }

                var saveDb = false;

                var lurrs = new List<string>();
                foreach (var xpRole in xpRolesSystemDb.OrderBy(r => r.Level))
                {
                    var role = Context.Guild.GetRole(xpRole.RoleId);
                    if (role != null)
                    {
                        lurrs.Add($"{GetText("level")} {xpRole.Level}: {role.Name}");
                    }
                    else
                    {
                        db.Remove(xpRole);
                        saveDb = true;
                    }
                }

                if (saveDb)
                    await db.SaveChangesAsync();
            
                var pager = new PaginatedMessage
                {
                    Title = GetText("role_rewards"),
                    Color = new Color(_creds.ConfirmColor),
                    Pages = lurrs,
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
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task ResetGuildExperienceAsync()
        {
            await ReplyConfirmationAsync("reset_guild_xp_confirmation");
            var input = await _is.NextMessageAsync((ShardedCommandContext) Context, timeout: TimeSpan.FromMinutes(1));
            if (input != null)
            {
                if (!string.Equals(input.Content, GetText("#bot_confirm"), StringComparison.InvariantCultureIgnoreCase))
                {
                    await Context.Channel.SendErrorMessageAsync(GetText("#bot_canceled"));
                    return;
                }

                using (var db = _db.GetDbContext())
                {
                    var xpSystemDb = db.XpSystem.Where(x => x.GuildId == Context.Guild.Id);
                    if (xpSystemDb.Any())
                    {
                        db.RemoveRange(xpSystemDb);
                        await db.SaveChangesAsync();
                    }

                    await ReplyConfirmationAsync("reset_guild_xp");
                }
            }
        }
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireOwner]
        [Priority(1)]
        public async Task RemoveGlobalExperienceAsync(int xp, [Remainder] IGuildUser user)
        {
            if (xp < 0)
            {
                return;
            }

            await RgXpAsync(xp, user);
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireOwner]
        [Priority(0)]
        public async Task RemoveGlobalExperienceAsync(int xp, [Remainder] string user)
        {
            if (xp < 0)
            {
                return;
            }

            IUser getUser;
            if (ulong.TryParse(user, out var id))
            {
                var restClient = ((DiscordShardedClient) Context.Client).GetShardFor(Context.Guild).Rest;
                getUser = await restClient.GetUserAsync(id);
            }
            else
            {
                var userSplit = user.Split("#");
                if (userSplit.Length == 2)
                    getUser = await Context.Client.GetUserAsync(userSplit[0], userSplit[1]);
                else
                    getUser = null;
            }

            if (getUser is null)
            {
                await ReplyErrorAsync("#bot_user_not_found");
                return;
            }

            await RgXpAsync(xp, getUser);
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireOwner]
        [Priority(1)]
        public async Task SetGlobalLevelAsync(int level, [Remainder] IGuildUser user)
        {
            if (level < 0) return;
            
            await SgLvlAsync(level, user);
        }
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireOwner]
        [Priority(0)]
        public async Task SetGlobalLevelAsync(int level, [Remainder] string user)
        {
            if (level < 0) return;
            
            IUser getUser;
            if (ulong.TryParse(user, out var id))
            {
                var restClient = ((DiscordShardedClient) Context.Client).GetShardFor(Context.Guild).Rest;
                getUser = await restClient.GetUserAsync(id);
            }
            else
            {
                var userSplit = user.Split("#");
                if (userSplit.Length == 2)
                    getUser = await Context.Client.GetUserAsync(userSplit[0], userSplit[1]);
                else
                    getUser = null;
            }

            if (getUser is null)
            {
                await ReplyErrorAsync("#bot_user_not_found");
                return;
            }

            await SgLvlAsync(level, getUser);
        }

        private async Task RgXpAsync(int xp, IUser user)
        {
            using (var db = _db.GetDbContext())
            {
                var xpDb = db.Users.FirstOrDefault(x => x.UserId == user.Id);
                if (xpDb is null)
                {
                    await ReplyErrorAsync("user_no_global_xp");
                    return;
                }

                if (xp > xpDb.Xp)
                {
                    await ReplyErrorAsync("user_remove_global_xp_exceed", user);
                    return;
                }

                var level = 0;
                xpDb.Xp -= xp;
                var xpSum = xpDb.Xp * 2 / 30;
                while ((level + 1) * (level + 2) <= xpSum)
                {
                    level++;
                }

                xpDb.Level = level;
            

                await db.SaveChangesAsync();
                await ReplyConfirmationAsync("user_remove_global_xp", user, level, xpDb.Xp);
            }
        }

        private async Task SgLvlAsync(int level, IUser user)
        {
            using (var db = _db.GetDbContext())
            {
                var xpDb = db.Users.FirstOrDefault(x => x.UserId == user.Id);
                if (xpDb is null)
                {
                    await ReplyErrorAsync("user_no_global_xp");
                    return;
                }
            
                xpDb.Level = level;
                xpDb.Xp = 30 * level * (level + 1) / 2;

                await db.SaveChangesAsync();
                await ReplyConfirmationAsync("user_remove_global_xp", user, level, xpDb.Xp);
            }
        }
    }
}