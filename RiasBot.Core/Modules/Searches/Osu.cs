using System;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Services;

namespace RiasBot.Modules.Searches
{
    public partial class Searches
    {
        public class Osu : RiasSubmodule
        {
            private readonly RLog _log;

            public Osu(RLog log)
            {
                _log = log;
            }
            
            private const string LemmyUrl1 = "https://lemmmy.pw/osusig/sig.php?colour=hexdc143c&uname=";
            private const string LemmyUrl2 = "&pp=2&countryrank&removeavmargin&flagshadow&darktriangles&onlineindicator=undefined&xpbar&xpbarhex";

            [RiasCommand][Aliases]
            [Description][Usages]
            public async Task OsuAsync([Remainder] string username) =>
                await SendOsuStatsAsync(username);
            
            [RiasCommand][Aliases]
            [Description][Usages]
            public async Task TaikoAsync([Remainder] string username) =>
                await SendOsuStatsAsync(username, 1);
            
            [RiasCommand][Aliases]
            [Description][Usages]
            public async Task CtbAsync([Remainder] string username) =>
                await SendOsuStatsAsync(username, 2);
            
            [RiasCommand][Aliases]
            [Description][Usages]
            public async Task ManiaAsync([Remainder] string username) =>
                await SendOsuStatsAsync(username, 3);

            private async Task SendOsuStatsAsync(string username, int mode = 0)
            {
                using (var http = new HttpClient())
                {
                    username = Uri.EscapeUriString(username);
                    var response = await http.GetAsync($"{LemmyUrl1}{username}&mode={mode}{LemmyUrl2}");
                    if (response.IsSuccessStatusCode)
                    {
                        using (var statsStream = await response.Content.ReadAsStreamAsync())
                        {
                            await Context.Channel.SendFileAsync(statsStream, $"{username}.png");
                        }
                    }
                    else
                    {
                        await _log.Error("Osu: The player's image stats couldn't be downloaded!");
                    }
                }
            }
        }
    }
}