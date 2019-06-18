using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Services;

namespace RiasBot.Modules.Help
{
    public class Help : RiasModule
    {
        private readonly CommandHandler _ch;
        private readonly CommandService _service;
        private readonly IBotCredentials _creds;

        public Help(CommandHandler ch, CommandService service, IBotCredentials creds)
        {
            _ch = ch;
            _service = service;
            _creds = creds;
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        public async Task HelpAsync()
        {
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
            embed.WithAuthor(GetText("title", Context.Client.CurrentUser.Username, RiasBot.Version), Context.Client.CurrentUser.GetRealAvatarUrl());

            embed.WithDescription(GetText("info_1") +
                                  GetText("info_2", _ch.GetPrefix(Context.Guild)) +
                                  GetText("info_3", _ch.GetPrefix(Context.Guild)) +
                                  GetText("info_4", _ch.GetPrefix(Context.Guild)) +
                                  GetText("info_5", _ch.GetPrefix(Context.Guild)));

            var links = new StringBuilder();
            const string delimiter = " • ";

            if (!string.IsNullOrEmpty(_creds.Invite))
                links.Append(GetText("invite_me", _creds.Invite));

            if (links.Length > 0) links.Append(delimiter);
            if (!string.IsNullOrEmpty(_creds.OwnerServerInvite))
            {
                var ownerServer = await Context.Client.GetGuildAsync(_creds.OwnerServerId);
                links.Append(GetText("support_server", ownerServer.Name, _creds.OwnerServerInvite));
            }

            if (links.Length > 0) links.Append(delimiter);
            if (!string.IsNullOrEmpty(_creds.Website))
                links.Append(GetText("website", _creds.Website));

            if (links.Length > 0) links.Append(delimiter);
            if (!string.IsNullOrEmpty(_creds.Patreon))
                links.Append(GetText("donate", _creds.Patreon));

            embed.AddField(GetText("links"), links.ToString());
            embed.WithFooter("© 2018 Copyright: Koneko#0001");
            await Context.Channel.SendMessageAsync(embed: embed.Build());
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [Priority(0)]
        public async Task HelpAsync(string name)
        {
            name = name?.Trim();
            var command = _service.Commands.FirstOrDefault(x => x.Aliases.Any(y => y.Equals(name, StringComparison.InvariantCultureIgnoreCase)));

            if (command is null)
            {
                await ReplyErrorAsync("command_not_found", _ch.GetPrefix(Context.Guild));
                return;
            }

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);

            var aliases = string.Join("/ ", command.Aliases.Select(x => _ch.GetPrefix(Context.Guild) + x));
            embed.WithTitle(aliases);

            var summary = command.Summary;
            summary = summary.Replace("[prefix]", _ch.GetPrefix(Context.Guild));
            summary = summary.Replace("[currency]", _creds.Currency);
            embed.WithDescription(summary);

            var requirements = GetCommandRequirements(command);
            if (!string.IsNullOrEmpty(requirements))
                embed.AddField(GetText("requires"), requirements, true);

            var module = command.Module.IsSubmodule ? $"{command.Module.Parent.Name} -> {command.Module.Name}" : $"{command.Module.Name}";
            embed.AddField(GetText("module"), module, true);

            embed.AddField(GetText("example"), command.Remarks.Replace("[prefix]", _ch.GetPrefix(Context.Guild)));
            embed.WithCurrentTimestamp();

            await Context.Channel.SendMessageAsync(embed: embed.Build());
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        public async Task ModulesAsync()
        {
            var embed = new EmbedBuilder();
            embed.WithColor(_creds.ConfirmColor);
            embed.WithTitle(GetText("modules_list"));

            var modules = _service.Modules.GroupBy(m => m.GetModule()).Select(m => m.Key).OrderBy(m => m.Name).ToList();

            foreach (var module in modules)
            {
                var submodules = string.Join("\n ", module.Submodules.Select(submodule => "•" + submodule.Name));
                embed.AddField(Format.Bold($"•{module.Name}"),
                    string.IsNullOrEmpty(submodules) ? "-" : submodules,
                    true);
            }

            embed.WithFooter(GetText("modules_info", _ch.GetPrefix(Context.Guild)));
            embed.WithCurrentTimestamp();
            await Context.Channel.SendMessageAsync(embed: embed.Build());
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        public async Task CommandsAsync([Remainder]string name)
        {
            var module = _service.Modules.FirstOrDefault(m => m.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase));
            if (module is null)
            {
                await ReplyErrorAsync("module_not_found", _ch.GetPrefix(Context.Guild));
                return;
            }

            var isSubmodule = module.IsSubmodule;
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);

            var moduleCommands = GetModuleCommands(module);
            var commands = GetCommands(moduleCommands);

            embed.WithTitle(GetText(isSubmodule ? "all_commands_for_submodule" : "all_commands_for_module", module.Name));
            embed.AddField(module.Name, string.Join("\n", commands), true);

            if (!isSubmodule)
            {
                foreach (var submodule in module.Submodules)
                {
                    var submoduleCommands = GetModuleCommands(submodule);
                    var commandsSb = GetCommands(submoduleCommands);

                    embed.AddField(submodule.Name, string.Join("\n", commandsSb), true);
                }
            }
            embed.WithFooter(GetText("command_info", _ch.GetPrefix(Context.Guild)));
            embed.WithCurrentTimestamp();
            await Context.Channel.SendMessageAsync(embed: embed.Build());
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task AllCommandsAsync()
        {
            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);

            foreach (var module in _service.Modules.OrderBy(m => m.Name))
            {
                var moduleCommands = GetModuleCommands(module);
                var commands = GetCommands(moduleCommands).ToList();

                embed.WithTitle("All commands");
                if (commands.Any())
                    embed.AddField(module.Name, string.Join("\n", commands), true);

                foreach (var submodule in module.Submodules.OrderBy(sb => sb.Name))
                {
                    var submoduleCommands = GetModuleCommands(submodule);
                    var commandsSb = GetCommands(submoduleCommands);
                    embed.AddField(submodule.Name, string.Join("\n", commandsSb), true);
                }

                if (embed.Fields.Count <= 20) continue;

                var received = await SendAllCommandsMessageAsync(embed);
                if (!received) return;

                embed = new EmbedBuilder().WithColor(_creds.ConfirmColor);
            }

            await SendAllCommandsMessageAsync(embed);
        }

        private string GetCommandRequirements(CommandInfo cmd)
        {
            return string.Join(", ", cmd.Preconditions.Where(precondition => precondition is RequireOwnerAttribute || precondition is RequireUserPermissionAttribute)
                .Select(precondition =>
                {
                    if (precondition is RequireOwnerAttribute)
                        return GetText("bot_owner");

                    var preconditionAttribute = (RequireUserPermissionAttribute) precondition;
                    if (preconditionAttribute.GuildPermission != null)
                        return preconditionAttribute.GuildPermission.ToString();

                    return preconditionAttribute.ChannelPermission.ToString();
                }));
        }

        /// <summary>
        /// Get an ordered enumerable with all commands from a module or submodule
        /// </summary>
        private IEnumerable<CommandInfo> GetModuleCommands(ModuleInfo module) =>
            module.Commands.GroupBy(c => c.Name).Select(x => x.First()).OrderBy(n => n.Name);

        /// <summary>
        /// Get an enumerable with all aliases from a command (prefix included): name [alias1, alias2...]
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        private IEnumerable<string> GetCommands(IEnumerable<CommandInfo> commands)
        {
            return commands.Select(x =>
            {
                var nextAlias = string.Join(", ", x.Aliases.Skip(1).Select(a => _ch.GetPrefix(Context.Guild) + a));
                if (!string.IsNullOrEmpty(nextAlias))
                    nextAlias = "[" + nextAlias + "]";

                return $"{_ch.GetPrefix(Context.Guild) + x.Aliases.First()} {nextAlias}";
            });
        }

        private async Task<bool> SendAllCommandsMessageAsync(EmbedBuilder embed)
        {
            embed.WithFooter(GetText("command_info", _ch.GetPrefix(Context.Guild)));
            embed.WithCurrentTimestamp();
            try
            {
                await Context.User.SendMessageAsync(embed: embed.Build());
            }
            catch
            {
                await ReplyErrorAsync("commands_not_sent");
                return false;
            }

            return true;
        }
    }
}