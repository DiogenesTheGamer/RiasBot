﻿using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiasBot.Extensions
{
    public static class IMessageExtension
    {
        ///<summary>
        ///Send confirmation embed message in current text channel.
        ///</summary>
        public static async Task<IUserMessage> SendConfirmationEmbed(this IMessageChannel channel, string description)
        {
            var embed = new EmbedBuilder().WithColor(RiasBot.goodColor);
            embed.WithDescription(description);
            return await channel.SendMessageAsync("", embed: embed.Build()).ConfigureAwait(false);
        }

        ///<summary>
        ///Send error embed message in current text channel.
        ///</summary>
        public static async Task<IUserMessage> SendErrorEmbed(this IMessageChannel channel, string description)
        {
            var embed = new EmbedBuilder().WithColor(RiasBot.badColor);
            embed.WithDescription(description);
            return await channel.SendMessageAsync("", embed: embed.Build()).ConfigureAwait(false);
        }
    }
}
