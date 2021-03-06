using System;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Qmmands;
using Rias.Core.Attributes;
using Rias.Core.Commons;
using Rias.Core.Services;
using Rias.Core.Implementation;

namespace Rias.Core.Modules.Nsfw
{
    [Name("Nsfw")]
    public class NsfwModule : RiasModule<NsfwService>
    {
        public NsfwModule(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }
        
        [Command("hentai"), Context(ContextType.Guild),
         Cooldown(1, 5, CooldownMeasure.Seconds, BucketType.Channel)]
        public async Task HentaiAsync([Remainder] string? tags = null)
            => await PostHentaiAsync(NsfwService.NsfwImageApiProvider.Random, tags);

        [Command("danbooru"), Context(ContextType.Guild),
         Cooldown(1, 5, CooldownMeasure.Seconds, BucketType.Channel)]
        public async Task DanbooruAsync([Remainder] string? tags = null)
            => await PostHentaiAsync(NsfwService.NsfwImageApiProvider.Danbooru, tags);
        
        [Command("konachan"), Context(ContextType.Guild),
         Cooldown(1, 5, CooldownMeasure.Seconds, BucketType.Channel)]
        public async Task KonachanAsync([Remainder] string? tags = null)
            => await PostHentaiAsync(NsfwService.NsfwImageApiProvider.Konachan, tags);
        
        [Command("yandere"), Context(ContextType.Guild),
         Cooldown(1, 5, CooldownMeasure.Seconds, BucketType.Channel)]
        public async Task YandereAsync([Remainder] string? tags = null)
            => await PostHentaiAsync(NsfwService.NsfwImageApiProvider.Yandere, tags);
        
        [Command("gelbooru"), Context(ContextType.Guild),
         Cooldown(1, 5, CooldownMeasure.Seconds, BucketType.Channel)]
        public async Task GelbooruAsync([Remainder] string? tags = null)
            => await PostHentaiAsync(NsfwService.NsfwImageApiProvider.Gelbooru, tags);
        
        [Command("hentaiplus"), Context(ContextType.Guild),
         Cooldown(1, 10, CooldownMeasure.Seconds, BucketType.Channel)]
        public async Task HentaiPlusAsync([Remainder] string? tags = null)
        {
            if (!((CachedTextChannel) Context.Channel).IsNsfw)
            {
                await ReplyErrorAsync(Localization.NsfwChannelNotNsfw);
                return;
            }
            
            if (!Service.CacheInitialized)
            {
                await ReplyErrorAsync(Localization.NsfwCacheNotInitialized);
                return;
            }
            
            var hentaiBuilder = new StringBuilder();
            var danbooruHentai = await Service.GetNsfwImageAsync(NsfwService.NsfwImageApiProvider.Danbooru, tags);
            var konachanHentai = await Service.GetNsfwImageAsync(NsfwService.NsfwImageApiProvider.Konachan, tags);
            var yandereHentai = await Service.GetNsfwImageAsync(NsfwService.NsfwImageApiProvider.Yandere, tags);
            var gelbooruHentai = await Service.GetNsfwImageAsync(NsfwService.NsfwImageApiProvider.Gelbooru, tags);

            if (danbooruHentai != null)
                hentaiBuilder.Append(danbooruHentai.Url).Append("\n");
            if (konachanHentai != null)
                hentaiBuilder.Append(konachanHentai.Url).Append("\n");
            if (yandereHentai != null)
                hentaiBuilder.Append(yandereHentai.Url).Append("\n");
            if (gelbooruHentai != null)
                hentaiBuilder.Append(gelbooruHentai.Url).Append("\n");

            if (hentaiBuilder.Length == 0)
            {
                await ReplyErrorAsync(Localization.NsfwNoHentai);
                return;
            }

            await Context.Channel.SendMessageAsync(hentaiBuilder.ToString());
        }
        
        private async Task PostHentaiAsync(NsfwService.NsfwImageApiProvider provider, string? tags = null)
        {
            if (!((CachedTextChannel) Context.Channel).IsNsfw)
            {
                await ReplyErrorAsync(Localization.NsfwChannelNotNsfw);
                return;
            }
            
            if (!Service.CacheInitialized)
            {
                await ReplyErrorAsync(Localization.NsfwCacheNotInitialized);
                return;
            }
            
            var nsfwImage = await Service.GetNsfwImageAsync(provider, tags);
            if (nsfwImage is null)
            {
                await ReplyErrorAsync(Localization.NsfwNoHentai);
                return;
            }

            var embed = new LocalEmbedBuilder
            {
                Color = RiasUtilities.ConfirmColor,
                Author = new LocalEmbedAuthorBuilder
                {
                    Name = $"{GetText(Localization.SearchesSource)}: {nsfwImage.Provider}",
                    Url = nsfwImage.Url
                },
                ImageUrl = nsfwImage.Url
            };

            await ReplyAsync(embed);
        }
    }
}