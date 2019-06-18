using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RiasBot.Commons;
using RiasBot.Commons.Attributes;
using RiasBot.Database.Models;
using RiasBot.Services.Websockets;

namespace RiasBot.Services
{
    [Service]
    public class VotesService
    {
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly RLog _log;
        
        public VotesService(IBotCredentials creds, DbService db, RLog log)
        {
            _creds = creds;
            _db = db;
            _log = log;
        }
        
        public List<Votes> VotesList;
        private VotesWebsocket _votesWebSocket;
        
        private string _protocol;
        
        public async Task ConfigureVotesWebSocket()
        {
            if (string.IsNullOrEmpty(_creds.VotesManagerConfig.WebSocketHost) || _creds.VotesManagerConfig.WebSocketPort == 0)
            {
                //the votes manager is not configured
                return;
            }
            
            _protocol = _creds.VotesManagerConfig.IsSecureConnection ? "https" : "http";
            
            _votesWebSocket = new VotesWebsocket(_creds.VotesManagerConfig, _log);
            await _votesWebSocket.Connect();

            _votesWebSocket.OnConnected += VotesWebSocketConnected;
            _votesWebSocket.OnReceive += AwardVoter;
        }
        
        private async Task LoadVotes()
        {
            try
            {
                var stw = new Stopwatch();
                stw.Start();
                
                using (var db = _db.GetDbContext())
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Clear();
                    http.DefaultRequestHeaders.Add("Authorization", _creds.VotesManagerConfig.Authorization);
                    var votesApi = await http.GetStringAsync($"{_protocol}://{_creds.VotesManagerConfig.WebSocketHost}/api/votes");
                    var dblVotes = JsonConvert.DeserializeObject<DBL>(votesApi);
                    
                    VotesList = new List<Votes>();
                    var votes = dblVotes.Votes.Where(x => x.Type == "upvote").ToList();
                    
                    foreach (var vote in votes)
                    {
                        var date = vote.Date.AddHours(12);
                        if (DateTime.Compare(date.ToUniversalTime(), DateTime.UtcNow) >= 1)
                        {
                            VotesList.Add(vote);
                            if (vote.IsChecked) continue;
                            
                            var userDb = db.Users.FirstOrDefault(x => x.UserId == vote.User);
                            if (userDb != null)
                            {
                                if (!userDb.IsBlacklisted)
                                    userDb.Currency += vote.IsWeekend ? 50 : 25;
                            }
                            else
                            {
                                var currency = new UserConfig { UserId = vote.User, Currency = vote.IsWeekend ? 50 : 25 };
                                await db.AddAsync(currency);
                            }

                            await UpdateVote(vote.User);
                            await db.SaveChangesAsync();
                        }
                    }
                }

                stw.Stop();
                await _log.Info($"Votes list loaded: {stw.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        
        private async Task RefreshVotes()
        {
            try
            {
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Clear();
                    http.DefaultRequestHeaders.Add("Authorization", _creds.VotesManagerConfig.Authorization);
                    var votesApi = await http.GetStringAsync($"{_protocol}://{_creds.VotesManagerConfig.WebSocketHost}/api/votes");
                    var dblVotes = JsonConvert.DeserializeObject<DBL>(votesApi);
                    
                    VotesList = new List<Votes>();
                    var votes = dblVotes.Votes.Where(x => x.Type == "upvote").ToList();
                    
                    foreach (var vote in votes)
                    {
                        var date = vote.Date.AddHours(12);
                        if (DateTime.Compare(date.ToUniversalTime(), DateTime.UtcNow) >= 1)
                        {
                            VotesList.Add(vote);
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex);
            }
        }
        
        private async Task AwardVoter(JObject vote)
        {
            using (var db = _db.GetDbContext())
            {
                var userId = ulong.Parse(vote["User"].ToString());
                var isWeekend = (bool) vote["IsWeekend"];
                var userDb = db.Users.FirstOrDefault(x => x.UserId == userId);
                if (userDb != null)
                {
                    if (!userDb.IsBlacklisted)
                        userDb.Currency += isWeekend ? 50 : 25;
                }
                else
                {
                    var currency = new UserConfig { UserId = userId, Currency = isWeekend ? 50 : 25 };
                    await db.AddAsync(currency);
                }
                await UpdateVote(userId);
                await db.SaveChangesAsync();
            }
        }
        
        private async Task UpdateVote(ulong userId)
        {
            try
            {
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Clear();
                    http.DefaultRequestHeaders.Add("Authorization", _creds.VotesManagerConfig.Authorization);

                    await http.PostAsync($"{_protocol}://{_creds.VotesManagerConfig.WebSocketHost}/api/votes/{userId}", null);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex);
            }
            await RefreshVotes();
        }

        private async Task VotesWebSocketConnected()
        {
            await LoadVotes();
        }
    }
}