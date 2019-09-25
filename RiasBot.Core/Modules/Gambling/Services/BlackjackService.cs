using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Modules.Gambling.Commons;
using RiasBot.Services;

namespace RiasBot.Modules.Gambling.Services
{
    [Service]
    public class BlackjackService
    {
        public readonly DiscordShardedClient Client;
        public readonly IBotCredentials Creds;
        public readonly DbService Db;
        public readonly ITranslations Translations;

        private readonly ConcurrentDictionary<ulong, BlackjackGame> _blackjackGames = new ConcurrentDictionary<ulong, BlackjackGame>();

        public BlackjackService(DiscordShardedClient client, IBotCredentials creds, DbService db, ITranslations translations)
        {
            Client = client;
            Creds = creds;
            Db = db;
            Translations = translations;

            client.ReactionAdded += OnReactionAddedAsync;
            client.ReactionRemoved += OnReactionRemovedAsync;
        }

        public BlackjackGame GetOrCreateGame(IGuildUser user)
        {
            var bj =  _blackjackGames.GetOrAdd(user.Id, new BlackjackGame(this));
            return bj;
        }

        public BlackjackGame GetGame(IGuildUser user)
        {
            _blackjackGames.TryGetValue(user.Id, out var bj);
            return bj;
        }

        public BlackjackGame RemoveGame(IGuildUser user)
        {
            _blackjackGames.TryRemove(user.Id, out var bj);
            return bj;
        }

        private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!reaction.User.IsSpecified)
                return;

            var bj = GetGame((IGuildUser)reaction.User.Value);
            if (bj != null)
            {
                await bj.UpdateGameAsync(reaction);
            }
        }

        private async Task OnReactionRemovedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!reaction.User.IsSpecified)
                return;

            var bj = GetGame((IGuildUser)reaction.User.Value);
            if (bj != null)
            {
                if (!bj.ManageMessagesPermission)
                {
                    await bj.UpdateGameAsync(reaction);
                }
            }
        }

        public int GetCurrency(IGuildUser user)
        {
            using (var db = Db.GetDbContext())
            {
                var userDb = db.Users.FirstOrDefault(x => x.UserId == user.Id);
                return userDb?.Currency ?? 0;
            }
        }
    }
}