using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Disqord;
using Disqord.Events;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Qmmands;
using Rias.Core.Attributes;
using Rias.Core.Commons;
using Rias.Core.Database;
using Rias.Core.Extensions;
using Rias.Core.Implementation;
using Serilog;

namespace Rias.Core.Services
{
    [AutoStart]
    public class CommandHandlerService : RiasService
    {
        private readonly CommandService _commandService;
        private readonly BotService _botService;
        private readonly CooldownService _cooldownService;
        private readonly XpService _xpService;
        
        private readonly string _commandsPath = Path.Combine(Environment.CurrentDirectory, "data/commands.json");
        public static int CommandsAttempted;
        public static int CommandsExecuted;

        private List<Type> _typeParsers = new List<Type>();

        public CommandHandlerService(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _commandService = serviceProvider.GetRequiredService<CommandService>();
            _botService = serviceProvider.GetRequiredService<BotService>();
            _cooldownService = serviceProvider.GetRequiredService<CooldownService>();
            _xpService = serviceProvider.GetRequiredService<XpService>();
            
            LoadCommands();
            LoadTypeParsers();

            RiasBot.MessageReceived += MessageReceivedAsync;
        }
        
        private void LoadCommands()
        {
            var sw = Stopwatch.StartNew();
            var modulesInfo = JObject.Parse(File.ReadAllText(_commandsPath))
                .SelectToken("modules")?
                .ToObject<List<ModuleInfo>>();

            if (modulesInfo is null)
            {
                throw new KeyNotFoundException("The modules node array couldn't be loaded");
            }

            var assembly = Assembly.GetAssembly(typeof(Rias));
            _commandService.AddModules(assembly, null, module => SetUpModule(module, modulesInfo));

            sw.Stop();
            Log.Information($"Commands loaded: {sw.ElapsedMilliseconds} ms");
        }

        private void SetUpModule(ModuleBuilder module, IReadOnlyList<ModuleInfo> modulesInfo)
        {
            if (string.IsNullOrEmpty(module.Name))
                return;
            
            var moduleInfo = modulesInfo.FirstOrDefault(x => string.Equals(x.Name, module.Name, StringComparison.InvariantCultureIgnoreCase));
            if (moduleInfo is null)
                return;

            if (!string.IsNullOrEmpty(moduleInfo.Aliases))
            {
                foreach (var moduleAlias in moduleInfo.Aliases.Split(" "))
                {
                    module.AddAlias(moduleAlias);
                }
            }

            if (!moduleInfo.Commands.Any())
                return;
            
            SetUpCommands(module, moduleInfo.Commands.ToList());

            foreach (var submodule in module.Submodules)
            {
                SetUpModule(submodule, moduleInfo.Submodules.ToList());
            }
        }

        private void SetUpCommands(ModuleBuilder module, IReadOnlyList<CommandInfo> commandsInfo)
        {
            foreach (var command in module.Commands)
            {
                Func<CommandInfo, bool> predicate;
                if (command.Aliases.Count != 0)
                {
                    predicate = x => !string.IsNullOrEmpty(x.Aliases) && x.Aliases.Split(" ")
                                         .Any(y => string.Equals(y, command.Aliases.First(), StringComparison.InvariantCultureIgnoreCase));
                }
                else
                {
                    predicate = x => string.IsNullOrEmpty(x.Aliases);
                }

                var commandInfo = commandsInfo.FirstOrDefault(predicate);
                if (commandInfo is null) continue;

                if (!string.IsNullOrEmpty(commandInfo.Aliases))
                {
                    foreach (var commandAlias in commandInfo.Aliases.Split(" "))
                    {
                        if (command.Aliases.Contains(commandAlias))
                            continue;
                        
                        command.AddAlias(commandAlias);
                    }
                }

                command.Description = commandInfo.Description;
                command.Remarks = string.Join("\n", commandInfo.Remarks!);
            }
        }

        private void LoadTypeParsers()
        {
            const string parserInterface = "ITypeParser";

            var typeParserInterface = _commandService.GetType().Assembly.GetTypes()
                .FirstOrDefault(x => x.Name == parserInterface);

            if (typeParserInterface is null)
                throw new NullReferenceException(parserInterface);

            var assembly = typeof(Rias).Assembly;
            _typeParsers = assembly!.GetTypes()
                .Where(x => typeParserInterface.IsAssignableFrom(x)
                            && !x.GetTypeInfo().IsInterface
                            && !x.GetTypeInfo().IsAbstract)
                .ToList();

            foreach (var typeParser in _typeParsers)
            {
                var methodInfo = typeof(CommandService).GetMethods()
                    .First(m => m.Name == "AddTypeParser" && m.IsGenericMethodDefinition);

                var targetBase = typeParser.BaseType ?? typeParser;
                var targetType = targetBase.GetGenericArguments()[0];

                var genericMethodInfo = methodInfo.MakeGenericMethod(targetType);
                genericMethodInfo.Invoke(_commandService, new[] { Activator.CreateInstance(typeParser), false });
            }
        }
        
        private async Task MessageReceivedAsync(MessageReceivedEventArgs args)
        {
            if (!(args.Message is CachedUserMessage userMessage)) return;
            if (userMessage.Author.IsBot) return;
            
            var guildChannel = userMessage.Channel as CachedTextChannel;
            if (guildChannel != null)
            {
                await RunTaskAsync(_botService.AddAssignableRoleAsync((CachedMember) userMessage.Author));
                await RunTaskAsync(_xpService.AddUserXpAsync(userMessage.Author));
                await RunTaskAsync(_xpService.AddGuildUserXpAsync((CachedMember) userMessage.Author, userMessage.Channel));
            }
            
            var prefix = await GetGuildPrefixAsync(guildChannel?.Guild);
            if (CommandUtilities.HasPrefix(userMessage.Content, prefix, out var output)
                || RiasUtilities.HasMentionPrefix(userMessage, out output))
            {
                await RunTaskAsync(ExecuteCommandAsync(userMessage, userMessage.Channel, prefix, output));
                return;
            }

            if (userMessage.Client.CurrentUser is null)
                return;
            
            if (CommandUtilities.HasPrefix(userMessage.Content,userMessage.Client.CurrentUser.Name, StringComparison.InvariantCultureIgnoreCase, out output))
                await RunTaskAsync(ExecuteCommandAsync(userMessage, userMessage.Channel, prefix, output));
        }

        private async Task ExecuteCommandAsync(CachedUserMessage userMessage, ICachedMessageChannel? channel, string prefix, string output)
        {
            if (await CheckUserBan(userMessage.Author) && userMessage.Author.Id != Credentials.MasterId)
                return;

            var guildChannel = channel as CachedTextChannel;
            if (guildChannel != null)
            {
                var channelPermissions = guildChannel.Guild.CurrentMember.GetPermissionsFor(guildChannel);
                if (!channelPermissions.SendMessages)
                    return;

                if (!guildChannel.Guild.CurrentMember.Permissions.EmbedLinks)
                {
                    await guildChannel.SendMessageAsync(GetText(guildChannel.Guild.Id, Localization.ServiceNoEmbedLinksPermission));
                    return;
                }

                if (!channelPermissions.EmbedLinks)
                {
                    await guildChannel.SendMessageAsync(GetText(guildChannel.Guild.Id, Localization.ServiceNoEmbedLinksChannelPermission));
                    return;
                }
            }
            
            var context = new RiasCommandContext(userMessage, RiasBot, prefix);
            var result = await _commandService.ExecuteAsync(output, context);
            
            if (result.IsSuccessful)
            {
                if (guildChannel != null
                    && guildChannel.Guild.CurrentMember.Permissions.ManageMessages
                    && await CheckGuildCommandMessageDeletion(guildChannel.Guild)
                    && !string.Equals(context.Command.Name, "prune"))
                {
                    await userMessage.DeleteAsync();
                }
                
                CommandsExecuted++;
                return;
            }
            
            CommandsAttempted++;

            switch (result)
            {
                case OverloadsFailedResult overloadsFailedResult:
                    await RunTaskAsync(SendFailedResultsAsync(context, overloadsFailedResult.FailedOverloads.Values));
                    break;
                case ChecksFailedResult _:
                case TypeParseFailedResult _:
                case ArgumentParseFailedResult _:
                    await RunTaskAsync(SendFailedResultsAsync(context, new []{ (FailedResult) result }));
                    break;
                case CommandOnCooldownResult commandOnCooldownResult:
                    await RunTaskAsync(SendCommandOnCooldownMessageAsync(context, commandOnCooldownResult));
                    break;
            }
        }

        private Task SendFailedResultsAsync(RiasCommandContext context, IEnumerable<FailedResult> failedResults)
        {
            var guildId = context.Guild?.Id;
            var embed = new LocalEmbedBuilder
            {
                Color = RiasUtilities.ErrorColor,
                Title = GetText(guildId, Localization.ServiceCommandNotExecuted)
            };

            var reasons = new List<string>();
            var parsedPrimitiveType = false;
            var areTooManyArguments = false;
            var areTooLessArguments = false;
            
            foreach (var failedResult in failedResults)
            {
                switch (failedResult)
                {
                    case ChecksFailedResult checksFailedResult:
                        reasons.AddRange(checksFailedResult.FailedChecks.Select(x => x.Result.Reason));
                        break;
                    case TypeParseFailedResult typeParseFailedResult:
                        if (_typeParsers.Any(x => x.BaseType!.GetGenericArguments()[0] == typeParseFailedResult.Parameter.Type))
                        {
                            reasons.Add(typeParseFailedResult.Reason);
                        }
                        else if (!parsedPrimitiveType)
                        {
                            reasons.Add(GetText(guildId, Localization.TypeParserPrimitiveType, context.Prefix, typeParseFailedResult.Parameter.Command.Name));
                            parsedPrimitiveType = true;
                        }
                        break;
                    case ArgumentParseFailedResult argumentParseFailedResult:
                        var rawArguments = Regex.Matches(argumentParseFailedResult.RawArguments, @"\w+|""[\w\s]*""");
                        var parameters = argumentParseFailedResult.Command.Parameters;

                        if (!areTooLessArguments && rawArguments.Count < parameters.Count)
                        {
                            reasons.Add(GetText(guildId, Localization.ServiceCommandLessArguments, context.Prefix, argumentParseFailedResult.Command.Name));
                            areTooLessArguments = true;
                        }
                        
                        if (!areTooManyArguments && rawArguments.Count > parameters.Count)
                        {
                            reasons.Add(GetText(guildId, Localization.ServiceCommandManyArguments, context.Prefix, argumentParseFailedResult.Command.Name));
                            areTooManyArguments = true;
                        }
                        break;
                }
            }

            if (reasons.Count == 0)
                return Task.CompletedTask;

            embed.WithDescription($"**{GetText(guildId, reasons.Count == 1 ? Localization.CommonReason : Localization.CommonReasons)}**:\n" +
                                  string.Join("\n", reasons.Select(x => $"• {x}")));
            return context.Channel.SendMessageAsync(embed);
        }
        
        private async Task SendCommandOnCooldownMessageAsync(RiasCommandContext context, CommandOnCooldownResult result)
        {
            var (cooldown, retryAfter) = result.Cooldowns[0];
            var cooldownKey = (BucketType) cooldown.BucketType switch
            {
                BucketType.Guild => _cooldownService.GenerateKey(context.Command.Name, context.Guild!.Id),
                BucketType.User => _cooldownService.GenerateKey(context.Command.Name, context.User.Id),
                BucketType.Member => _cooldownService.GenerateKey(context.Command.Name, context.Guild!.Id, context.User.Id),
                BucketType.Channel => _cooldownService.GenerateKey(context.Command.Name, context.Channel.Id),
                _ => string.Empty
            };
            
            if (_cooldownService.Has(cooldownKey))
                return;

            _cooldownService.Add(cooldownKey);
            
            retryAfter += TimeSpan.FromSeconds(1);
            await context.Channel.SendErrorMessageAsync(GetText(context.Guild?.Id, Localization.ServiceCommandCooldown,
                retryAfter.Humanize(culture: new CultureInfo(Localization.GetGuildLocale(context.Guild?.Id)), minUnit: TimeUnit.Second)));
            
            await Task.Delay(retryAfter);
            _cooldownService.Remove(cooldownKey);
        }
        
        private async Task<string> GetGuildPrefixAsync(CachedGuild? guild)
        {
            if (guild is null)
                return Credentials.Prefix;

            using var scope = RiasBot.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RiasDbContext>();
            var prefix = (await db.Guilds.FirstOrDefaultAsync(x => x.GuildId == guild.Id))?.Prefix;
            
            return !string.IsNullOrEmpty(prefix) ? prefix : Credentials.Prefix;
        }
        
        private async Task<bool> CheckGuildCommandMessageDeletion(CachedGuild guild)
        {
            using var scope = RiasBot.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RiasDbContext>();
            return (await db.Guilds.FirstOrDefaultAsync(x => x.GuildId == guild.Id))?.DeleteCommandMessage ?? false;
        }
        
        private async Task<bool> CheckUserBan(CachedUser user)
        {
            using var scope = RiasBot.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RiasDbContext>();
            return (await db.Users.FirstOrDefaultAsync(x => x.UserId == user.Id))?.IsBanned ?? false;
        }
        
        public class ModuleInfo
        {
            public string? Name { get; set; }
            public string? Aliases { get; set; }
            public IEnumerable<CommandInfo>? Commands { get; set; }
            public IEnumerable<ModuleInfo>? Submodules { get; set; }
        }

        public class CommandInfo
        {
            public string? Aliases { get; set; }
            public string? Description { get; set; }
            public IEnumerable<string>? Remarks { get; set; }
        }
    }
}