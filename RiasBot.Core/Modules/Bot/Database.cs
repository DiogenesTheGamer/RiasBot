using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Services;
using RiasBot.Database.Models;

namespace RiasBot.Modules.Bot
{
    public partial class Bot
    {
        public class Database : RiasSubmodule
        {
            private readonly IBotCredentials _creds;
            private readonly DbService _db;
            private readonly InteractiveService _is;

            public Database(IBotCredentials creds, DbService db, InteractiveService ins)
            {
                _creds = creds;
                _db = db;
                _is = ins;
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task DeleteAsync([Remainder]string user)
            {
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
                    await ReplyErrorAsync("user_not_found");
                    return;
                }

                if (getUser.Id == _creds.MasterId)
                {
                    await ReplyErrorAsync("no_delete_master");
                    return;
                }

                await ReplyConfirmationAsync("user_db_delete");
                var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromSeconds(30));
                if (input != null)
                {
                    if (input.Content.Equals(GetText("yes"), StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (var db = _db.GetDbContext())
                        {
                            var userDb = db.Users.FirstOrDefault(x => x.UserId == getUser.Id);
                            if (userDb != null)
                            {
                                db.Remove(userDb);
                            }
                            var waifusDb = db.Waifus.Where(x => x.UserId == getUser.Id);
                            db.RemoveRange(waifusDb);

                            var profileDb = db.Profile.FirstOrDefault(x => x.UserId == getUser.Id);
                            if (profileDb != null)
                            {
                                db.Remove(profileDb);
                            }

                            await ReplyConfirmationAsync("user_db_deleted", getUser);
                            await db.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        await ReplyErrorAsync("canceled");
                    }
                }
                else
                {
                    await ReplyErrorAsync("canceled");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task DbAsync([Remainder]string user)
            {
                IUser getUser;
                if (ulong.TryParse(user, out var id))
                {
                    getUser = await Context.Client.GetUserAsync(id);
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
                    await ReplyErrorAsync("user_not_found");
                    return;
                }

                var mutualGuilds = ((SocketUser) getUser).MutualGuilds;

                using (var db = _db.GetDbContext())
                {
                    var userDb = db.Users.FirstOrDefault(x => x.UserId == getUser.Id);

                    if (userDb is null)
                    {
                        await ReplyErrorAsync("db_user_not_found");
                        return;
                    }

                    var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                        .WithAuthor(getUser.ToString())
                        .AddField(GetText("#administration_id"), getUser.Id, true)
                        .AddField(GetText("#gambling_currency"), $"{userDb.Currency} {_creds.Currency}", true)
                        .AddField(GetText("#xp_global_level"), userDb.Level, true)
                        .AddField(GetText("#xp_global_xp"), userDb.Xp, true)
                        .AddField(GetText("is_blacklisted"), userDb.IsBlacklisted, true)
                        .AddField(GetText("is_banned"), userDb.IsBanned, true)
                        .AddField(GetText("mutual_guilds"), mutualGuilds.Count, true)
                        .WithImageUrl(getUser.GetRealAvatarUrl());

                    await Context.Channel.SendMessageAsync(embed: embed.Build());
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task BlacklistAsync([Remainder]string user)
            {
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
                    await ReplyErrorAsync("user_not_found");
                    return;
                }

                if (getUser.Id == _creds.MasterId)
                {
                    await ReplyErrorAsync("no_blacklist_master");
                    return;
                }

                await ReplyConfirmationAsync("add_user_blacklist");
                var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromSeconds(30));
                if (input != null)
                {
                    if (input.Content.Equals(GetText("yes"), StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (var db = _db.GetDbContext())
                        {
                            var userDb = db.Users.FirstOrDefault(x => x.UserId == getUser.Id);
                            if (userDb != null)
                            {
                                userDb.IsBlacklisted = true;
                            }
                            else
                            {
                                var userConfig = new UserConfig { UserId = getUser.Id, IsBlacklisted = true };
                                await db.AddAsync(userConfig);
                            }

                            await ReplyConfirmationAsync("user_blacklisted", getUser);
                            await db.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        await ReplyErrorAsync("canceled");
                    }
                }
                else
                {
                    await ReplyErrorAsync("canceled");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task RemoveBlacklistAsync([Remainder]string user)
            {
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
                    await ReplyErrorAsync("user_not_found");
                    return;
                }

                if (getUser.Id == _creds.MasterId)
                {
                    await ReplyErrorAsync("not_blacklisted_master");
                    return;
                }

                await ReplyConfirmationAsync("remove_user_blacklist");
                var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromSeconds(30));
                if (input != null)
                {
                    if (input.Content.Equals(GetText("yes"), StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (var db = _db.GetDbContext())
                        {
                            var userDb = db.Users.FirstOrDefault(x => x.UserId == id);
                            if (userDb != null)
                            {
                                userDb.IsBlacklisted = false;
                            }
                            else
                            {
                                var userConfig = new UserConfig { UserId = getUser.Id, IsBlacklisted = false };
                                await db.AddAsync(userConfig);
                            }

                            await ReplyConfirmationAsync("user_not_blacklisted", getUser);
                            await db.SaveChangesAsync(); 
                        }
                    }
                    else
                    {
                        await ReplyErrorAsync("canceled");
                    }
                }
                else
                {
                    await ReplyErrorAsync("canceled");
                }
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task BotBanAsync([Remainder]string user)
            {
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
                    await ReplyErrorAsync("user_not_found");
                    return;
                }
                
                if (getUser.Id == _creds.MasterId)
                {
                    await ReplyErrorAsync("no_botban_master");
                    return;
                }

                await ReplyConfirmationAsync("add_botban_user");
                var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromSeconds(30));
                if (input != null)
                {
                    if (input.Content.Equals(GetText("yes"), StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (var db = _db.GetDbContext())
                        {
                            var userDb = db.Users.FirstOrDefault(x => x.UserId == getUser.Id);
                            if (userDb != null)
                            {
                                userDb.IsBlacklisted = true;
                                userDb.IsBanned = true;
                            }
                            else
                            {
                                var userConfig = new UserConfig { UserId = getUser.Id, IsBlacklisted = true, IsBanned = true };
                                await db.AddAsync(userConfig);
                            }

                            await ReplyConfirmationAsync("user_botbanned", getUser);
                            await db.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        await ReplyErrorAsync("canceled");
                    }
                }
                else
                {
                    await ReplyErrorAsync("canceled");
                }
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task RemoveBotBanAsync([Remainder]string user)
            {
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
                    await ReplyErrorAsync("user_not_found");
                    return;
                }
                
                if (getUser.Id == _creds.MasterId)
                {
                    await ReplyErrorAsync("not_botbanned_master");
                    return;
                }

                await ReplyConfirmationAsync("remove_botban_user");
                var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromSeconds(30));
                if (input != null)
                {
                    if (input.Content.Equals(GetText("yes"), StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (var db = _db.GetDbContext())
                        {
                            var userDb = db.Users.FirstOrDefault(x => x.UserId == getUser.Id);
                            if (userDb != null)
                            {
                                userDb.IsBanned = false;
                            }
                            else
                            {
                                var userConfig = new UserConfig { UserId = getUser.Id, IsBanned = false };
                                await db.AddAsync(userConfig);
                            }

                            await ReplyConfirmationAsync("user_not_botbanned", getUser);
                            await db.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        await ReplyErrorAsync("canceled");
                    }
                }
                else
                {
                    await ReplyErrorAsync("canceled");
                }
            }
        }
    }
}