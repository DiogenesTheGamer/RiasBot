using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;

namespace RiasBot.Services
{
    [Service]
    public class StartupService
    {
        private readonly CommandService _commands;
        private readonly IBotCredentials _creds;
        private readonly DiscordShardedClient _discord;
        private readonly IServiceProvider _provider;

        public StartupService(DiscordShardedClient discord, CommandService commands, IServiceProvider provider, IBotCredentials creds)
        {
            _creds = creds;
            _discord = discord;
            _provider = provider;
            _commands = commands;
        }

        public async Task StartAsync()
        {
            if (!VerifyCredentials()) return;

            var discordToken = _creds.Token;
            await _discord.LoginAsync(TokenType.Bot, discordToken);
            await _discord.StartAsync();

            LoadTypeReaders();

            await _commands.AddModulesAsync(Assembly.GetAssembly(typeof(RiasBot)), _provider);

            RiasBot.UpTime.Start();

            await Task.Delay(10000);
        }

        private void LoadTypeReaders()
        {
            var assembly = Assembly.GetAssembly(typeof(RiasBot));
            var typeReaders = assembly.GetTypes()
                .Where(x => x.IsSubclassOf(typeof(TypeReader))
                            && x.BaseType.GetGenericArguments().Length > 0
                            && !x.IsAbstract);

            foreach (var type in typeReaders)
            {
                var typeReader = (TypeReader) Activator.CreateInstance(type, _discord, _commands);
                var baseType = type.BaseType;
                var typeArgs = baseType.GetGenericArguments();

                _commands.AddTypeReader(typeArgs[0], typeReader);
            }
        }

        private bool VerifyCredentials()
        {
            if (string.IsNullOrEmpty(_creds.Token))
            {
                Console.WriteLine("You must set the token in credentials.json!");
                return false;
            }

            if (string.IsNullOrEmpty(_creds.Prefix))
            {
                Console.WriteLine("You must set the default prefix in credentials.json!");
                return false;
            }

            return true;
        }
    }
}