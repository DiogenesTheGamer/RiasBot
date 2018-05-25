﻿using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.Webhook;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Music.MusicServices;
using RiasBot.Modules.Reactions.Services;
using RiasBot.Modules.Searches.Services;
using RiasBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RiasBot.Modules.Bot
{
    public partial class Bot : RiasModule
    {
        private readonly CommandHandler _ch;
        private readonly CommandService _service;
        private readonly IServiceProvider _provider;
        private readonly DbService _db;
        private readonly DiscordShardedClient _client;
        private readonly BotService _botService;
        private readonly InteractiveService _is;
        private readonly IBotCredentials _creds;

        private readonly MusicService _musicService;
        private readonly ReactionsService _reactionsService;

        public Bot(CommandHandler ch, CommandService service, IServiceProvider provider, DbService db, DiscordShardedClient client, BotService botService,
            InteractiveService interactiveService, IBotCredentials creds, MusicService musicService, ReactionsService reactionsService)
        {
            _ch = ch;
            _service = service;
            _provider = provider;
            _db = db;
            _client = client;
            _botService = botService;
            _is = interactiveService;
            _creds = creds;

            _musicService = musicService;
            _reactionsService = reactionsService;
        }

        [RiasCommand][@Alias]
        [Description][@Remarks]
        [RequireOwner]
        public async Task LeaveGuild(ulong id)
        {
            var guild = await Context.Client.GetGuildAsync(id).ConfigureAwait(false);
            if (guild != null)
            {
                var usersGuild = await guild.GetUsersAsync();
                var embed = new EmbedBuilder().WithColor(RiasBot.goodColor);
                embed.WithDescription($"Leaving {Format.Bold(guild.Name)}");
                embed.AddField("Id", guild.Id, true).AddField("Users", usersGuild.Count, true);

                await Context.Channel.SendMessageAsync("", embed: embed.Build()).ConfigureAwait(false);

                await guild.LeaveAsync().ConfigureAwait(false);
            }
        }

        [RiasCommand]
        [@Alias]
        [Description]
        [@Remarks]
        [RequireOwner]
        public async Task Die()
        {
            await Context.Channel.SendConfirmationEmbed("Shutting down...").ConfigureAwait(false);
            foreach (var mp in _musicService.MPlayer)
            {
                try
                {
                    await mp.Value.Destroy("", true).ConfigureAwait(false);
                }
                catch
                {

                }
            }
            await Context.Client.StopAsync().ConfigureAwait(false);
            Environment.Exit(0);
        }

        [RiasCommand]
        [@Alias]
        [Description]
        [@Remarks]
        [RequireOwner]
        public async Task Send(string id, [Remainder]string message)
        {
            IGuild guild;
            IUser user;
            ITextChannel channel;

            var embed = Extensions.Extensions.EmbedFromJson(message);

            if (id.Contains("|"))
            {
                try
                {
                    var ids = id.Split('|');
                    string guildId = ids[0];
                    string channelId = ids[1];

                    guild = await Context.Client.GetGuildAsync(Convert.ToUInt64(guildId)).ConfigureAwait(false);
                    channel = await guild.GetTextChannelAsync(Convert.ToUInt64(channelId)).ConfigureAwait(false);
                    if (embed is null)
                        await channel.SendMessageAsync(message).ConfigureAwait(false);
                    else
                        await channel.SendMessageAsync("", embed: embed.Build()).ConfigureAwait(false);
                    await Context.Channel.SendConfirmationEmbed("Message sent!").ConfigureAwait(false);
                }
                catch
                {
                    await Context.Channel.SendErrorEmbed("I couldn't find the guild or the channel");
                }
            }
            else
            {
                try
                {
                    user = await Context.Client.GetUserAsync(Convert.ToUInt64(id));
                    if (embed is null)
                        await user.SendMessageAsync(message).ConfigureAwait(false);
                    else
                        await user.SendMessageAsync("", embed: embed.Build()).ConfigureAwait(false);
                    await Context.Channel.SendConfirmationEmbed("Message sent!").ConfigureAwait(false);
                }
                catch
                {
                    await Context.Channel.SendErrorEmbed("I couldn't find the user").ConfigureAwait(false);
                }
            }
        }

        [RiasCommand]
        [@Alias]
        [Description]
        [@Remarks]
        [RequireOwner]
        public async Task Edit(string channelMessage, [Remainder]string message)
        {
            try
            {
                var embed = Extensions.Extensions.EmbedFromJson(message);
                var ids = channelMessage.Split("|");
                UInt64.TryParse(ids[0], out ulong channelId);
                UInt64.TryParse(ids[1], out ulong messageId);


                var channel = await Context.Client.GetChannelAsync(channelId).ConfigureAwait(false);
                var msg = await ((ITextChannel)channel).GetMessageAsync(messageId).ConfigureAwait(false);

                if (embed is null)
                    await ((IUserMessage)msg).ModifyAsync(x => x.Content = message).ConfigureAwait(false);
                else
                    await ((IUserMessage)msg).ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                await Context.Channel.SendConfirmationEmbed("Message edited!");
            }
            catch
            {
                await Context.Channel.SendConfirmationEmbed("I couldn't find the channel/message!");
            }
        }

        [RiasCommand][@Alias]
        [Description][@Remarks]
        [RequireOwner]
        public async Task Dbl()
        {
            var embed = new EmbedBuilder().WithColor(RiasBot.goodColor);
            var votes = new List<string>();
            int index = 0;
            foreach (var vote in _botService.votesList)
            {
                var user = await Context.Client.GetUserAsync(vote.user);
                votes.Add($"#{index+1} {user?.ToString()} ({vote.user})");
                index++;
            }
            var pager = new PaginatedMessage
            {
                Title = "List of voters today",
                Color = new Color(RiasBot.goodColor),
                Pages = votes,
                Options = new PaginatedAppearanceOptions
                {
                    ItemsPerPage = 15,
                    Timeout = TimeSpan.FromMinutes(1),
                    DisplayInformationIcon = false,
                    JumpDisplayOptions = JumpDisplayOptions.Never
                }

            };
            await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
        }

        [RiasCommand][@Alias]
        [Description][@Remarks]
        [RequireOwner]
        public async Task UpdateImages()
        {
            await _reactionsService.UpdateImages("H1Pqa", _reactionsService.biteList);
            await _reactionsService.UpdateImages("woGOn", _reactionsService.cryList);
            await _reactionsService.UpdateImages("GdiXR", _reactionsService.gropeList);
            await _reactionsService.UpdateImages("KTkPe", _reactionsService.hugList);
            await _reactionsService.UpdateImages("CotHR", _reactionsService.kissList);
            await _reactionsService.UpdateImages("5cMDN", _reactionsService.lickList);
            await _reactionsService.UpdateImages("OQjWy", _reactionsService.patList);
            await _reactionsService.UpdateImages("AQoU8", _reactionsService.slapList);

            await Context.Channel.SendConfirmationEmbed($"{Context.User.Mention} reactions images, updated");
        }

        [RiasCommand][@Alias]
        [Description][@Remarks]
        [RequireOwner]
        public async Task Delete(ulong id)
        {
            var confirm = await Context.Channel.SendConfirmationEmbed($"Are you sure you want to delete the user? Type {Format.Code("confirm")}");
            var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            if (input != null)
            {
                if (input.Content == "confirm")
                {
                    var user = await Context.Client.GetUserAsync(id).ConfigureAwait(false);
                    using (var db = _db.GetDbContext())
                    {
                        var userDb = db.Users.Where(x => x.UserId == id).FirstOrDefault();
                        if (userDb != null)
                        {
                            db.Remove(userDb);
                        }
                        var waifusDb = db.Waifus.Where(x => x.UserId == id);
                        if (waifusDb != null)
                        {
                            db.RemoveRange(waifusDb);
                        }
                        var profileDb = db.Profile.Where(x => x.UserId == id).FirstOrDefault();
                        if (profileDb != null)
                        {
                            db.Remove(profileDb);
                        }
                        if (user != null)
                        {
                            await Context.Channel.SendConfirmationEmbed($"{Context.User.Mention} user {user} has been deleted from the database").ConfigureAwait(false);
                        }
                        else
                        {
                            await Context.Channel.SendConfirmationEmbed($"{Context.User.Mention} user {id} has been deleted from the database").ConfigureAwait(false);
                        }
                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        [RiasCommand][@Alias]
        [Description][@Remarks]
        [RequireOwner]
        public async Task Delete([Remainder]string user)
        {
            var confirm = await Context.Channel.SendConfirmationEmbed($"Are you sure you want to delete the user? Type {Format.Code("confirm")}");
            var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            if (input != null)
            {
                if (input.Content == "confirm")
                {
                    var userSplit = user.Split("#");
                    var getUser = await Context.Client.GetUserAsync(userSplit[0], userSplit[1]).ConfigureAwait(false);

                    if (getUser is null)
                    {
                        await Context.Channel.SendErrorEmbed($"{Context.User.Mention} the user couldn't be found");
                        return;
                    }
                    using (var db = _db.GetDbContext())
                    {
                        var userDb = db.Users.Where(x => x.UserId == getUser.Id).FirstOrDefault();
                        if (userDb != null)
                        {
                            db.Remove(userDb);
                        }
                        var waifusDb = db.Waifus.Where(x => x.UserId == getUser.Id);
                        if (waifusDb != null)
                        {
                            db.RemoveRange(waifusDb);
                        }
                        var profileDb = db.Profile.Where(x => x.UserId == getUser.Id).FirstOrDefault();
                        if (profileDb != null)
                        {
                            db.Remove(profileDb);
                        }
                        await Context.Channel.SendConfirmationEmbed($"{Context.User.Mention} user {user} has been deleted from the database").ConfigureAwait(false);
                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
