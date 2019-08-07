using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Music.Services;
using RiasBot.Services;
using BotService = RiasBot.Modules.Bot.Services.BotService;

namespace RiasBot.Modules.Bot
{
    public partial class Bot : RiasModule<BotService>
    {
        private readonly DiscordShardedClient _client;
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly InteractiveService _is;
        private readonly VotesService _votesService;
        private readonly MusicService _musicService;

        public Bot(DiscordShardedClient client, DbService db, IBotCredentials creds,
            InteractiveService interactiveService, VotesService votesService, MusicService musicService)
        {
            _db = db;
            _creds = creds;
            _client = client;
            _is = interactiveService;
            _votesService = votesService;
            _musicService = musicService;
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        [Priority(1)]
        public async Task LeaveGuildAsync(ulong id)
        {
            var guild = await Context.Client.GetGuildAsync(id);
            if (guild != null)
            {
                var usersGuild = await guild.GetUsersAsync();
                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                embed.WithDescription(GetText("leave_guild", guild.Name));
                embed.AddField(GetText("#administration_id"), guild.Id, true).AddField(GetText("users"), usersGuild.Count, true);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
                await guild.LeaveAsync();
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        [Priority(0)]
        public async Task LeaveGuildAsync(string name)
        {
            var guild = (await Context.Client.GetGuildsAsync()).FirstOrDefault(g => string.Equals(name, g.Name));
            if (guild != null)
            {
                var usersGuild = await guild.GetUsersAsync();
                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                embed.WithDescription(GetText("leave_guild", guild.Name));
                embed.AddField(GetText("administration", "id"), guild.Id, true).AddField(GetText("users"), usersGuild.Count, true);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
                await guild.LeaveAsync();
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        public async Task UpdateAsync()
        {
            foreach (var musicPlayer in _musicService.MusicPlayers.Values)
            {
                await _musicService.StopAsync(musicPlayer.Guild, false);
            }

            await ReplyConfirmationAsync("update");
            Environment.Exit(0);
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        public async Task SendAsync(string id, [Remainder]string message)
        {
            var isEmbed = Extensions.Extensions.TryParseEmbed(message, out var embed);

            if (id.Contains("c:"))
            {
                id = id.Substring(2);
                ITextChannel channel;

                if (ulong.TryParse(id, out var channelId))
                {
                    channel = (ITextChannel) await Context.Client.GetChannelAsync(channelId);
                }
                else
                {
                    await Context.Channel.SendErrorMessageAsync(GetText("#administration_text_channel_not_found"));
                    return;
                }

                if (channel is null)
                {
                    await Context.Channel.SendErrorMessageAsync(GetText("#administration_text_channel_not_found"));
                    return;
                }

                var currentUser = await Context.Guild.GetCurrentUserAsync();
                var preconditions = currentUser.GetPermissions(channel);
                if (preconditions.ViewChannel)
                {
                    if (preconditions.SendMessages)
                    {
                        if (isEmbed)
                        {
                            await channel.SendMessageAsync(embed: embed.Build());
                        }
                        else
                        {
                            await channel.SendMessageAsync(message);
                        }

                        await ReplyConfirmationAsync("message_sent");
                    }
                    else
                    {
                        await ReplyErrorAsync("text_channel_no_permission_send");
                    }
                }
                else
                {
                    await Context.Channel.SendErrorMessageAsync(GetText("#administration_text_channel_no_permission_view"));
                }
            }
            else if (id.Contains("u:"))
            {
                id = id.Substring(2);
                if (ulong.TryParse(id, out var userId))
                {
                    var user = await Context.Client.GetUserAsync(userId);
                    try
                    {
                        if (isEmbed)
                        {
                            await user.SendMessageAsync(embed: embed.Build());
                        }
                        else
                        {
                            await user.SendMessageAsync(message);
                        }
                        await ReplyConfirmationAsync("message_sent");
                    }
                    catch
                    {
                        await ReplyErrorAsync("message_user_not_sent");
                    }
                }
                else
                {
                    await ReplyErrorAsync("user_not_found");
                }
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        public async Task EditAsync(string id, [Remainder]string message)
        {
            var isEmbed = Extensions.Extensions.TryParseEmbed(message, out var embed);
            var ids = id.Split("|");

            ITextChannel channel;
            IUserMessage msg;
            if (ulong.TryParse(ids[0], out var channelId))
            {
                channel = (ITextChannel) await Context.Client.GetChannelAsync(channelId);
            }
            else
            {
                await Context.Channel.SendErrorMessageAsync(GetText("#administration_text_channel_not_found"));
                return;
            }

            if (channel is null)
            {
                await Context.Channel.SendErrorMessageAsync(GetText("#administration_text_channel_not_found"));
                return;
            }

            var currentUser = await Context.Guild.GetCurrentUserAsync();
            var preconditions = currentUser.GetPermissions(channel);
            if (!preconditions.ViewChannel) return;

            if (ulong.TryParse(ids[1], out var messageId))
            {
                msg = (IUserMessage) await channel.GetMessageAsync(messageId);
            }
            else
            {
                await ReplyErrorAsync("message_not_found");
                return;
            }

            if (msg is null)
            {
                await ReplyErrorAsync("message_not_found");
                return;
            }

            if (msg.Author.Id != _client.CurrentUser.Id)
            {
                await ReplyErrorAsync("message_not_belong");
            }

            if (isEmbed)
            {
                await msg.ModifyAsync(m =>
                {
                    m.Content = null;
                    m.Embed = embed.Build();
                });
            }
            else
            {
                await msg.ModifyAsync(m =>
                {
                    m.Content = message;
                    m.Embed = null;
                });
            }

            await ReplyConfirmationAsync("message_edited");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        public async Task VotesAsync()
        {
            var votes = new List<string>();
            var index = 0;
            if (_votesService.VotesList != null)
            {
                foreach (var vote in _votesService.VotesList)
                {
                    var user = await Context.Client.GetUserAsync(vote.User);
                    votes.Add($"#{index+1} {user} ({vote.User})");
                    index++;
                }
                var pager = new PaginatedMessage
                {
                    Title = GetText("votes_list"),
                    Color = new Color(_creds.ConfirmColor),
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
            else
            {
                await ReplyErrorAsync("votes_manager_not_configured");
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        public async Task FindUserAsync([Remainder]string user)
        {
            IUser getUser;
            if (ulong.TryParse(user, out var id))
            {
                getUser = await Context.Client.GetUserAsync(id);
            }
            else
            {
                var userSplit = user.Split("#");
                if (userSplit.Length == 2)
                    getUser = await Context.Client.GetUserAsync(userSplit[0], userSplit[1]);
                else
                    getUser = null;
            }

            if (getUser is null)
            {
                var restClient = ((DiscordShardedClient) Context.Client).GetShardFor(Context.Guild).Rest;
                getUser = await restClient.GetUserAsync(id);
            }

            if (getUser is null)
            {
                await ReplyErrorAsync("user_not_found");
                return;
            }

            var mutualGuilds = 0;

            if (getUser is SocketUser socketUser)
            {
                mutualGuilds = socketUser.MutualGuilds.Count;
            }

            var accountCreated = getUser.CreatedAt.UtcDateTime.ToUniversalTime().ToString("dd MMM yyyy hh:mm tt");

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .AddField(GetText("#administration_user"), getUser, true)
                    .AddField(GetText("#administration_id"), getUser.Id, true)
                    .AddField(GetText("#utility_joined_discord"), accountCreated, true)
                    .AddField(GetText("mutual_guilds"), mutualGuilds, true)
                    .WithImageUrl(getUser.GetRealAvatarUrl());

            await Context.Channel.SendMessageAsync(embed: embed.Build());
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        //this commands doesn't use the translation strings, lazy, they will be added later
        public async Task EvaluateAsync([Remainder]string code)
        {
            var embed = new EmbedBuilder()
                .WithColor(_creds.ConfirmColor)
                .WithAuthor("Roslyn Compiler", Context.User.GetRealAvatarUrl())
                .WithDescription("Evaluating the C# code")
                .WithTimestamp(DateTimeOffset.UtcNow);

            var message = await Context.Channel.SendMessageAsync(embed: embed.Build());
            var evaluation = await Service.EvaluateAsync(Context, code);

            if (evaluation is null)
            {
                await message.DeleteAsync();
                return;
            }

            embed.AddField("Code", Format.Code(evaluation.Code, "csharp"));
            if (evaluation.Success)
            {
                embed.WithDescription("Code evaluated");
                embed.AddField(evaluation.ReturnType, Format.Code(evaluation.Result, "csharp"));
                embed.AddField("Compilation Time", $"{evaluation.CompilationTime.TotalMilliseconds} ms", true);
                embed.AddField("Execution Time", $"{evaluation.ExecutionTime.TotalMilliseconds} ms", true);
            }
            else
            {
                if (evaluation.IsCompiled)
                {
                    embed.WithDescription("Code compiled but threw an error on execution.");
                    embed.AddField("Exception", Format.Code(evaluation.Exception));
                    embed.AddField("Compilation time", $"{evaluation.CompilationTime.TotalMilliseconds} ms", true);
                    embed.AddField("Execution time", $"{evaluation.ExecutionTime.TotalMilliseconds} ms", true);
                }
                else
                {
                    embed.WithDescription("Code threw an error on compilation");
                    embed.AddField("Exception", Format.Code(evaluation.Exception));
                    embed.AddField("Compilation time", $"{evaluation.CompilationTime.TotalMilliseconds} ms", true);
                }
            }

            await message.ModifyAsync(m => m.Embed = embed.Build());
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        public async Task ExecuteSqlAsync([Remainder] string query)
        {
            var message = await ReplyConfirmationAsync("execute_sql");
            var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromMinutes(1));
            if (input != null)
            {
                if (input.Content.Equals(GetText("yes"), StringComparison.InvariantCultureIgnoreCase))
                {
                    using (var db = _db.GetDbContext())
                    {
                        var transaction = await db.Database.BeginTransactionAsync();
                        var rows = await db.Database.ExecuteSqlCommandAsync(query);
                        transaction.Commit();

                        var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
                        embed.WithAuthor(GetText("execute_sql_query"), Context.User.GetRealAvatarUrl());
                        embed.WithDescription(Format.Code(query));
                        embed.AddField(GetText("rows_affected"), Format.Code(rows.ToString()));

                        await message.DeleteAsync();
                        await Context.Message.DeleteAsync();
                        await Context.Channel.SendMessageAsync(embed: embed.Build());
                    }
                }
                else
                {
                    await message.DeleteAsync();
                    await ReplyErrorAsync("execution_aborted");
                }
            }
            else
            {
                await message.DeleteAsync();
                await ReplyErrorAsync("execution_aborted");
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        public async Task DownloadUsersAsync()
        {
            await Context.Guild.DownloadUsersAsync();
            await ReplyConfirmationAsync("users_downloaded", Context.Guild.Name);
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireOwner]
        public async Task DownloadUsersAsync(ulong guildId)
        {
            var guild = await Context.Client.GetGuildAsync(guildId);
            await guild.DownloadUsersAsync();
            await ReplyConfirmationAsync("users_downloaded", guild.Name);
        }
    }
}