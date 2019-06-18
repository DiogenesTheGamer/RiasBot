using System.Threading.Tasks;
using Discord;

namespace RiasBot.Extensions
{
    public static class MessageExtensions
    {
        public static async Task<IUserMessage> SendConfirmationMessageAsync(this IMessageChannel channel, string message)
        {
            var embed = new EmbedBuilder().WithColor(RiasBot.ConfirmColor)
                .WithDescription(message);
            return await channel.SendMessageAsync(embed: embed.Build());
        }

        public static async Task<IUserMessage> SendErrorMessageAsync(this IMessageChannel channel, string message)
        {
            var embed = new EmbedBuilder().WithColor(RiasBot.ErrorColor)
                .WithDescription(message);
            return await channel.SendMessageAsync(embed: embed.Build());
        }
    }
}