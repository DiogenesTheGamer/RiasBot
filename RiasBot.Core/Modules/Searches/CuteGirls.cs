using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Modules.Searches.Services;
using RiasBot.Services;

namespace RiasBot.Modules.Searches
{
    public partial class Searches
    {
        public class CuteGirls : RiasSubmodule<CuteGirlsService>
        {
            private readonly IBotCredentials _creds;

            public CuteGirls(IBotCredentials creds)
            {
                _creds = creds;
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RateLimit(2, 5, RateLimitType.GuildUser)]
            public async Task NekoAsync()
            {
                var neko = await Service.GetNekoImageAsync();
                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                embed.WithTitle("Neko <3");
                embed.WithImageUrl(neko);
                embed.WithFooter($"{GetText("#reactions_powered_by")} weeb.sh");

                await Context.Channel.SendMessageAsync("", embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RateLimit(2, 5, RateLimitType.GuildUser)]
            public async Task KitsuneAsync()
            {
                var kitsune = await Service.GetKitsuneImageAsync();
                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                embed.WithTitle("Kitsune <3");
                embed.WithImageUrl(kitsune);
                embed.WithFooter($"{GetText("#reactions_powered_by")} riasbot.me");

                await Context.Channel.SendMessageAsync("", embed: embed.Build());
            }
        }
    }
}