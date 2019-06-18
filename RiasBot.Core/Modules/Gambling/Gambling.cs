using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Modules.Gambling.Services;
using RiasBot.Services;

namespace RiasBot.Modules.Gambling
{
    public partial class Gambling : RiasModule
    {
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly CommandHandler _ch;
        private readonly BlackjackService _blackjackService;

        public Gambling(IBotCredentials creds, DbService db, CommandHandler ch, BlackjackService blackjackService)
        {
            _creds = creds;
            _db = db;
            _ch = ch;
            _blackjackService = blackjackService;
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(3, 5, RateLimitType.GuildUser)]
        public async Task WheelAsync(int bet)
        {
            if (bet < 5)
            {
                await ReplyErrorAsync("bet_less", 5, _creds.Currency);
                return;
            }
            
            if (bet > 5000)
            {
                await ReplyErrorAsync("bet_more", 5000, _creds.Currency);
                return;
            }
            
            string[] arrow = { "⬆", "↗", "➡", "↘", "⬇", "↙", "⬅", "↖" };
            float[] wheelMultiple = { 1.7f, 2.0f, 1.2f, 0.5f, 0.3f, 0.0f, 0.2f, 1.5f };
            var rnd = new Random((int)DateTime.UtcNow.Ticks);
            var wheel = rnd.Next(8);

            using (var db = _db.GetDbContext())
            {
                var userDb = db.Users.FirstOrDefault(x => x.UserId == Context.User.Id);
                if (userDb != null)
                {
                    if (bet <= userDb.Currency)
                    {
                        var win = (int)(bet * wheelMultiple[wheel]);
                        userDb.Currency += win - bet;
                        await db.SaveChangesAsync();

                        var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                        embed.WithTitle($"{Context.User} {GetText("you_won", win, _creds.Currency)}");
                        embed.WithDescription($"「1.5x」\t「1.7x」\t「2.0x」\n\n「0.2x」\t    {arrow[wheel]}    \t「1.2x」\n\n「0.0x」\t「0.3x」\t「0.5x」");
                        await Context.Channel.SendMessageAsync("", embed: embed.Build());
                    }
                    else
                    {
                        await ReplyErrorAsync("currency_not_enough");
                    }
                }
                else
                {
                    await ReplyErrorAsync("currency_not_enough");
                }
            }
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task BlackjackAsync(int bet)
        {
            if (bet < 5)
            {
                await ReplyErrorAsync("bet_less", 5, _creds.Currency);
                return;
            }
            
            if (bet > 5000)
            {
                await ReplyErrorAsync("bet_more", 5000, _creds.Currency);
                return;
            }
            
            var currency = _blackjackService.GetCurrency((IGuildUser)Context.User);
            if (bet <= currency)
            {
                var bj = _blackjackService.GetGame((IGuildUser) Context.User);
                if (bj is null)
                {
                    bj = _blackjackService.GetOrCreateGame((IGuildUser) Context.User);
                    await bj.InitializeGameAsync(Context.Guild, Context.Channel, (IGuildUser)Context.User, bet); 
                }
                else
                {
                    await ReplyErrorAsync("blackjack_session", _ch.GetPrefix(Context.Guild));
                } 
            }
            else
            {
                await ReplyErrorAsync("currency_not_enough");
            }
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        public async Task BlackjackAsync(string subcommand)
        {
            subcommand = subcommand.ToLowerInvariant();
            var bj = _blackjackService.GetGame((IGuildUser) Context.User);
            switch (subcommand)
            {
                case "resume":
                    if (bj != null)
                        await bj.ResumeGameAsync((IGuildUser) Context.User, Context.Channel);
                    else
                        await ReplyErrorAsync("blackjack_no_session");
                    break;
                case "stop":
                case "surrender":
                    if (bj != null)
                    {
                        await bj.StopGameAsync();
                        await ReplyConfirmationAsync("blackjack_stop");
                    }
                    else
                        await ReplyErrorAsync("blackjack_no_session");
                    break;
            }
        }
    }
}