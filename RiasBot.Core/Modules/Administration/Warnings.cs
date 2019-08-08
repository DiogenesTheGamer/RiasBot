using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Administration.Services;
using RiasBot.Services;
using RiasBot.Database.Models;

namespace RiasBot.Modules.Administration
{
    public partial class Administration
    {
        public class Warnings : RiasSubmodule<WarningsService>
        {
            private readonly CommandHandler _ch;
            private readonly IBotCredentials _creds;
            private readonly DbService _db;
            private readonly InteractiveService _is;
            private readonly MuteService _muteService;

            public Warnings(CommandHandler ch, IBotCredentials creds, DbService db, InteractiveService ins, MuteService muteService)
            {
                _ch = ch;
                _creds = creds;
                _db = db;
                _is = ins;
                _muteService = muteService;
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.KickMembers | GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.KickMembers | GuildPermission.BanMembers)]
            [RequireContext(ContextType.Guild)]
            public async Task WarningAsync(IGuildUser user, [Remainder]string reason = null)
            {
                if (user.Id == Context.User.Id)
                    return;
                if (user.Id == Context.Guild.OwnerId)
                {
                    await ReplyErrorAsync("cannot_warn_owner");
                    return;
                }

                if ((await Context.Guild.GetCurrentUserAsync()).CheckHierarchy(user) <= 0)
                {
                    await ReplyErrorAsync("user_above");
                    return;
                }

                var punishment = await Service.WarnUserAsync(Context.Guild, (IGuildUser) Context.User, user, Context.Channel,
                    Context.Message, reason);

                if (string.IsNullOrEmpty(punishment))
                    return;

                switch (punishment)
                {
                    case "mute":
                        await _muteService.MuteUserAsync(Context.Guild, (IGuildUser) Context.User, user,
                            Context.Channel, GetText("warn_mute"));
                        break;
                    case "kick":
                        await SendMessageAsync(user, "warn_kick", "kicked_from", reason);
                        await user.KickAsync();
                        break;
                    case "ban":
                        await SendMessageAsync(user, "warn_ban", "banned_from", reason);
                        await Context.Guild.AddBanAsync(user);
                        break;
                    case "softban":
                        await SendMessageAsync(user, "warn_soft_ban", "kicked_from", reason);
                        await Context.Guild.AddBanAsync(user, 7);
                        await Context.Guild.RemoveBanAsync(user);
                        break;
                    case "pruneban":
                        await SendMessageAsync(user, "warn_prune_ban", "banned_from", reason);
                        await Context.Guild.AddBanAsync(user, 7);
                        break;
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task WarningListAsync()
            {
                using (var db = _db.GetDbContext())
                {
                    var warnings = db.Warnings.Where(w => w.GuildId == Context.Guild.Id)
                        .GroupBy(u => u.UserId)
                        .Select(x => x.FirstOrDefault())
                        .ToList();

                    if (warnings.Count == 0)
                    {
                        await ReplyConfirmationAsync("no_warned_users");
                    }
                    else
                    {
                        var index = 0;
                        var warnUsers = new List<string>();
                        foreach (var warn in warnings)
                        {
                            var user = await Context.Guild.GetUserAsync(warnings[index].UserId);
                            if (user != null)
                            {
                                warnUsers.Add($"{index + 1}. {user} | {user.Id}");
                                index++;
                            }
                            else
                            {
                                db.Remove(warn);
                            }
                        }
                        await db.SaveChangesAsync();
                        if (warnUsers.Any(x => !string.IsNullOrEmpty(x)))
                        {
                            var pager = new PaginatedMessage
                            {
                                Title = GetText("warned_users"),
                                Color = new Color(_creds.ConfirmColor),
                                Pages = warnUsers,
                                Options = new PaginatedAppearanceOptions
                                {
                                    ItemsPerPage = 15,
                                    Timeout = TimeSpan.FromMinutes(1),
                                    DisplayInformationIcon = false,
                                    JumpDisplayOptions = JumpDisplayOptions.Never,
                                    FooterFormat = "Page {0}/{1} | " + GetText("warn_list_footer", _ch.GetPrefix(Context.Guild))
                                }
                            };

                            await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
                        }
                        else
                        {
                            await ReplyConfirmationAsync("no_warned_users");
                        }
                    }
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task WarningLogAsync([Remainder]IGuildUser user)
            {
                using (var db = _db.GetDbContext())
                {
                    var warnings = db.Warnings.Where(x => x.GuildId == Context.Guild.Id);
                    var warningsUser = warnings.Where(x => x.UserId == user.Id).ToList();

                    var reasons = new List<string>();
                    for (var i = 0; i < warningsUser.Count; i++)
                    {
                        var moderator = await Context.Guild.GetUserAsync(warningsUser[i].Moderator);
                        reasons.Add($"#{i+1} {warningsUser[i].Reason ?? "-"}\n" +
                                    $"{Format.Bold(GetText("moderator").ToLowerInvariant())} " +
                                    $"{moderator}\n");
                    }

                    if (warningsUser.Count == 0)
                    {
                        await ReplyConfirmationAsync("user_no_warn", user);
                    }
                    else
                    {
                        var pager = new PaginatedMessage
                        {
                            Title = GetText("user_all_warns", user),
                            Color = new Color(_creds.ConfirmColor),
                            Pages = reasons,
                            Options = new PaginatedAppearanceOptions
                            {
                                ItemsPerPage = 5,
                                Timeout = TimeSpan.FromMinutes(1),
                                DisplayInformationIcon = false,
                                JumpDisplayOptions = JumpDisplayOptions.Never
                            }

                        };
                        await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
                    }
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task WarningClearAsync(IGuildUser user, string number)
            {
                using (var db = _db.GetDbContext())
                {
                    var warnings = db.Warnings.Where(g => g.GuildId == Context.Guild.Id).Where(u => u.UserId == user.Id).ToList();

                    if (warnings.Count == 0)
                    {
                        await ReplyErrorAsync("user_no_warn", user);
                        return;
                    }

                    if (int.TryParse(number, out var index))
                    {
                        if (index < warnings.Count)
                        {
                            db.Remove(warnings[index]);
                            await db.SaveChangesAsync();

                            await ReplyConfirmationAsync("warn_removed", user);
                        }
                    }
                    else
                    {
                        if (string.Equals(number, "all", StringComparison.InvariantCultureIgnoreCase))
                        {
                            db.RemoveRange(warnings);
                            await db.SaveChangesAsync();

                            await ReplyConfirmationAsync("all_warns_removed", user);
                        }
                    }
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task WarningPunishmentAsync(int warns, [Remainder]string punishment = null)
            {
                if (warns < 0)
                {
                    return;
                }

                if (warns > 10)
                {
                    await ReplyErrorAsync("warn_punishment_limit", 10);
                    return;
                }

                if (string.IsNullOrEmpty(punishment))
                {
                    if (warns > 0)
                    {
                        await ReplyErrorAsync("incorrect_punishment");
                        return;
                    }

                    using (var db = _db.GetDbContext())
                    {
                        var warnings = db.Guilds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
                        if (warnings != null)
                        {
                            warnings.WarnsPunishment = 0;
                            warnings.PunishmentMethod = null;
                            await db.SaveChangesAsync();
                        }
                    }

                    await ReplyConfirmationAsync("no_warn_punishment_set");
                }
                else
                {
                    switch (punishment.ToLowerInvariant())
                    {
                        case "mute":
                        case "m":
                            await RegisterPunishmentAsync(Context.Guild, warns, "mute");
                            break;
                        case "kick":
                        case "k":
                            await RegisterPunishmentAsync(Context.Guild, warns, "kick");
                            break;
                        case "ban":
                        case "b":
                            await RegisterPunishmentAsync(Context.Guild, warns, "ban");
                            break;
                        case "softban":
                        case "sb":
                            await RegisterPunishmentAsync(Context.Guild, warns, "softban");
                            break;
                        case "pruneban":
                        case "pb":
                            await RegisterPunishmentAsync(Context.Guild, warns, "pruneban");
                            break;
                        default:
                            await ReplyErrorAsync("incorrect_punishment");
                            break;
                    }
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task WarningPunishmentAsync()
            {
                using (var db = _db.GetDbContext())
                {
                    var warnings = db.Guilds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
                    var warns = warnings?.WarnsPunishment ?? 0;
                    var punish = warnings?.PunishmentMethod;

                    if (warns > 0 && !string.IsNullOrEmpty(punish))
                    {
                        var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                            .WithTitle(GetText("warn_punishment_title"))
                            .WithDescription(GetText("warn_punishment", warns, punish));

                        await Context.Channel.SendMessageAsync(embed: embed.Build());
                    }
                    else
                        await ReplyErrorAsync("no_warn_punishment");
                }
            }

            private async Task SendMessageAsync(IGuildUser user, string moderationType, string fromWhere, string warnReason)
            {
                using (var db = _db.GetDbContext())
                {
                    var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);

                    var embed = new EmbedBuilder().WithColor(_creds.ErrorColor)
                        .WithTitle(GetText(moderationType))
                        .AddField(GetText("user"), user, true)
                        .AddField(GetText("id"), user.Id.ToString(), true)
                        .AddField(GetText("moderator"), Context.User, true)
                        .AddField(GetText("reason"), moderationType, true)
                        .WithThumbnailUrl(user.GetRealAvatarUrl());

                    if (guildDb != null)
                    {
                        var modlog = await Context.Guild.GetTextChannelAsync(guildDb.ModLogChannel);
                        if (modlog != null)
                        {
                            var currentUser = await Context.Guild.GetCurrentUserAsync();
                            var preconditions = currentUser.GetPermissions(modlog);
                            if (preconditions.ViewChannel && preconditions.SendMessages)
                            {
                                await Context.Message.AddReactionAsync(new Emoji("✅"));
                                await modlog.SendMessageAsync(embed: embed.Build());
                            }
                            else
                            {
                                await Context.Channel.SendMessageAsync(embed: embed.Build());
                            }
                        }
                        else
                        {
                            await Context.Channel.SendMessageAsync(embed: embed.Build());
                        }
                    }
                    else
                    {
                        await Context.Channel.SendMessageAsync(embed: embed.Build());
                    }

                    if (string.IsNullOrEmpty(fromWhere))
                        return;

                    var reasonEmbed = new EmbedBuilder().WithColor(_creds.ErrorColor)
                        .WithDescription(GetText(fromWhere, Context.Guild.Name));

                    if (!string.IsNullOrEmpty(warnReason))
                        reasonEmbed.AddField(GetText("reason"), warnReason);

                    try
                    {
                        if (!user.IsBot)
                            await user.SendMessageAsync("", embed: reasonEmbed.Build());
                    }
                    catch
                    {
                        // the user blocked the messages from the guild users
                    }
                }
            }

            private async Task RegisterPunishmentAsync(IGuild guild, int warns, string punishment)
            {
                using (var db = _db.GetDbContext())
                {
                    var warnings = db.Guilds.FirstOrDefault(x => x.GuildId == guild.Id);
                    if (warnings != null)
                    {
                        warnings.WarnsPunishment = warns;
                        warnings.PunishmentMethod = punishment;
                    }
                    else
                    {
                        var warningPunishment = new GuildConfig
                        {
                            GuildId = guild.Id,
                            WarnsPunishment = warns,
                            PunishmentMethod = punishment
                        };
                        await db.AddAsync(warningPunishment);
                    }

                    await db.SaveChangesAsync();
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithTitle(GetText("warn_punishment_set"))
                    .WithDescription(GetText("warn_punishment", warns, punishment));

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }
        }
    }
}