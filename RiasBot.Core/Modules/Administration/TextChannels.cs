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
        public class TextChannels : RiasSubmodule
        {
            private readonly IBotCredentials _creds;

            public TextChannels(IBotCredentials creds)
            {
                _creds = creds;
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RequireContext(ContextType.Guild)]
            public async Task CreateTextChannelAsync([Remainder]string name)
            {
                if (name.Length < 1 || name.Length > 100)
                {
                    await ReplyErrorAsync("channel_name_length_limit");
                    return;
                }

                await Context.Guild.CreateTextChannelAsync(name);
                await ReplyConfirmationAsync("text_channel_created", name);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RequireContext(ContextType.Guild)]
            public async Task DeleteTextChannelAsync([Remainder]string name)
            {
                name = name.Replace(" ", "-");
                var channel = (await Context.Guild.GetTextChannelsAsync()).FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.InvariantCultureIgnoreCase)) ??
                              await ChannelsExtensions.GetTextChannelByIdAsync(Context.Guild, name);
                if (channel is null) return;

                var permissions = (await Context.Guild.GetCurrentUserAsync()).GetPermissions(channel);
                if (permissions.ViewChannel)
                {
                    await channel.DeleteAsync();
                    if (channel.Id != Context.Channel.Id)
                        await ReplyConfirmationAsync("text_channel_deleted", channel.Name);
                }
                else
                {
                    await ReplyErrorAsync("text_channel_no_permission_view");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RequireContext(ContextType.Guild)]
            public async Task RenameTextChannelAsync([Remainder]string names)
            {
                var namesSplit = names.Split("->");
                var oldName = namesSplit[0].TrimEnd().Replace(" ", "-");
                var newName = namesSplit[1].TrimStart();
                var channel = await ChannelsExtensions.GetTextChannelByIdAsync(Context.Guild, oldName) ??
                              (await Context.Guild.GetTextChannelsAsync())
                              .FirstOrDefault(x => string.Equals(x.Name, oldName, StringComparison.InvariantCultureIgnoreCase));
                if (channel != null)
                {
                    var permissions = (await Context.Guild.GetCurrentUserAsync()).GetPermissions(channel);
                    if (permissions.ViewChannel)
                    {
                        oldName = channel.Name;
                        await channel.ModifyAsync(x => x.Name = newName);
                        await ReplyConfirmationAsync("text_channel_renamed", oldName, channel.Name);
                    }
                    else
                    {
                        await ReplyErrorAsync("text_channel_no_permission_view");
                    }
                }
                else
                {
                    await ReplyErrorAsync("text_channel_not_found");
                }
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task ChannelTopicAsync()
            {
                var channel = (ITextChannel)Context.Channel;
                if (!string.IsNullOrEmpty(channel.Topic))
                {
                    var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                    embed.WithTitle(GetText("channel_topic_title"));
                    embed.WithDescription(channel.Topic);
                    await Context.Channel.SendMessageAsync(embed: embed.Build());
                }
                else
                {
                    await ReplyErrorAsync("channel_no_topic");
                }
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RequireContext(ContextType.Guild)]
            public async Task SetChannelTopicAsync([Remainder]string topic = null)
            {
                var channel = (ITextChannel)Context.Channel;
                await channel.ModifyAsync(x => x.Topic = topic);
                if (string.IsNullOrEmpty(topic))
                    await ReplyConfirmationAsync("channel_topic_removed");
                else
                {
                    await ReplyConfirmationAsync("channel_topic", topic);
                }
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RequireContext(ContextType.Guild)]
            public async Task SetNsfwChannelAsync([Remainder]ITextChannel channel = null)
            {
                if (channel is null)
                {
                    channel = (ITextChannel)Context.Channel;
                }
                var permissions = (await Context.Guild.GetCurrentUserAsync()).GetPermissions(channel);
                if (permissions.ViewChannel)
                {
                    if (channel.IsNsfw)
                    {
                        await channel.ModifyAsync(x => x.IsNsfw = false);
                        if (channel.Id == Context.Channel.Id)
                            await ReplyConfirmationAsync("current_channel_nsfw_disable");
                        else
                            await ReplyConfirmationAsync("channel_nsfw_disable", channel.Name);
                    }
                    else
                    {
                        await channel.ModifyAsync(x => x.IsNsfw = true);
                        if (channel.Id == Context.Channel.Id)
                            await ReplyConfirmationAsync("current_channel_nsfw_enable");
                        else
                            await ReplyConfirmationAsync("channel_nsfw_enable", channel.Name);
                    }
                }
                else
                {
                    await ReplyErrorAsync("text_channel_no_permission_view");
                }
            }
        }
    }
}