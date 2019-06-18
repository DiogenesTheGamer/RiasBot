using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Modules.Reactions.Services;
using RiasBot.Services;

namespace RiasBot.Modules.Reactions
{
    public class Reactions : RiasModule<ReactionsService>
    {
        private readonly IBotCredentials _creds;

        public Reactions(IBotCredentials creds)
        {
            _creds = creds;
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task PatAsync([Remainder]IGuildUser user)
        {
            await SendReactionAsync(user, "pat", "pat_you", "patted_by");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task PatAsync([Remainder]string user = null)
        {
            await SendReactionAsync("pat", "pat_you", "patted_by", user);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task HugAsync([Remainder]IGuildUser user)
        {
            await SendReactionAsync(user, "hug", "hug_you", "hugged_by");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task HugAsync([Remainder]string user = null)
        {
            await SendReactionAsync("hug", "hug_you", "hugged_by", user);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task KissAsync([Remainder]IGuildUser user)
        {
            await SendReactionAsync(user, "kiss", "kiss_you", "kissed_by");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task KissAsync([Remainder]string user = null)
        {
            await SendReactionAsync("kiss", "kiss_you", "kissed_by", user);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task LickAsync([Remainder]IGuildUser user)
        {
            await SendReactionAsync(user, "lick", "lick_you", "licked_by");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task LickAsync([Remainder]string user = null)
        {
            await SendReactionAsync("lick", "lick_you", "licked_by", user);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task CuddleAsync([Remainder]IGuildUser user)
        {
            await SendReactionAsync(user, "cuddle", "cuddle_you", "cuddled_by");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task CuddleAsync([Remainder]string user = null)
        {
            await SendReactionAsync("cuddle", "cuddle_you", "cuddled_by", user);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task BiteAsync([Remainder]IGuildUser user)
        {
            await SendReactionAsync(user, "bite", "bite_you", "bitten_by");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task BiteAsync([Remainder]string user = null)
        {
            await SendReactionAsync("bite", "bite_you", "bitten_by", user);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task SlapAsync([Remainder]IGuildUser user)
        {
            await SendReactionAsync(user, "slap", "slap_you", "slapped_by");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task SlapAsync([Remainder]string user = null)
        {
            await SendReactionAsync("slap", "slap_you", "slapped_by", user);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task CryAsync()
        {
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
            embed.WithImageUrl(await Service.GetReactionAsync("cry", "gif"));
            embed.WithFooter($"{GetText("powered_by")} weeb.sh");

            await Context.Channel.SendMessageAsync(GetText("dont_cry", Context.User.Mention),embed: embed.Build());
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task GropeAsync([Remainder]IGuildUser user)
        {
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
            embed.WithImageUrl(await Service.GetGropeImage());
            embed.WithFooter($"{GetText("powered_by")} weeb.sh");

            if (user.Id == Context.User.Id)
                await Context.Channel.SendMessageAsync(GetText("grope_you", Context.User.Mention), embed: embed.Build());
            else
                await Context.Channel.SendMessageAsync(GetText("groped_by", user.Mention, Context.User), embed: embed.Build());
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        [RateLimit(2, 5, RateLimitType.GuildUser)]
        public async Task GropeAsync([Remainder]string user = null)
        {
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
            embed.WithImageUrl(await Service.GetGropeImage());
            embed.WithFooter($"{GetText("powered_by")} weeb.sh");

            if (user is null)
                await Context.Channel.SendMessageAsync(GetText("grope_you", Context.User.Mention), embed: embed.Build());
            else
                await Context.Channel.SendMessageAsync(GetText("groped_by", user, Context.User), embed: embed.Build());
        }

        private async Task SendReactionAsync(IGuildUser user, string type, string you, string by)
        {
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
            embed.WithImageUrl(await Service.GetReactionAsync(type, "gif"));
            embed.WithFooter($"{GetText("powered_by")} weeb.sh");

            if (user.Id == Context.User.Id)
                await Context.Channel.SendMessageAsync(GetText(you, Context.User.Mention), embed: embed.Build());
            else
                await Context.Channel.SendMessageAsync(GetText(by, user.Mention, Context.User), embed: embed.Build());
        }
        
        private async Task SendReactionAsync(string type, string you, string by, string user = null)
        {
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
            embed.WithImageUrl(await Service.GetReactionAsync(type, "gif"));
            embed.WithFooter($"{GetText("powered_by")} weeb.sh");

            if (user is null)
                await Context.Channel.SendMessageAsync(GetText(you, Context.User.Mention), embed: embed.Build());
            else
                await Context.Channel.SendMessageAsync(GetText(by, user, Context.User), embed: embed.Build());
        }
    }
}