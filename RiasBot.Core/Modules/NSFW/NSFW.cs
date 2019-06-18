using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Modules.Nsfw.Services;
using RiasBot.Services;

namespace RiasBot.Modules.Nsfw
{
    public class Nsfw : RiasModule<NsfwService>
    {
        private readonly IBotCredentials _creds;

        public Nsfw(IBotCredentials creds)
        {
            _creds = creds;
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task HentaiAsync([Remainder]string tag = null)
        {
            await PostHentaiAsync(NsfwService.DapiWebsite.Random, tag);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task DanbooruAsync([Remainder]string tag = null)
        {
            await PostHentaiAsync(NsfwService.DapiWebsite.Danbooru, tag);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task KonachanAsync([Remainder]string tag = null)
        {
            await PostHentaiAsync(NsfwService.DapiWebsite.Konachan, tag);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task YandereAsync([Remainder]string tag = null)
        {
            await PostHentaiAsync(NsfwService.DapiWebsite.Yandere, tag);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task GelbooruAsync([Remainder]string tag = null)
        {
            await PostHentaiAsync(NsfwService.DapiWebsite.Gelbooru, tag);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task HentaiPlusAsync([Remainder]string tag = null)
        {
            var channel = (ITextChannel)Context.Channel;
            if (!channel.IsNsfw)
            {
                await ReplyErrorAsync("not_nsfw_channel");
                return;
            }

            var hentais = new StringBuilder();
            hentais.Append((await Service.GetImageAsync(NsfwService.DapiWebsite.Danbooru, tag))?.FileUrl ?? "").Append("\n");
            hentais.Append((await Service.GetImageAsync(NsfwService.DapiWebsite.Konachan, tag))?.FileUrl ?? "").Append("\n");
            hentais.Append((await Service.GetImageAsync(NsfwService.DapiWebsite.Yandere, tag))?.FileUrl ?? "").Append("\n");
            hentais.Append((await Service.GetImageAsync(NsfwService.DapiWebsite.Gelbooru, tag))?.FileUrl ?? "");
            
            if (hentais.Length > 0)
            {
                await Context.Channel.SendMessageAsync(hentais.ToString());
            }
            else
            {
                var embed = new EmbedBuilder().WithColor(RiasBot.ErrorColor);
                embed.WithDescription(GetText("hentai_image_not_found"));

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }
        }

        private async Task PostHentaiAsync(NsfwService.DapiWebsite dapiWebsite, string tag)
        {
            var channel = (ITextChannel)Context.Channel;
            if (!channel.IsNsfw)
            {
                await ReplyErrorAsync("not_nsfw_channel");
                return;
            }
            
            var retry = 5;
            
            NsfwService.DapiImage hentai;
            do
            {
                hentai = await Service.GetImageAsync(dapiWebsite, tag);
                retry--;
            } while ((hentai is null || string.IsNullOrEmpty(hentai.FileUrl)) && retry > 0);

            if (hentai != null && !string.IsNullOrEmpty(hentai.FileUrl))
            {
                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                embed.WithImageUrl(hentai.FileUrl);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }
            else
            {
                var embed = new EmbedBuilder().WithColor(RiasBot.ErrorColor);
                embed.WithDescription(GetText("hentai_image_not_found"));

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }
        }
    }
}