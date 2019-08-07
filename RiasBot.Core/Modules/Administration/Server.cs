using System;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;

namespace RiasBot.Modules.Administration
{
    public partial class Administration
    {
        public class Server : RiasSubmodule
        {
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageNicknames)]
            [RequireBotPermission(GuildPermission.ManageNicknames)]
            public async Task SetNicknameAsync(IGuildUser user, [Remainder]string name = null)
            {
                if (user.Id == Context.Guild.OwnerId)
                {
                    await ReplyErrorAsync("nickname_owner");
                    return;
                }

                if (user.CheckHierarchy(await Context.Guild.GetCurrentUserAsync()))
                {
                    await ReplyErrorAsync("user_above");
                    return;
                }

                if (string.IsNullOrEmpty(name))
                {
                    await user.ModifyAsync(x => x.Nickname = name);
                    await ReplyConfirmationAsync("nickname_removed", user, user.Username);
                }
                else
                {
                    await user.ModifyAsync(x => x.Nickname = name);
                    await ReplyConfirmationAsync("nickname_changed", user, name);
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageGuild)]
            [RequireBotPermission(GuildPermission.ManageGuild)]
            public async Task SetGuildNameAsync([Remainder]string name)
            {
                await Context.Guild.ModifyAsync(x => x.Name = name);
                await ReplyConfirmationAsync("server_name_changed", name);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageGuild)]
            [RequireBotPermission(GuildPermission.ManageGuild)]
            public async Task SetGuildIconAsync(string url)
            {
                try
                {
                    using (var http = new HttpClient())
                    {
                        var stream = await http.GetStreamAsync(new Uri(url));
                        await Context.Guild.ModifyAsync(x => x.Icon = new Image(stream));
                    }

                    await ReplyConfirmationAsync("server_icon_changed");
                }
                catch
                {
                    await ReplyErrorAsync("image_or_url_not_good");
                }
            }
        }
    }
}