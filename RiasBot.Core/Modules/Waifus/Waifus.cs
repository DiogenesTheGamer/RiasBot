using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Searches.Services;
using RiasBot.Services;
using DBModels = RiasBot.Database.Models;

namespace RiasBot.Modules.Waifus
{
    public class Waifus : RiasModule
    {
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly InteractiveService _is;
        private readonly CommandHandler _ch;
        private readonly AnimeService _animeService;

        public Waifus(IBotCredentials creds, DbService db, InteractiveService iss,
            CommandHandler ch, AnimeService animeService)
        {
            _creds = creds;
            _db = db;
            _is = iss;
            _ch = ch;
            _animeService = animeService;
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task ClaimWaifuAsync([Remainder] string name)
        {
            var waifu = await _animeService.GetCharacterAsync(name);
            if (waifu is null)
            {
                await ReplyErrorAsync("character_not_found");
                return;
            }

            var firstName = waifu.Name.First;
            var lastName = waifu.Name.Last;

            string waifuName;
            if (string.IsNullOrEmpty(firstName))
                waifuName = lastName.Trim();
            else if (string.IsNullOrEmpty(lastName))
                waifuName = firstName.Trim();
            else
                waifuName = $"{firstName.Trim()} {lastName.Trim()}";

            var waifuImage = waifu.Image.Large;

            using (var db = _db.GetDbContext())
            {
                var userDb = db.Users.FirstOrDefault(x => x.UserId == Context.User.Id);
                var waifuDb = db.Waifus.Where(x => x.UserId == Context.User.Id);
                var waifus = db.Waifus.Where(x => x.WaifuId == waifu.Id);

                var waifuPrice = 1000;
                if (waifus.Any())
                {
                    waifuPrice += waifus.Count() * 10;
                }

                var claimCanceled = false;

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithUrl(waifu.SiteUrl)
                    .WithTitle(waifuName);

                if (waifuDb.Any(x => x.WaifuId == waifu.Id))
                {
                    embed.WithDescription(GetText("has_waifu"));
                    claimCanceled = true;
                }
                else
                {
                    if (userDb is null || userDb.Currency < waifuPrice)
                    {
                        embed.WithDescription(GetText("claim_currency_not_enough", _creds.Currency));
                        claimCanceled = true;
                    }
                    else
                    {
                        embed.WithDescription(GetText("claim_confirmation"));
                    }
                }
                
                embed.AddField(GetText("claimed_by"),
                        $"{waifus.Count()} {GetText("#bot_users").ToLowerInvariant()}", true)
                    .AddField(GetText("#utility_price"), waifuPrice, true)
                    .WithThumbnailUrl(waifuImage);

                await Context.Channel.SendMessageAsync(Format.Bold(GetText("claim_note", _ch.GetPrefix(Context.Guild))),
                    embed: embed.Build());

                if (claimCanceled) return;

                var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromMinutes(1));
                if (input != null)
                {
                    if (!string.Equals(input.Content, GetText("#bot_confirm"), StringComparison.InvariantCultureIgnoreCase))
                    {
                        await Context.Channel.SendErrorMessageAsync(GetText("claim_canceled"));
                        return;
                    }

                    userDb.Currency -= waifuPrice;
                    var newWaifu = new DBModels.Waifus
                    {
                        UserId = Context.User.Id,
                        WaifuId = waifu.Id,
                        WaifuName = waifuName,
                        WaifuUrl = waifu.SiteUrl,
                        WaifuImage = waifuImage,
                        WaifuPrice = waifuPrice
                    };
                    await db.AddAsync(newWaifu);
                    await db.SaveChangesAsync();

                    embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                    embed.WithDescription(GetText("waifu_claimed", waifuName, waifuPrice, _creds.Currency));
                    embed.WithThumbnailUrl(waifuImage);
                    await Context.Channel.SendMessageAsync(embed: embed.Build());
                }
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task BelovedWaifuAsync([Remainder] string name)
        {
            using (var db = _db.GetDbContext())
            {
                var userDb = db.Users.FirstOrDefault(u => u.UserId == Context.User.Id);

                if (userDb is null || userDb.Currency < 5000)
                {
                    await ReplyErrorAsync("#gambling_currency_not_enough_required", _creds.Currency, 5000);
                    return;
                }

                var waifusDb = db.Waifus.Where(w => w.UserId == Context.User.Id);
                if (!waifusDb.Any())
                {
                    await ReplyErrorAsync("no_waifus");
                    return;
                }

                var waifus = int.TryParse(name, out var waifuId)
                    ? waifusDb.Where(w => w.WaifuId == waifuId)
                    : FilterWaifus(waifusDb, name);

                if (!waifus.Any())
                {
                    await ReplyConfirmationAsync("waifu_not_found");
                    return;
                }

                if (waifus.Count() > 1)
                {
                    var pager = new PaginatedMessage
                    {
                        Title = GetText("multiple_waifus_found", name),
                        Color = new Color(_creds.ConfirmColor),
                        Pages = waifus.Select((w, i) => $"#{i+1} {w.WaifuName} | {GetText("#administration_id")}: {w.WaifuId}"),
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

                var waifu = waifus.First();
                if (waifu.IsPrimary)
                {
                    await ReplyErrorAsync("waifu_already_beloved", waifu.WaifuName);
                    return;
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithUrl(waifu.WaifuUrl)
                    .WithTitle(waifu.WaifuName)
                    .WithDescription(GetText("beloved_waifu_confirmation", waifu.WaifuName))
                    .WithThumbnailUrl(waifu.WaifuImage);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
                var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromMinutes(1));
                if (input != null)
                {
                    if (!string.Equals(input.Content, GetText("#bot_confirm"), StringComparison.InvariantCultureIgnoreCase))
                    {
                        await Context.Channel.SendErrorMessageAsync(GetText("#bot_canceled"));
                        return;
                    }

                    var lastPrimaryWaifu = waifusDb.FirstOrDefault(x => x.IsPrimary);
                    if (lastPrimaryWaifu != null)
                    {
                        lastPrimaryWaifu.IsPrimary = false;
                        lastPrimaryWaifu.BelovedWaifuImage = null;
                    }

                    waifu.IsPrimary = true;
                    userDb.Currency -= 5000;

                    await db.SaveChangesAsync();
                    await ReplyConfirmationAsync("waifu_beloved", waifu.WaifuName);
                }
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task DivorceAsync([Remainder] string name)
        {
            using (var db = _db.GetDbContext())
            {
                var waifusDb = db.Waifus.Where(w => w.UserId == Context.User.Id);
                if (!waifusDb.Any())
                {
                    await ReplyErrorAsync("no_waifus");
                    return;
                }

                var waifus = int.TryParse(name, out var waifuId)
                    ? waifusDb.Where(w => w.WaifuId == waifuId)
                    : FilterWaifus(waifusDb, name);

                if (!waifus.Any())
                {
                    await ReplyConfirmationAsync("waifu_not_found");
                    return;
                }

                if (waifus.Count() > 1)
                {
                    var pager = new PaginatedMessage
                    {
                        Title = GetText("multiple_waifus_found", name),
                        Color = new Color(_creds.ConfirmColor),
                        Pages = waifus.Select((w, i) => $"#{i+1} {w.WaifuName} | {GetText("#administration_id")}: {w.WaifuId}"),
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

                var waifu = waifus.First();

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithUrl(waifu.WaifuUrl)
                    .WithTitle(waifu.WaifuName)
                    .WithDescription(GetText("divorce_confirmation", waifu.WaifuName))
                    .WithThumbnailUrl(waifu.WaifuImage);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
                var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromMinutes(1));
                if (input != null)
                {
                    if (!string.Equals(input.Content, GetText("#bot_confirm"), StringComparison.InvariantCultureIgnoreCase))
                    {
                        await Context.Channel.SendErrorMessageAsync(GetText("#bot_canceled"));
                        return;
                    }

                    db.Remove(waifu);
                    await db.SaveChangesAsync();
                    await ReplyConfirmationAsync("waifu_divorced", waifu.WaifuName);
                }
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task WaifusAsync([Remainder] IUser user = null)
        {
            user = user ?? Context.User;

            using (var db = _db.GetDbContext())
            {
                var waifusDb = db.Waifus.Where(w => w.UserId == user.Id).ToList();

                if (!waifusDb.Any())
                {
                    await ReplyErrorAsync("no_waifus");
                    return;
                }

                var waifusList = waifusDb.OrderByDescending(x => x.IsPrimary).ThenBy(y => y.WaifuName).Select((w, i) =>
                {
                    var str = $"#{i+1} {{0}} [{w.WaifuName}]({w.WaifuUrl}) | " +
                              $"{GetText("#administration_id")}: {w.WaifuId} | " +
                              $"{GetText("#utility_price")}: {w.WaifuPrice} {_creds.Currency}";

                    return string.Format(str, w.IsPrimary ? "❤" : "");
                });

                var pager = new PaginatedMessage
                {
                    Title = user.Id == Context.User.Id ? GetText("your_waifus") : GetText("waifus_list", user),
                    Color = new Color(_creds.ConfirmColor),
                    Pages = waifusList,
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

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task BelovedWaifuAvatarAsync(string url)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                await ReplyErrorAsync("#utility_url_not_valid");
                return;
            }
            if (!url.Contains("https"))
            {
                await ReplyErrorAsync("#utility_url_not_https");
                return;

            }
            if (!url.Contains(".png") && !url.Contains(".jpg") && !url.Contains(".jpeg"))
            {
                await ReplyErrorAsync("#utility_url_not_png_jpg");
                return;
            }

            var db = _db.GetDbContext();
            var waifusDb = db.Waifus.Where(w => w.UserId == Context.User.Id);
            if (!waifusDb.Any())
            {
                await ReplyErrorAsync("no_waifus");
                return;
            }

            var belovedWaifu = waifusDb.FirstOrDefault(w => w.IsPrimary);
            if (belovedWaifu is null)
            {
                await ReplyErrorAsync("no_beloved_waifu");
                return;
            }

            belovedWaifu.BelovedWaifuImage = url;
            await db.SaveChangesAsync();

            await ReplyConfirmationAsync("beloved_waifu_avatar_changed", belovedWaifu.WaifuName);
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task CreateWaifuAsync(string url, [Remainder] string name)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                await ReplyErrorAsync("#utility_url_not_valid");
                return;
            }
            if (!url.Contains("https"))
            {
                await ReplyErrorAsync("#utility_url_not_https");
                return;

            }
            if (!url.Contains(".png") && !url.Contains(".jpg") && !url.Contains(".jpeg"))
            {
                await ReplyErrorAsync("#utility_url_not_png_jpg");
                return;
            }

            var db = _db.GetDbContext();
            var userDb = db.Users.FirstOrDefault(u => u.UserId == Context.User.Id);

            if (userDb is null || userDb.Currency < 10000)
            {
                await ReplyErrorAsync("#gambling_currency_not_enough_required", _creds.Currency, 10000);
                return;
            }

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                .WithDescription(GetText("create_waifu_confirmation", name))
                .WithThumbnailUrl(url);

            await Context.Channel.SendMessageAsync(embed: embed.Build());
            var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromMinutes(1));
            if (input != null)
            {
                if (!string.Equals(input.Content, GetText("#bot_confirm"), StringComparison.InvariantCultureIgnoreCase))
                {
                    await Context.Channel.SendErrorMessageAsync(GetText("#bot_canceled"));
                    return;
                }

                var waifusDb = db.Waifus.Where(x => x.UserId == Context.User.Id);
                var lastPrimaryWaifu = waifusDb.FirstOrDefault(x => x.IsPrimary);
                if (lastPrimaryWaifu != null)
                {
                    lastPrimaryWaifu.IsPrimary = false;
                    lastPrimaryWaifu.BelovedWaifuImage = null;
                }

                var waifu = new DBModels.Waifus { UserId = Context.User.Id, WaifuName = name, WaifuImage = url, WaifuPrice = 10000, IsPrimary = true };
                await db.AddAsync(waifu);
                userDb.Currency -= 10000;

                await db.SaveChangesAsync();

                embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithDescription(GetText("waifu_created", name))
                    .WithThumbnailUrl(url);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }
        }

        private IQueryable<DBModels.Waifus> FilterWaifus(IEnumerable<DBModels.Waifus> waifus, string fullName)
        {
            var waifusList = waifus.ToList();
            foreach (var name in fullName.Split(" ", StringSplitOptions.RemoveEmptyEntries))
            {
                waifusList.RemoveAll(w => !w.WaifuName.Contains(name, StringComparison.InvariantCultureIgnoreCase));
            }

            return waifusList.AsQueryable();
        }
    }
}