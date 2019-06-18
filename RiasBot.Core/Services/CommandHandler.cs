using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Database.Models;
using RiasBot.Modules.Xp.Services;

namespace RiasBot.Services
{
    [Service]
    public class CommandHandler
    {
        private readonly DiscordShardedClient _client;
        private readonly CommandService _commands;
        private readonly IBotCredentials _creds;
        private readonly ITranslations _tr;
        private readonly DbService _db;

        private readonly LoggingService _loggingService;
        private readonly BotService _botService;
        private readonly XpService _xpService;

        private readonly IServiceProvider _provider;

        public CommandHandler(DiscordShardedClient client, CommandService commands, IBotCredentials creds, ITranslations translations,
            DbService db, LoggingService loggingService, BotService botService, XpService xpService, IServiceProvider provider)
        {
            _client = client;
            _commands = commands;
            _creds = creds;
            _tr = translations;
            _db = db;

            _loggingService = loggingService;
            _botService = botService;
            _xpService = xpService;

            _provider = provider;

            _client.MessageReceived += MessageReceivedAsync;
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            if (!(message is SocketUserMessage userMessage)) return; // Ensure the message is from a user/bot
            if (userMessage.Author.Id == _client.CurrentUser.Id) return; // Ignore self when checking commands
            if (userMessage.Author.IsBot) return; // Ignore other bots

            var context = new ShardedCommandContext(_client, userMessage);

            GuildConfig guildDb = null;
            using (var db = _db.GetDbContext())
            {
                if (!context.IsPrivate)
                {
                    guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == context.Guild.Id);
                    var userDb = db.Users.FirstOrDefault(x => x.UserId == userMessage.Author.Id);

                    _ = Task.Run(async () => await GiveXpAsync(context, userMessage, userDb));

                    if (guildDb != null)
                    {
                        var currentUser = context.Guild.CurrentUser;
                        if (currentUser.GuildPermissions.ManageRoles)
                            await _botService.AddAssignableRoleAsync(guildDb, (IGuildUser) context.User, currentUser);
                    }

                    if (userDb != null)
                    {
                        if (userDb.IsBanned)
                            return; //banned users cannot use the commands
                    }
                }
            }

            var prefix = GetPrefix(guildDb, context.IsPrivate);
            
            var argPos = 0;

            if (userMessage.HasStringPrefix(prefix, ref argPos) ||
                userMessage.HasStringPrefix($"{context.Client.CurrentUser.Username} ", ref argPos, StringComparison.InvariantCultureIgnoreCase) ||
                userMessage.HasMentionPrefix(context.Client.CurrentUser, ref argPos))
            {
                var socketGuildUser = context.Guild?.CurrentUser;
                if (socketGuildUser != null)
                {
                    var preconditions = socketGuildUser.GetPermissions((IGuildChannel) context.Channel);
                    if (!preconditions.SendMessages) return;
                }

                var result = await _commands.ExecuteAsync(context, argPos, _provider);
                _loggingService.CommandArguments = userMessage.Content.Substring(argPos);

                if (guildDb != null)
                    if (guildDb.DeleteCommandMessage)
                        await userMessage.DeleteAsync();

                if (result.IsSuccess)
                    RiasBot.CommandsExecuted++;
                else if (result.Error == CommandError.UnmetPrecondition ||
                         result.Error == CommandError.Exception)
                    _ = Task.Run(async () => await SendErrorResultAsync(context, userMessage, result));
            }
        }

        public string GetPrefix(IGuild guild, bool isPrivate = false)
        {
            if (isPrivate) return _creds.Prefix;

            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == guild.Id);
                if (guildDb != null)
                {
                    return !string.IsNullOrEmpty(guildDb.Prefix) ? guildDb.Prefix : _creds.Prefix;
                }
            }

            return _creds.Prefix;
        }

        public string GetPrefix(GuildConfig guildDb, bool isPrivate = false)
        {
            if (isPrivate) return _creds.Prefix;

            if (guildDb != null)
            {
                return !string.IsNullOrEmpty(guildDb.Prefix) ? guildDb.Prefix : _creds.Prefix;
            }

            return _creds.Prefix;
        }

        private async Task GiveXpAsync(ShardedCommandContext context, SocketUserMessage msg, UserConfig userDb)
        {
            if (!context.IsPrivate)
            {
                var socketGuildUser = context.Guild.GetUser(_client.CurrentUser.Id);
                var preconditions = socketGuildUser.GetPermissions((IGuildChannel)context.Channel);

                if (!userDb.IsBlacklisted)
                    await _xpService.GiveXpUserMessageAsync((IGuildUser)msg.Author);

                await _xpService.GiveGuildXpUserMessageAsync((IGuildUser) msg.Author, context.Channel, preconditions.SendMessages);
            }
        }

        private async Task SendErrorResultAsync(ShardedCommandContext context, SocketMessage msg, IResult result)
        {
            var errorReason = result.ErrorReason;
            if (errorReason.StartsWith("#"))
            {
                var errorReasonArgs = errorReason.Split(":");
                errorReason = _tr.GetText(context.Guild.Id, null, errorReasonArgs[0], errorReasonArgs.Skip(1).ToArray());
            }
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor).WithDescription(errorReason);
            var timeoutMsg = await msg.Channel.SendMessageAsync(embed: embed.Build());
            await Task.Delay(10000);
            await timeoutMsg.DeleteAsync();
        }
    }
}