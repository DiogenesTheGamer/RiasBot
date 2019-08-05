using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Services;

namespace RiasBot.Modules.Administration
{
    public partial class Administration
    {
        public class Moderation : RiasSubmodule
        {
            private readonly IBotCredentials _creds;
            private readonly DbService _db;

            public Moderation(IBotCredentials creds, DbService db)
            {
                _creds = creds;
                _db = db;
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.KickMembers)]
            [RequireBotPermission(GuildPermission.KickMembers)]
            public async Task KickAsync(IGuildUser user, [Remainder]string reason = null)
            {
                if (user.Id == Context.User.Id)
                    return;
                if (user.Id == Context.Guild.OwnerId)
                {
                    await ReplyErrorAsync("cannot_kick_owner");
                    return;
                }

                if (user.CheckHierarchy(await Context.Guild.GetCurrentUserAsync()))
                {
                    await ReplyErrorAsync("user_above");
                    return;
                }

                await SendMessageAsync(user, "user_kicked", "kicked_from", reason);
                await user.KickAsync();
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            public async Task BanAsync(IGuildUser user, [Remainder]string reason = null)
            {
                if (user.Id == Context.User.Id)
                    return;
                if (user.Id == Context.Guild.OwnerId)
                {
                    await ReplyErrorAsync("cannot_ban_owner");
                    return;
                }

                if (user.CheckHierarchy(await Context.Guild.GetCurrentUserAsync()))
                {
                    await ReplyErrorAsync("user_above");
                    return;
                }

                await SendMessageAsync(user, "user_banned", "banned_from", reason);
                await Context.Guild.AddBanAsync(user);
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.KickMembers)]
            [RequireBotPermission(GuildPermission.KickMembers | GuildPermission.BanMembers)]
            public async Task SoftBanAsync(IGuildUser user, [Remainder]string reason = null)
            {
                if (user.Id == Context.User.Id)
                    return;
                if (user.Id == Context.Guild.OwnerId)
                {
                    await ReplyErrorAsync("cannot_softban_owner");
                    return;
                }

                if (user.CheckHierarchy(await Context.Guild.GetCurrentUserAsync()))
                {
                    await ReplyErrorAsync("user_above");
                    return;
                }

                await SendMessageAsync(user, "user_soft_banned", "kicked_from", reason);
                await Context.Guild.AddBanAsync(user, 7);
                await Context.Guild.RemoveBanAsync(user);
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            public async Task PruneBanAsync(IGuildUser user, [Remainder]string reason = null)
            {
                if (user.Id == Context.User.Id)
                    return;
                if (user.Id == Context.Guild.OwnerId)
                {
                    await ReplyErrorAsync("cannot_pruneban_owner");
                    return;
                }

                if (user.CheckHierarchy(await Context.Guild.GetCurrentUserAsync()))
                {
                    await ReplyErrorAsync("user_above");
                    return;
                }

                await SendMessageAsync(user, "user_banned", "banned_from", reason);
                await Context.Guild.AddBanAsync(user, 7);
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(ChannelPermission.ManageMessages)]
            [RequireBotPermission(ChannelPermission.ManageMessages)]
            public async Task PruneAsync(int amount = 100)
            {
                var channel = (ITextChannel) Context.Channel;

                amount++;
                if (amount < 1)
                    return;
                if (amount > 100)
                    amount = 100;

                var messages = (await channel.GetMessagesAsync(amount).FlattenAsync()).Where(m => DateTimeOffset.UtcNow.Subtract(m.CreatedAt.ToUniversalTime()).Days < 14).ToList();
                if (messages.Any())
                {
                    await channel.DeleteMessagesAsync(messages);
                }
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageMessages)]
            [RequireBotPermission(GuildPermission.ManageMessages)]
            public async Task PruneAsync(IGuildUser user, int amount = 100)
            {
                var channel = (ITextChannel)Context.Channel;

                amount++;
                if (amount < 1)
                    return;

                if (amount > 100)
                    amount = 100;

                await Context.Message.DeleteAsync();
                var messages = (await channel.GetMessagesAsync().FlattenAsync()).Where(m => m.Author.Id == user.Id &&
                                                                                            DateTimeOffset.UtcNow.Subtract(m.CreatedAt.ToUniversalTime()).Days < 14)
                                                                                .Take(amount).ToList();
                if (messages.Any())
                {
                    await channel.DeleteMessagesAsync(messages);
                }
                else
                {
                    await ReplyErrorAsync("prune_limit");
                }
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageMessages)]
            [RequireBotPermission(GuildPermission.ManageMessages)]
            public async Task PruneAsync(int amount, IGuildUser user)
            {
                var channel = (ITextChannel)Context.Channel;

                amount++;
                if (amount < 1)
                    return;

                if (amount > 100)
                    amount = 100;

                await Context.Message.DeleteAsync();
                var messages = (await channel.GetMessagesAsync().FlattenAsync()).Where(m => m.Author.Id == user.Id &&
                                                                                            DateTimeOffset.UtcNow.Subtract(m.CreatedAt.ToUniversalTime()).Days < 14)
                                                                                .Take(amount).ToList();
                if (messages.Any())
                {
                    await channel.DeleteMessagesAsync(messages);
                }
                else
                {
                    await ReplyErrorAsync("prune_limit");
                }
            }

            private async Task SendMessageAsync(IGuildUser user, string moderationType, string fromWhere, string reason)
            {
                using (var db = _db.GetDbContext())
                {
                    var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);

                    var embed = new EmbedBuilder().WithColor(_creds.ErrorColor)
                        .WithTitle(GetText(moderationType))
                        .AddField(GetText("user"), user, true)
                        .AddField(GetText("id"), user.Id.ToString(), true)
                        .AddField(GetText("moderator"), Context.User, true)
                        .WithThumbnailUrl(user.GetRealAvatarUrl());

                    if (!string.IsNullOrEmpty(reason))
                        embed.AddField(GetText("reason"), reason);

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

                    var reasonEmbed = new EmbedBuilder().WithColor(_creds.ErrorColor)
                        .WithDescription(GetText(fromWhere, Context.Guild.Name));

                    if (!string.IsNullOrEmpty(reason))
                        reasonEmbed.AddField(GetText("reason"), reason);

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
        }
    }
}