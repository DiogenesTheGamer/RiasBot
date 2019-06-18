using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;

namespace RiasBot.Modules.Games
{
    public class Games : RiasModule
    {
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task RpsAsync(string rps)
        {
            rps = rps?.ToLowerInvariant();
            string[] types = { "rock", "paper", "scissors" };
            var playerChoice = 0;

            switch(rps)
            {
                case "rock":
                case "r":
                    playerChoice = 1;
                    break;
                case "paper":
                case "p":
                    playerChoice = 2;
                    break;
                case "scissors":
                case "s":
                    playerChoice = 3;
                    break;
            }
            if (playerChoice > 0)
            {
                var rnd = new Random((int)DateTime.UtcNow.Ticks);
                var botChoice = rnd.Next(1, 4);

                if (botChoice % 3 + 1 == playerChoice)
                    await ReplyConfirmationAsync("rps_won", types[botChoice - 1]);
                else if (playerChoice % 3 + 1 == botChoice)
                    await ReplyErrorAsync("rps_lost", types[botChoice - 1]);
                else
                {
                    var embed = new EmbedBuilder().WithColor(0xffff00);
                    embed.WithDescription(GetText("rps_draw", types[botChoice - 1]));
                    await Context.Channel.SendMessageAsync(embed: embed.Build());
                }
            }
        }
    }
}