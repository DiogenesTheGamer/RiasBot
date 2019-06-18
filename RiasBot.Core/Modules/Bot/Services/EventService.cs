using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Services;
using RiasBot.Database.Models;

namespace RiasBot.Modules.Bot.Services
{
    [Service]
    public class EventService
    {
        private readonly DiscordShardedClient _client;
        private readonly IBotCredentials _creds;
        private readonly DbService _db;

        private Queue<IUser> _heartUsers;
        private IEmote _heart;
        private IUserMessage _message;
        
        private int _reward;
        private bool _eventStarted;
        
        public EventService(DiscordShardedClient client, IBotCredentials creds, DbService db)
        {
            _client = client;
            _creds = creds;
            _db = db;

            _client.ReactionAdded += OnReactionAddedAsync;
        }
        
        private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (_message is null) return;
            if (reaction.MessageId == _message.Id)
            {
                if (_eventStarted)
                {
                    if (reaction.Emote.Name == _heart.Name)
                    {
                        if (_heartUsers.All(x => x.Id != reaction.User.Value.Id))
                        {
                            if (reaction.User.Value.Id != _client.CurrentUser.Id)
                            {
                                _heartUsers.Enqueue(reaction.User.Value);
                                await AwardUserHeartsAsync(reaction.User.Value, _reward);
                            }
                        }
                    }
                }
            }
        }
        
        public async Task StartHeartEventAsync(IUserMessage message, int timeout, int reward)
        {
            if (!_eventStarted)
            {
                _message = message;
                _reward = reward;
                _eventStarted = true;
                _heartUsers = new Queue<IUser>();
                _heart = Emote.Parse(_creds.Currency);
                await _message.AddReactionAsync(_heart);

                await Task.Delay(timeout * 1000);

                if (_eventStarted)
                {
                    _eventStarted = false;
                    await _message.DeleteAsync();
                }
            }
        }
        
        private async Task AwardUserHeartsAsync(IUser user, int reward)
        {
            using (var db = _db.GetDbContext())
            {
                var userDb = db.Users.FirstOrDefault(x => x.UserId == user.Id);
                if (userDb != null)
                {
                    userDb.Currency += reward;
                }
                else
                {
                    var userConfig = new UserConfig { UserId = user.Id, Currency = reward };
                    await db.AddAsync(userConfig);
                }
                await db.SaveChangesAsync();
            }
        }

        public async Task StopHeartEventAsync()
        {
            _eventStarted = false;
            await _message.DeleteAsync();
        }
    }
}