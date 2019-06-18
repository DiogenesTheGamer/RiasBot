using System;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;

namespace RiasBot.Modules.Bot
{
    public partial class Bot
    {
        public class Configuration : RiasSubmodule
        {
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task SetUsernameAsync([Remainder]string username)
            {
                if (username.Length < 2 || username.Length > 32)
                {
                    await ReplyErrorAsync("username_length_limit");
                    return;
                }

                try
                {
                    await Context.Client.CurrentUser.ModifyAsync(u => u.Username = username);
                    await ReplyConfirmationAsync("username_changed", username);
                }
                catch
                {
                    await ReplyErrorAsync("username_change_error");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task SetAvatarAsync(string url)
            {
                try
                {
                    using (var http = new HttpClient())
                    {
                        var res = await http.GetStreamAsync(new Uri(url));
                        await Context.Client.CurrentUser.ModifyAsync(x => x.Avatar = new Image(res));
                    }
                    
                    await ReplyConfirmationAsync("avatar_changed");
                }
                catch
                {
                    await ReplyErrorAsync("avatar_change_error");
                }
            }
        }
    }
}