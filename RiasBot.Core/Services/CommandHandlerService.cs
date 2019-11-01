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
            if (userMessage.Author.IsBot) return; // Ignore other bots

            var prefix = _creds.Prefix;
            GuildConfig guildDb = null;
            using (var db = _db.GetDbContext())
            {
                if (userMessage.Channel is SocketTextChannel textChannel)
                {
                    guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == textChannel.Guild.Id);
                    if (guildDb != null && !string.IsNullOrWhiteSpace(guildDb.Prefix))
                        prefix = guildDb.Prefix;

                    var userDb = db.Users.FirstOrDefault(x => x.UserId == userMessage.Author.Id);

                    _ = Task.Run(async () => await GiveXpAsync(userMessage, textChannel.Guild, userDb?.IsBlacklisted));
                    if (userDb != null && userDb.IsBanned)
                        return;
                }
            }

            _ = Task.Run(async () => await _botService.AddAssignableRoleAsync(userMessage.Author));

            var argPos = 0;
            if (!(userMessage.HasStringPrefix(prefix, ref argPos) ||
                  userMessage.HasStringPrefix($"{_client.CurrentUser.Username} ", ref argPos, StringComparison.InvariantCultureIgnoreCase) ||
                  userMessage.HasMentionPrefix(_client.CurrentUser, ref argPos)))
                return;

            var context = new ShardedCommandContext(_client, userMessage);
            var socketGuildUser = context.Guild?.CurrentUser;
            if (socketGuildUser != null)
            {
                var preconditions = socketGuildUser.GetPermissions((IGuildChannel) context.Channel);
                if (!preconditions.SendMessages) return;
            }

            var result = await _commands.ExecuteAsync(context, argPos, _provider);
            _loggingService.CommandArguments = userMessage.Content.Substring(argPos);

            if (guildDb != null && guildDb.DeleteCommandMessage)
                await userMessage.DeleteAsync();

            if (result.IsSuccess)
                RiasBot.CommandsExecuted++;

            if (result.Error == CommandError.UnmetPrecondition)
                _ = Task.Run(async () => await SendErrorResultAsync(context, userMessage, result));
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

        private async Task GiveXpAsync(SocketMessage msg, SocketGuild guild, bool? isUserBlacklisted)
        {
            var socketGuildUser = guild.GetUser(_client.CurrentUser.Id);
            var preconditions = socketGuildUser.GetPermissions((IGuildChannel)msg.Channel);

            if (isUserBlacklisted.HasValue && !isUserBlacklisted.Value)
                await _xpService.GiveXpUserMessageAsync((IGuildUser)msg.Author);

            await _xpService.GiveGuildXpUserMessageAsync((IGuildUser) msg.Author, msg.Channel, preconditions.SendMessages);
        }

        private async Task SendErrorResultAsync(ShardedCommandContext context, SocketMessage msg, IResult result)
        {
            var errorReason = result.ErrorReason;
            if (errorReason.StartsWith("#"))
            {
                if (errorReason.Contains("rate_limit"))
                    return;
                
                var errorReasonArgs = errorReason.Split(":");
                errorReason = _tr.GetText(context.Guild.Id, null, errorReasonArgs[0], errorReasonArgs.Skip(1).ToArray());
            }
            
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor).WithDescription(errorReason);
            await msg.Channel.SendMessageAsync(embed: embed.Build());
        }
    }
}