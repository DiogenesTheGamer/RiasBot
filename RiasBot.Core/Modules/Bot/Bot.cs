using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Music.Services;
using RiasBot.Services;

namespace RiasBot.Modules.Bot
{
    public partial class Bot : RiasModule
    {
        private readonly DiscordShardedClient _client;
        private readonly IServiceProvider _services;
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly InteractiveService _is;
        private readonly VotesService _votesService;
        private readonly MusicService _musicService;

        public Bot(DiscordShardedClient client, IServiceProvider services, DbService db,
            IBotCredentials creds, InteractiveService interactiveService, VotesService votesService, MusicService musicService)
        {
            _services = services;
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
        public async Task EvaluateAsync([Remainder]string expression)
        {
            var globals = new Globals
            {
                Context = Context,
                Client = _client,
                Services = _services,
                Db = _db
            };
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
            try
            {
                var result = await CSharpScript.EvaluateAsync(expression,
                    ScriptOptions.Default.WithReferences(typeof(RiasBot).Assembly)
                        .WithImports("System", "System.Collections.Generic", "System.Linq", "Discord", "System.Threading.Tasks", "System.Text",
                            "Discord.WebSocket", "RiasBot.Extensions"), globals);

                embed.WithAuthor("Success", Context.User.GetRealAvatarUrl());
                embed.AddField("Code", Format.Code(expression, "csharp"));
                if (result != null)
                {
                    var resultMessage = result.ToString();
                    if (result is IEnumerable enumerable)
                    {
                        var enumType = enumerable.GetType();
                        resultMessage = $"{enumType.Name}<{string.Join(", ", enumType.GenericTypeArguments.Select(t => t.Name))}> " +
                                        $"{{{string.Join(", ", enumerable.Cast<object>())}}}";
                    }

                    embed.AddField("Result", Format.Code(resultMessage, "csharp"));
                    await Context.Channel.SendMessageAsync(embed: embed.Build());
                }
            }
            catch (Exception e)
            {
                embed.WithAuthor("Failed", Context.User.GetRealAvatarUrl());
                embed.AddField("CompilationErrorException", Format.Code(e.Message, "csharp"));
                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }
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
        
        public class Globals
        {
            public ICommandContext Context { get; set; }
            public DiscordShardedClient Client { get; set; }
            public IServiceProvider Services { get; set; }
            public DbService Db { get; set; }
        }
    }
}