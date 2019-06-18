using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Services;
using RiasBot.Database.Models;

namespace RiasBot.Modules.Gambling
{
    public partial class Gambling
    {
        public class Currency : RiasSubmodule
        {
            private readonly IBotCredentials _creds;
            private readonly DbService _db;

            public Currency(IBotCredentials creds, DbService db)
            {
                _creds = creds;
                _db = db;
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireOwner]
            [Priority(1)]
            public async Task RewardAsync(int amount, [Remainder]IGuildUser user)
            {
                if (amount < 1)
                    return;

                using (var db = _db.GetDbContext())
                {
                    var userDb = db.Users.FirstOrDefault(x => x.UserId == user.Id);
                    if (userDb != null)
                    {
                        userDb.Currency += amount;
                    }
                    else
                    {
                        var currency = new UserConfig { UserId = user.Id, Currency = amount };
                        await db.Users.AddAsync(currency);
                    }

                    await db.SaveChangesAsync();
                }

                await ReplyConfirmationAsync("user_rewarded", user, amount, _creds.Currency);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            [Priority(0)]
            public async Task RewardAsync(int amount, [Remainder]string user)
            {
                if (amount < 1)
                    return;

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
                    return;

                using (var db = _db.GetDbContext())
                {
                    var userDb = db.Users.FirstOrDefault(x => x.UserId == getUser.Id);
                    if (userDb != null)
                    {
                        userDb.Currency += amount;
                    }
                    else
                    {
                        var currency = new UserConfig { UserId = getUser.Id, Currency = amount };
                        await db.Users.AddAsync(currency);
                    }

                    await db.SaveChangesAsync();
                }

                await ReplyConfirmationAsync("user_rewarded", user, amount, _creds.Currency);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireOwner]
            [Priority(1)]
            public async Task TakeAsync(int amount, [Remainder]IGuildUser user)
            {
                if (amount < 1)
                    return;

                var amountTook = 0;
                using (var db = _db.GetDbContext())
                {
                    var userDb = db.Users.FirstOrDefault(x => x.UserId == user.Id);
                    if (userDb != null)
                    {
                        if (userDb.Currency - amount < 0)
                        {
                            amountTook = userDb.Currency;
                            userDb.Currency = 0;
                        }
                        else
                        {
                            amountTook = amount;
                            userDb.Currency -= amount;
                        }
                    }
                    else
                    {
                        var currency = new UserConfig { UserId = user.Id, Currency = 0 };
                        await db.Users.AddAsync(currency);
                    }

                    await db.SaveChangesAsync();
                }

                await ReplyConfirmationAsync("user_took", user, amountTook, _creds.Currency);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            [Priority(0)]
            public async Task TakeAsync(int amount, [Remainder]string user)
            {
                if (amount < 1)
                    return;

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
                    return;

                var amountTook = 0;
                using (var db = _db.GetDbContext())
                {
                    var userDb = db.Users.FirstOrDefault(x => x.UserId == getUser.Id);
                    if (userDb != null)
                    {
                        if (userDb.Currency - amount < 0)
                        {
                            amountTook = userDb.Currency;
                            userDb.Currency = 0;
                        }
                        else
                        {
                            amountTook = amount;
                            userDb.Currency -= amount;
                        }
                    }
                    else
                    {
                        var currency = new UserConfig { UserId = getUser.Id, Currency = 0 };
                        await db.Users.AddAsync(currency);
                    }

                    await db.SaveChangesAsync();
                }

                await ReplyConfirmationAsync("user_took", user, amountTook, _creds.Currency);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            public async Task CashAsync([Remainder]IUser user = null)
            {
                user = user ?? Context.User;
                
                var currency = 0;

                using (var db = _db.GetDbContext())
                {
                    var userDb = db.Users.FirstOrDefault(x => x.UserId == user.Id);
                    if (userDb != null)
                    {
                        currency = userDb.Currency;
                    }
                    else
                    {
                        var currencyDb = new UserConfig { UserId = user.Id, Currency = 0 };
                        await db.Users.AddAsync(currencyDb);
                        await db.SaveChangesAsync();
                    }
                }

                if (user.Id == Context.User.Id)
                    await ReplyConfirmationAsync("currency_you", currency, _creds.Currency);
                else
                    await ReplyConfirmationAsync("currency_user", user, currency, _creds.Currency);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RateLimit(1, 5, RateLimitType.GuildUser)]
            public async Task LeaderboardAsync(int page = 0)
            {
                if (page < 0) page = 0;

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                embed.WithTitle($"{_creds.Currency} {GetText("leaderboard")}");

                using (var db = _db.GetDbContext())
                {
                    var userDb = db.Users.OrderByDescending(x => x.Currency).Skip(page * 9).Take(9).ToList();

                    for (var i = 0; i < userDb.Count; i++)
                    {
                        var user = await Context.Client.GetUserAsync(userDb[i].UserId);
                        embed.AddField($"#{i + 1 + (page * 9)} {user?.ToString() ?? userDb[i].UserId.ToString()}", $"{userDb[i].Currency} {_creds.Currency}", true);
                    }

                    if (userDb.Count == 0)
                        embed.WithDescription(GetText("no_users_lb"));
                }

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task DailyAsync()
            {
                using (var db = _db.GetDbContext())
                {
                    var userDb = db.Users.FirstOrDefault(x => x.UserId == Context.User.Id);
                    var dailyDb = db.Dailies.FirstOrDefault(x => x.UserId == Context.User.Id);

                    var nextDateTimeDaily = DateTime.UtcNow.AddDays(1);

                    if (dailyDb != null)
                    {
                        if (DateTime.Compare(DateTime.UtcNow, dailyDb.NextDaily) >= 0)
                        {
                            dailyDb.NextDaily = nextDateTimeDaily;
                        }
                        else
                        {
                            var timeLeft = dailyDb.NextDaily.Subtract(DateTime.UtcNow);
                            await ReplyErrorAsync("daily_wait", timeLeft.FancyTimeSpanString());
                            return;
                        }
                    }
                    else
                    {
                        var newDailyDb = new Dailies { UserId = Context.User.Id, NextDaily = nextDateTimeDaily};
                        await db.AddAsync(newDailyDb);
                    }

                    if (userDb != null)
                    {
                        userDb.Currency += 100;
                    }
                    else
                    {
                        var newUserDb = new UserConfig {UserId = Context.User.Id, Currency = 100};
                        await db.AddAsync(newUserDb);
                    }
                    await db.SaveChangesAsync();
                }
                
                await ReplyConfirmationAsync("daily_received", 100, _creds.Currency);
            }
        }
    }
}

//TODO: Add vote info for currency and daily