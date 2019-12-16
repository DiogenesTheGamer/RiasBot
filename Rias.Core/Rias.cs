using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;
using Rias.Core.Commons;
using Rias.Core.Database;
using Rias.Core.Implementation;
using Rias.Core.Services;
using Rias.Core.Services.Commons;
using Rias.Interactive;
using Serilog;
using Victoria;

namespace Rias.Core
{
    public class Rias
    {
        public const string Author = "Koneko#0001";
        public const string Version = "2.1.2";
        public static readonly Stopwatch UpTime = new Stopwatch();

        private DiscordShardedClient? _client;
        private CommandService? _commandService;
        private Credentials? _creds;

        public async Task InitializeAsync()
        {
#if GLOBAL
            Log.Information($"Initializing public RiasBot version {Version}");
#elif DEBUG || RELEASE
            Log.Information($"Initializing development RiasBot version {Version}");
#endif

            _creds = new Credentials();

            _client = new DiscordShardedClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
                TotalShards = 6,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 0,
                ExclusiveBulkDelete = true
            });
            _commandService = new CommandService(new CommandServiceConfiguration
            {
                DefaultRunMode = RunMode.Parallel,
                StringComparison = StringComparison.InvariantCultureIgnoreCase,
                CooldownBucketKeyGenerator = CooldownBucketKeyGenerator
            });

            var provider = InitializeServices();
            ApplyDatabaseMigrations(provider.GetRequiredService<RiasDbContext>());
            provider.GetRequiredService<LoggingService>();

            await StartAsync();

            provider.GetRequiredService<CommandHandlerService>();
            provider.GetRequiredService<BotService>();
            await provider.GetRequiredService<NsfwService>().InitializeAsync();

            if (_creds.PatreonConfig != null)
                await provider.GetRequiredService<PatreonService>().CheckPatronsAsync();
            if (_creds.VotesConfig != null)
                await provider.GetRequiredService<VotesService>().CheckVotesAsync();
        }

        private object? CooldownBucketKeyGenerator(object bucketType, CommandContext context)
        {
            var riasContext = (RiasCommandContext) context;
            
            // owner doesn't have cooldown
            if (_creds!.MasterId != 0 && riasContext.User.Id == _creds.MasterId)
                return null;
            
            return (BucketType) bucketType switch
            {
                BucketType.Guild => riasContext.Guild!.Id.ToString(),
                BucketType.User => riasContext.User.Id.ToString(),
                BucketType.GuildUser => $"{riasContext.Guild!.Id}_{riasContext.User.Id}",
                _ => null
            };
        }

        private IServiceProvider InitializeServices()
        {
            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commandService)
                .AddSingleton(_creds)
                .AddSingleton<Resources>()
                .AddSingleton<HttpClient>()
                .AddSingleton(new InteractiveService(_client!));

            if (_creds!.LavalinkConfig != null)
            {
                services.AddSingleton(new LavaNode<MusicPlayer>(_client, new LavaConfig
                {
                    Hostname = _creds!.LavalinkConfig.Host,
                    Port = _creds.LavalinkConfig.Port,
                    Authorization = _creds.LavalinkConfig.Authorization,
                    EnableResume = true,
                    ReconnectAttempts = 100,
                    ResumeKey = "Rias"
                }));
            }
            
            var connection = GetDatabaseConnection();
            if (connection is null)
                throw new NullReferenceException("The database connection is not set in credentials.json");

            services.AddDbContext<RiasDbContext>(x => x.UseNpgsql(connection).UseSnakeCaseNamingConvention(), ServiceLifetime.Transient);

            var attributeServices = typeof(Rias).Assembly!.GetTypes()
                .Where(x => typeof(RiasService).IsAssignableFrom(x)
                            && !x.GetTypeInfo().IsInterface
                            && !x.GetTypeInfo().IsAbstract)
                .ToArray();

            foreach (var serviceType in attributeServices)
            {
                services.AddSingleton(serviceType);
            }

            return services.BuildServiceProvider();
        }

        private string? GetDatabaseConnection()
        {
            if (_creds!.DatabaseConfig is null)
            {
                return null;
            }

            var connectionString = new StringBuilder();
            connectionString.Append("Host=").Append(_creds.DatabaseConfig.Host).Append(";");

            if (_creds.DatabaseConfig.Port > 0)
                connectionString.Append("Port=").Append(_creds.DatabaseConfig.Port).Append(";");

            connectionString.Append("Username=").Append(_creds.DatabaseConfig.Username).Append(";")
                .Append("Password=").Append(_creds.DatabaseConfig.Password).Append(";")
                .Append("Database=").Append(_creds.DatabaseConfig.Database).Append(";")
                .Append("ApplicationName=").Append(_creds.DatabaseConfig.ApplicationName);

            return connectionString.ToString();
        }

        private static void ApplyDatabaseMigrations(RiasDbContext dbContext)
        {
            if (!dbContext.Database.GetPendingMigrations().Any())
            {
                return;
            }

            dbContext.Database.Migrate();
            dbContext.SaveChanges();
        }

        private async Task StartAsync()
        {
#if GLOBAL
            Log.Information($"Running public RiasBot version {Version}");
#elif DEBUG || RELEASE
            Log.Information($"Running development RiasBot version {Version}");
#endif

            if (!VerifyCredentials()) return;

            await _client!.LoginAsync(TokenType.Bot, _creds!.Token);
            await _client.StartAsync();
            UpTime.Start();
        }

        private bool VerifyCredentials()
        {
            if (string.IsNullOrEmpty(_creds!.Token))
            {
                throw new NullReferenceException("You must set the token in credentials.json!");
            }

            if (!string.IsNullOrEmpty(_creds.Prefix)) return true;

            throw new NullReferenceException("You must set the default prefix in credentials.json!");
        }
    }
}