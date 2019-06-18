using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RiasBot.Commons.Attributes;
using RiasBot.Services;
using RiasBot.Services.Implementation;
using Victoria;

namespace RiasBot
{
    public class RiasBot
    {
        public const string Author = "Koneko#0001";
        public const string Version = "2.0.0-alpha1";

        public static uint ConfirmColor { get; set; }
        public static uint ErrorColor { get; set; }

        public static int CommandsExecuted = 0;
        public static readonly Stopwatch UpTime = new Stopwatch();

        public async Task StartAsync()
        {
            var credentials = new BotCredentials();

            var services = new ServiceCollection()
                .AddSingleton(new DiscordShardedClient(new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Info
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    DefaultRunMode = RunMode.Async,
                    LogLevel = LogSeverity.Verbose
                }))
                .AddSingleton<IBotCredentials>(credentials)
                .AddSingleton<LavaShardClient>();

            var assembly = Assembly.GetAssembly(typeof(RiasBot));

            var attributeServices = assembly.GetTypes()
                .Where(x => x.GetCustomAttribute<ServiceAttribute>() != null
                            && !x.GetTypeInfo().IsInterface && !x.GetTypeInfo().IsAbstract)
                .ToList();

            foreach (var type in attributeServices)
            {
                var implementation = type.GetCustomAttribute<ServiceAttribute>().Implementation;
                services.AddSingleton(implementation != null ? implementation : type, type);
            }

            var provider = services.BuildServiceProvider();

            provider.GetRequiredService<LoggingService>();
            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();
            provider.GetRequiredService<BotService>();
            provider.GetRequiredService<DbService>();
            await provider.GetRequiredService<VotesService>().ConfigureVotesWebSocket();

            await Task.Delay(-1);
        }
    }
}

//TODO: don't await Task.Run