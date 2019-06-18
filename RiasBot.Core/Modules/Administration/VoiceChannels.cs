using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;

namespace RiasBot.Modules.Administration
{
    public partial class Administration
    {
        public class VoiceChannels : RiasSubmodule
        {
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RequireContext(ContextType.Guild)]
            public async Task CreateVoiceChannelAsync([Remainder]string name)
            {
                if (name.Length < 1 || name.Length > 100)
                {
                    await ReplyConfirmationAsync("channel_name_length_limit");
                    return;
                }
                
                await Context.Guild.CreateVoiceChannelAsync(name);
                await ReplyConfirmationAsync("voice_channel_created", name);
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RequireContext(ContextType.Guild)]
            public async Task DeleteVoiceChannelAsync([Remainder]IVoiceChannel channel)
            {
                var permissions = (await Context.Guild.GetCurrentUserAsync()).GetPermissions(channel);
                if (permissions.ViewChannel)
                {
                    await channel.DeleteAsync();
                    await ReplyConfirmationAsync("voice_channel_deleted", channel.Name);
                }
                else
                {
                    await ReplyErrorAsync("voice_channel_no_permission_view");
                }
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RequireContext(ContextType.Guild)]
            public async Task RenameVoiceChannelAsync([Remainder] string names)
            {
                var namesSplit = names.Split("->");
                var oldName = namesSplit[0].TrimEnd();
                var newName = namesSplit[1].TrimStart();
                var channel = await ChannelsExtensions.GetVoiceChannelByIdAsync(Context.Guild, oldName) ??
                              (await Context.Guild.GetVoiceChannelsAsync())
                              .FirstOrDefault(x => string.Equals(x.Name, oldName, StringComparison.InvariantCultureIgnoreCase));
                if (channel != null)
                {
                    var permissions = (await Context.Guild.GetCurrentUserAsync()).GetPermissions(channel);
                    if (permissions.ViewChannel)
                    {
                        oldName = channel.Name;
                        await channel.ModifyAsync(x => x.Name = newName);
                        await ReplyConfirmationAsync("voice_channel_renamed", oldName, channel.Name);
                    }
                    else
                    {
                        await ReplyErrorAsync("voice_channel_no_permission_view");
                    }
                }
                else
                {
                    await ReplyErrorAsync("voice_channel_not_found");
                }
            }
        }
    }
}