using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Music.Commons;
using RiasBot.Services;

namespace RiasBot.Modules.Music.Services
{
    [Service]
    public class MusicService
    {
        public readonly DiscordShardedClient Client;
        private readonly IAudioService _audioService;
        private readonly IBotCredentials _creds;
        private readonly ITranslations _tr;
        private readonly InteractiveService _is;
        private readonly DbService _db;

        public bool LavalinkOk { get; private set; }

        public MusicService(DiscordShardedClient client, IAudioService audioService,
            IBotCredentials creds, ITranslations tr, InteractiveService iss, DbService db)
        {
            Client = client;
            _audioService = audioService;
            _creds = creds;
            _tr = tr;
            _is = iss;
            _db = db;

            client.UserVoiceStateUpdated += UserVoiceStateUpdatedAsync;

            var lavalinkNode = (LavalinkNode) _audioService;
            lavalinkNode.Connected += LavalinkConnectedAsync;
            lavalinkNode.Disconnected += LavalinkDisconnectedAsync;
            lavalinkNode.PlayerDisconnected += PlayerDisconnectedAsync;
        }

        private const string Module = "music";

//        private async Task ReplyConfirmationAsync(IMessageChannel channel, ulong guildId, string lowerModuleTypeName, string key)
//            => await channel.SendConfirmationMessageAsync(_tr.GetText(guildId, lowerModuleTypeName, key));

        public async Task ReplyConfirmationAsync(IMessageChannel channel, ulong guildId, string key, params object[] args)
            => await channel.SendConfirmationMessageAsync(_tr.GetText(guildId, Module, key, args));

        public async Task ReplyErrorAsync(IMessageChannel channel, ulong guildId, string key)
            => await channel.SendErrorMessageAsync(_tr.GetText(guildId, Module, key));

        public async Task ReplyErrorAsync(IMessageChannel channel, ulong guildId, string key, params object[] args)
            => await channel.SendErrorMessageAsync(_tr.GetText(guildId, Module, key, args));

        public string GetText(ulong guildId, string key)
            => _tr.GetText(guildId, Module, key);

        public string GetText(ulong guildId, string key, params object[] args)
            => _tr.GetText(guildId, Module, key, args);

        public MusicPlayer GetPlayer(ulong guildId)
            => _audioService.GetPlayer<MusicPlayer>(guildId);

        public async Task<SocketMessage> NextMessageAsync(ShardedCommandContext context, TimeSpan timeout)
            => await _is.NextMessageAsync(context, timeout: timeout);

        public async Task<MusicPlayer> InitializePlayerAsync(IVoiceChannel voiceChannel, IMessageChannel channel)
        {
            if (!LavalinkOk)
            {
                await ReplyConfirmationAsync(channel, voiceChannel.GuildId, "lavalink_not_ready");
                return null;
            }

            var player = await _audioService.JoinAsync<MusicPlayer>(voiceChannel.GuildId, voiceChannel.Id);
            await ReplyConfirmationAsync(channel, voiceChannel.GuildId, "channel_connected", voiceChannel.Name);

            player.Initialize(this, _creds, channel, voiceChannel, GetPatreonPlayerFeatures((SocketGuild) voiceChannel.Guild));
            return player;
        }

        public async Task<TrackLoadResponsePayload> LoadTracksAsync(string query, SearchMode searchMode)
            => await _audioService.LoadTracksAsync(query, searchMode);

        public PatreonPlayerFeatures GetPatreonPlayerFeatures(SocketGuild guild)
        {
            if (guild.OwnerId == _creds.MasterId)
                return PatreonPlayerFeatures.Volume | PatreonPlayerFeatures.LongTracks | PatreonPlayerFeatures.Livestream;

            using (var db = _db.GetDbContext())
            {
                var playerPatreonFeatures = PatreonPlayerFeatures.None;

                var patreonDb = db.Patreon.FirstOrDefault(x => x.UserId == guild.OwnerId);
                if (patreonDb is null)
                    return playerPatreonFeatures;

                if (patreonDb.Reward >= 5000)
                    playerPatreonFeatures |= PatreonPlayerFeatures.Volume;
                if (patreonDb.Reward >= 10000)
                    playerPatreonFeatures |= PatreonPlayerFeatures.LongTracks;
                if (patreonDb.Reward >= 15000)
                    playerPatreonFeatures |= PatreonPlayerFeatures.Livestream;

                return playerPatreonFeatures;
            }
        }

        private async Task UserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            var guildUser = (IGuildUser) user;
            var player = _audioService.GetPlayer<MusicPlayer>(guildUser.GuildId);
            if (player is null)
                return;

            if (user.Id == Client.CurrentUser.Id && newState.VoiceChannel != null)
            {
                player.ChangeVoiceChannel(newState.VoiceChannel);
            }

            if (oldState.VoiceChannel != null)
                await AutoDisconnect(oldState.VoiceChannel.Users, guildUser, player);

            if (newState.VoiceChannel != null)
                await AutoDisconnect(newState.VoiceChannel.Users, guildUser, player);
        }

        private async Task AutoDisconnect(IReadOnlyCollection<SocketGuildUser> users, IGuildUser guildUser, MusicPlayer player)
        {
            if (!users.Contains(await guildUser.Guild.GetCurrentUserAsync()))
                return;

            if (users.Count(u => !u.IsBot) < 1)
            {
                await StartAutoDisconnecting(TimeSpan.FromMinutes(2), player);
            }
            else
            {
                await StopAutoDisconnecting(player);
            }
        }

        private async Task StartAutoDisconnecting(TimeSpan dueTime, MusicPlayer player)
        {
            if (player.State != PlayerState.Paused)
                await player.PauseAsync(false);

            player.AutoDisconnectTimer = new Timer(async _ => await player.LeaveAndDisposeAsync(), null, dueTime, TimeSpan.Zero);

            var outputChannelState = MusicUtils.CheckOutputChannel(Client, player.GuildId, player.OutputChannel);
            if (outputChannelState == OutputChannelState.Available)
                await ReplyConfirmationAsync(player.OutputChannel, player.GuildId, "stop_after");
        }

        private async Task StopAutoDisconnecting(MusicPlayer player)
        {
            if (player.AutoDisconnectTimer is null)
                return;

            if (player.State == PlayerState.Paused)
            {
                var outputChannelState = MusicUtils.CheckOutputChannel(Client, player.GuildId, player.OutputChannel);
                var sendMessage = outputChannelState == OutputChannelState.Available;
                await player.ResumeAsync(sendMessage);
            }

            player.AutoDisconnectTimer.Dispose();
            player.AutoDisconnectTimer = null;
        }

        private Task LavalinkConnectedAsync(object sender, ConnectedEventArgs args)
        {
            LavalinkOk = true;
            return Task.CompletedTask;
        }

        private Task LavalinkDisconnectedAsync(object sender, DisconnectedEventArgs args)
        {
            LavalinkOk = false;
            return Task.CompletedTask;
        }

        private async Task PlayerDisconnectedAsync(object sender, PlayerDisconnectedEventArgs args)
        {
            var player = (MusicPlayer) args.Player;
            var outputChannelState = MusicUtils.CheckOutputChannel(Client, player.GuildId, player.OutputChannel);
            var sendMessage = outputChannelState == OutputChannelState.Available;
            if (args.DisconnectCause == PlayerDisconnectCause.Disconnected && sendMessage)
                await ReplyConfirmationAsync(player.OutputChannel, player.GuildId, "channel_disconnected", player.VoiceChannel.Name);
        }
    }
}